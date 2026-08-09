"""
Microsoft Edge TTS WebSocket 协议实现
逆向自 text-to-speech.online 前端 JavaScript
"""
import asyncio
import json
import uuid
import time
import hashlib
from typing import Optional, AsyncGenerator

try:
    import websockets
except ImportError:
    raise ImportError("请安装 websockets: pip install websockets")

# === 常量 ===
TRUSTED_CLIENT_TOKEN = "6A5AA1D4EAFF4E9FB37E23D68491D6F4"
GEC_VERSION = "1-147.0.3912.98"
WINDOWS_EPOCH_OFFSET = 11644473600  # 1601-01-01 到 1970-01-01 的秒数
OUTPUT_FORMAT = "audio-24khz-48kbitrate-mono-mp3"


def generate_ecn_version() -> str:
    """生成 Sec-MS-GEC 签名"""
    now = int(time.time())
    n = now + WINDOWS_EPOCH_OFFSET
    n -= n % 300
    n = int(n * (1e9 / 100))
    payload = f"{n}{TRUSTED_CLIENT_TOKEN}"
    digest = hashlib.sha256(payload.encode("utf-8")).hexdigest().upper()
    return digest


def generate_connection_id() -> str:
    """生成 UUID 格式的 ConnectionId"""
    return str(uuid.uuid4()).upper()


def generate_request_id() -> str:
    """生成请求 UUID"""
    return str(uuid.uuid4()).upper()


def generate_timestamp() -> str:
    """生成 ISO 时间戳"""
    return time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())


def escape_xml(text: str) -> str:
    """转义 XML 特殊字符"""
    replacements = {
        "<": "&lt;",
        ">": "&gt;",
        "&": "&amp;",
        "'": "&apos;",
        '"': "&quot;",
    }
    for char, entity in replacements.items():
        text = text.replace(char, entity)
    return text


def format_prosody_value(value: int, min_val: int, max_val: int) -> str:
    """格式化 prosody 值（百分比）"""
    value = max(min_val, min(max_val, round(value)))
    return f"{value}%"


def build_audio_config(output_format: str = OUTPUT_FORMAT) -> str:
    """构建音频配置消息"""
    config = {
        "context": {
            "synthesis": {
                "audio": {
                    "metadataoptions": {
                        "sentenceBoundaryEnabled": False,
                        "wordBoundaryEnabled": True
                    },
                    "outputFormat": output_format
                }
            }
        }
    }
    return (
        f"X-Timestamp:{generate_timestamp()}\r\n"
        f"Content-Type:application/json; charset=utf-8\r\n"
        f"Path:speech.config\r\n"
        f"\r\n"
        f"{json.dumps(config)}"
    )


def build_ssml(
    voice_name: str,
    text: str,
    rate_percent: int = 0,
    pitch_percent: int = 0,
    volume_percent: int = 0,
) -> str:
    """构建 SSML 消息"""
    rate = format_prosody_value(rate_percent, -50, 50)
    pitch = format_prosody_value(pitch_percent, -100, 200)
    volume = format_prosody_value(volume_percent, -100, 100)

    ssml = (
        f"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>"
        f"<voice name='{voice_name}'>"
        f"<prosody pitch='{pitch}' rate='{rate}' volume='{volume}'>"
        f"{escape_xml(text)}"
        f"</prosody></voice></speak>"
    )

    return (
        f"X-RequestId:{generate_request_id()}\r\n"
        f"Content-Type:application/ssml+xml\r\n"
        f"X-Timestamp:{generate_timestamp()}Z\r\n"
        f"Path:ssml\r\n"
        f"\r\n"
        f"{ssml}"
    )


def parse_headers(text: str) -> dict:
    """解析 WebSocket 消息头"""
    headers = {}
    for line in text.split("\r\n"):
        if ":" in line:
            key, value = line.split(":", 1)
            headers[key.strip()] = value.strip()
    return headers


async def parse_binary_message(data: bytes) -> Optional[dict]:
    """解析二进制消息"""
    if len(data) < 2:
        return None
    header_len = (data[0] << 8) | data[1]
    if header_len + 2 > len(data):
        return None
    header_bytes = data[2:header_len + 2]
    headers = parse_headers(header_bytes.decode("utf-8"))
    payload = data[header_len + 2:]
    return {"headers": headers, "payload": payload}


class EdgeTTS:
    """Edge TTS 客户端"""

    # 优先使用 text-to-speech.online 代理（不需要 Edge 扩展 Origin）
    # 如果需要直接连 bing.com，需要 Origin: chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold
    DEFAULT_BASE_URL = "speech.text-to-speech.online/consumer/speech/synthesize/readaloud/edge/v1"
    BING_BASE_URL = "speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1"

    def __init__(
        self,
        voice: str = "zh-CN-XiaoxiaoNeural",
        rate: int = 0,
        pitch: int = 0,
        volume: int = 0,
        base_url: str = None,
    ):
        self.voice = voice
        self.rate = rate
        self.pitch = pitch
        self.volume = volume
        self.base_url = base_url or self.DEFAULT_BASE_URL
        # 根据 base_url 决定 Origin 头
        self._origin = (
            "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold"
            if "bing.com" in self.base_url
            else "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold"
        )

    def _build_ws_url(self) -> str:
        """构建 WebSocket URL"""
        sec_ms_gec = generate_ecn_version()
        connection_id = generate_connection_id()
        return (
            f"wss://{self.base_url}"
            f"?TrustedClientToken={TRUSTED_CLIENT_TOKEN}"
            f"&Ocp-Apim-Subscription-Key={TRUSTED_CLIENT_TOKEN}"
            f"&Sec-MS-GEC={sec_ms_gec}"
            f"&Sec-MS-GEC-Version={GEC_VERSION}"
            f"&ConnectionId={connection_id}"
        )

    async def synthesize(self, text: str) -> AsyncGenerator[bytes, None]:
        """
        合成语音，异步生成音频数据块

        Yields:
            bytes: MP3 音频数据块
        """
        url = self._build_ws_url()

        async with websockets.connect(
            url,
            additional_headers={
                "Origin": self._origin,
                "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 Edg/131.0.0.0",
            },
            open_timeout=15,
            ping_interval=30,
            ping_timeout=10,
            close_timeout=5,
        ) as ws:
            # 发送音频配置
            await ws.send(build_audio_config())
            # 发送 SSML
            await ws.send(build_ssml(
                voice_name=self.voice,
                text=text,
                rate_percent=self.rate,
                pitch_percent=self.pitch,
                volume_percent=self.volume,
            ))

            turn_complete = False
            while not turn_complete:
                try:
                    msg = await asyncio.wait_for(ws.recv(), timeout=120)
                except asyncio.TimeoutError:
                    break

                if isinstance(msg, str):
                    headers = parse_headers(msg)
                    path = headers.get("Path", "")
                    if path == "turn.end":
                        turn_complete = True
                    elif path == "response":
                        pass  # 响应开始
                elif isinstance(msg, bytes):
                    result = await parse_binary_message(msg)
                    if result and result["headers"].get("Path") == "audio":
                        content_type = result["headers"].get("Content-Type", "")
                        if content_type == "audio/mpeg" and len(result["payload"]) > 0:
                            yield result["payload"]

    async def synthesize_full(self, text: str) -> bytes:
        """
        合成语音，返回完整音频数据

        Returns:
            bytes: 完整的 MP3 音频数据
        """
        chunks = []
        async for chunk in self.synthesize(text):
            chunks.append(chunk)
        return b"".join(chunks)
