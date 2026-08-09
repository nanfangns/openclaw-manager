"""
TTS 2API 服务器
提供 OpenAI 兼容的 TTS API 接口
逆向自 text-to-speech.online
"""
import os
import io
import json
import time
import uuid
import asyncio
import hashlib
import base64
import secrets
from typing import Optional, List
from datetime import datetime
from pathlib import Path

from fastapi import FastAPI, HTTPException, Request, Query
from fastapi.responses import StreamingResponse, Response, FileResponse, JSONResponse
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel, Field

from edge_tts_engine import EdgeTTS

# ============================================================
# 配置
# ============================================================
SERVER_HOST = "0.0.0.0"
SERVER_PORT = 8100
PUBLIC_BASE_URL = os.environ.get("TTS_PUBLIC_URL", "https://tts.nanfangxu.eu.cc")

# 来自 text-to-speech.online 的常量
SITE_BASE_URL = "https://www.text-to-speech.online"
API_BASE = f"{SITE_BASE_URL}/api/index.php"
AES_KEY = b"FreeTTSOnline2024SecretKey!!1234"

# 音频缓存目录
CACHE_DIR = Path(os.environ.get("TTS_CACHE_DIR", "/home/agent/audiotts/cache"))
CACHE_DIR.mkdir(parents=True, exist_ok=True)
# 缓存清理：最大保留 200 个文件 或 200MB，超出自动删旧的
CACHE_MAX_FILES = int(os.environ.get("TTS_CACHE_MAX_FILES", "200"))
CACHE_MAX_SIZE_MB = int(os.environ.get("TTS_CACHE_MAX_SIZE_MB", "200"))


def cleanup_cache():
    """清理过期缓存文件，保持 cache 目录可控"""
    try:
        files = sorted(CACHE_DIR.glob("*.mp3"), key=lambda f: f.stat().st_mtime)
        # 按数量清理
        while len(files) > CACHE_MAX_FILES:
            oldest = files.pop(0)
            oldest.unlink(missing_ok=True)
        # 按大小清理
        total = sum(f.stat().st_size for f in CACHE_DIR.glob("*.mp3"))
        max_bytes = CACHE_MAX_SIZE_MB * 1024 * 1024
        while total > max_bytes and files:
            oldest = files.pop(0)
            total -= oldest.stat().st_size
            oldest.unlink(missing_ok=True)
    except Exception:
        pass

# ============================================================
# FastAPI 应用
# ============================================================
app = FastAPI(
    title="TTS 2API",
    description="OpenAI 兼容的文本转语音 API，基于 Edge TTS",
    version="1.0.0",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# 挂载缓存目录为静态文件
app.mount("/cache", StaticFiles(directory=str(CACHE_DIR)), name="cache")


# ============================================================
# 请求/响应模型
# ============================================================
class TTSRequest(BaseModel):
    """OpenAI 兼容的 TTS 请求"""
    model: str = Field(default="tts-1", description="模型名称 (tts-1 / tts-1-hd)")
    input: str = Field(..., description="要转换为语音的文本", max_length=4096)
    voice: str = Field(default="alloy", description="语音名称")
    response_format: str = Field(default="mp3", description="音频格式 (mp3 / opus / aac / flac / wav / pcm)")
    speed: float = Field(default=1.0, ge=0.25, le=4.0, description="语速 (0.25-4.0)")
    # 扩展字段
    pitch: int = Field(default=0, ge=-100, le=200, description="音调偏移 (-100 到 200)")


class VoiceInfo(BaseModel):
    """语音信息"""
    name: str
    locale: str
    gender: str
    display_name: str


class VoiceListResponse(BaseModel):
    """语音列表响应"""
    voices: List[VoiceInfo]
    total: int


# ============================================================
# 语音映射表 (OpenAI 风格 -> Edge TTS)
# ============================================================
VOICE_MAPPING = {
    # OpenAI 默认语音 -> Edge TTS 中文语音
    "alloy": "zh-CN-XiaoxiaoNeural",
    "echo": "zh-CN-YunxiNeural",
    "fable": "zh-CN-YunyangNeural",
    "onyx": "zh-CN-YunjianNeural",
    "nova": "zh-CN-XiaoyiNeural",
    "shimmer": "zh-CN-XiaohanNeural",
    # 也支持直接使用 Edge TTS 语音名称
}

# 速率映射: OpenAI speed (0.25-4.0) -> Edge TTS rate (-50% 到 +50%)
def speed_to_rate(speed: float) -> int:
    """将 OpenAI speed 映射到 Edge TTS rate 百分比"""
    # speed=1.0 -> rate=0, speed=0.25 -> rate=-50, speed=4.0 -> rate=50
    # 使用对数映射更自然
    import math
    if speed <= 0:
        return 0
    rate = (math.log2(speed)) * 50
    return max(-50, min(50, round(rate)))


def resolve_voice(voice_name: str) -> str:
    """解析语音名称，支持 OpenAI 风格和 Edge TTS 原名"""
    if voice_name in VOICE_MAPPING:
        return VOICE_MAPPING[voice_name]
    # 直接使用 Edge TTS 语音名称
    return voice_name


# ============================================================
# 核心 TTS 端点
# ============================================================
@app.post("/v1/audio/speech", response_class=Response)
async def create_speech(request: TTSRequest):
    """
    OpenAI 兼容的 TTS 端点

    POST /v1/audio/speech
    Body: {"model": "tts-1", "input": "你好世界", "voice": "alloy"}

    返回: 音频二进制流
    """
    voice = resolve_voice(request.voice)
    rate = speed_to_rate(request.speed)

    engine = EdgeTTS(
        voice=voice,
        rate=rate,
        pitch=request.pitch,
    )

    try:
        audio_data = await engine.synthesize_full(request.input)
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"TTS 合成失败: {str(e)}")

    if not audio_data:
        raise HTTPException(status_code=500, detail="TTS 合成返回空数据")

    # 根据请求格式设置 Content-Type
    content_types = {
        "mp3": "audio/mpeg",
        "opus": "audio/opus",
        "aac": "audio/aac",
        "flac": "audio/flac",
        "wav": "audio/wav",
        "pcm": "audio/pcm",
    }
    content_type = content_types.get(request.response_format, "audio/mpeg")

    return Response(
        content=audio_data,
        media_type=content_type,
        headers={
            "Content-Disposition": f'attachment; filename="speech.{request.response_format}"'
        }
    )


@app.post("/v1/audio/speech/stream")
async def create_speech_stream(request: TTSRequest):
    """
    流式 TTS 端点

    返回 Server-Sent Events 格式的音频流
    """
    voice = resolve_voice(request.voice)
    rate = speed_to_rate(request.speed)

    engine = EdgeTTS(
        voice=voice,
        rate=rate,
        pitch=request.pitch,
    )

    async def audio_stream():
        try:
            async for chunk in engine.synthesize(request.input):
                yield chunk
        except Exception as e:
            yield json.dumps({"error": str(e)}).encode()

    return StreamingResponse(
        audio_stream(),
        media_type="audio/mpeg",
        headers={
            "Content-Disposition": f'attachment; filename="speech.mp3"',
            "Transfer-Encoding": "chunked",
        }
    )


# ============================================================
# URL 端点 (供 FunPlugin 等需要直接 URL 的场景)
# ============================================================
@app.get("/tts")
async def tts_get_url(
    text: str = Query(..., description="要转换的文本"),
    voice: str = Query(default="alloy", description="语音名称"),
    speed: float = Query(default=1.0, ge=0.25, le=4.0),
    pitch: int = Query(default=0, ge=-100, le=200),
):
    """生成 TTS 音频并返回可直接访问的 URL"""
    resolved_voice = resolve_voice(voice)
    rate = speed_to_rate(speed)
    engine = EdgeTTS(voice=resolved_voice, rate=rate, pitch=pitch)
    try:
        audio_data = await engine.synthesize_full(text)
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"TTS 合成失败: {str(e)}")
    if not audio_data:
        raise HTTPException(status_code=500, detail="TTS 合成返回空数据")
    filename = f"{secrets.token_hex(8)}.mp3"
    filepath = CACHE_DIR / filename
    filepath.write_bytes(audio_data)
    cleanup_cache()
    url = f"{PUBLIC_BASE_URL}/cache/{filename}"
    return JSONResponse({"url": url, "voice": resolved_voice, "format": "mp3"})


@app.post("/tts")
async def tts_post_url(request: TTSRequest):
    """POST 版本的 URL 端点"""
    resolved_voice = resolve_voice(request.voice)
    rate = speed_to_rate(request.speed)
    engine = EdgeTTS(voice=resolved_voice, rate=rate, pitch=request.pitch)
    try:
        audio_data = await engine.synthesize_full(request.input)
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"TTS 合成失败: {str(e)}")
    if not audio_data:
        raise HTTPException(status_code=500, detail="TTS 合成返回空数据")
    filename = f"{secrets.token_hex(8)}.mp3"
    filepath = CACHE_DIR / filename
    filepath.write_bytes(audio_data)
    cleanup_cache()
    url = f"{PUBLIC_BASE_URL}/cache/{filename}"
    return JSONResponse({"url": url, "voice": resolved_voice, "format": "mp3"})


# ============================================================
# 语音列表端点
# ============================================================
@app.get("/v1/audio/voices")
async def list_voices():
    """返回常用 Edge TTS 语音列表（内置）"""
    voices = [
        # 中文
        VoiceInfo(name="zh-CN-XiaoxiaoNeural", locale="zh-CN", gender="Female", display_name="晓晓"),
        VoiceInfo(name="zh-CN-YunxiNeural", locale="zh-CN", gender="Male", display_name="云希"),
        VoiceInfo(name="zh-CN-YunyangNeural", locale="zh-CN", gender="Male", display_name="云扬"),
        VoiceInfo(name="zh-CN-YunjianNeural", locale="zh-CN", gender="Male", display_name="云健"),
        VoiceInfo(name="zh-CN-XiaoyiNeural", locale="zh-CN", gender="Female", display_name="晓伊"),
        VoiceInfo(name="zh-CN-XiaohanNeural", locale="zh-CN", gender="Female", display_name="晓涵"),
        VoiceInfo(name="zh-CN-XiaoxuanNeural", locale="zh-CN", gender="Female", display_name="晓萱"),
        VoiceInfo(name="zh-CN-XiaomoNeural", locale="zh-CN", gender="Female", display_name="晓墨"),
        VoiceInfo(name="zh-CN-YunfengNeural", locale="zh-CN", gender="Male", display_name="云枫"),
        VoiceInfo(name="zh-CN-YunhaoNeural", locale="zh-CN", gender="Male", display_name="云皓"),
        VoiceInfo(name="zh-CN-YunxiaNeural", locale="zh-CN", gender="Male", display_name="云夏"),
        VoiceInfo(name="zh-CN-XiaochenNeural", locale="zh-CN", gender="Female", display_name="晓辰"),
        VoiceInfo(name="zh-CN-XiaomengNeural", locale="zh-CN", gender="Female", display_name="晓梦"),
        VoiceInfo(name="zh-CN-XiaoyouNeural", locale="zh-CN", gender="Female", display_name="晓悠"),
        VoiceInfo(name="zh-CN-XiaoruiNeural", locale="zh-CN", gender="Female", display_name="晓睿"),
        VoiceInfo(name="zh-CN-XiaoqiuNeural", locale="zh-CN", gender="Female", display_name="晓秋"),
        VoiceInfo(name="zh-CN-YunzeNeural", locale="zh-CN", gender="Male", display_name="云泽"),
        VoiceInfo(name="zh-CN-XiaoshuangNeural", locale="zh-CN", gender="Female", display_name="晓双"),
        VoiceInfo(name="zh-CN-XiaoruiNeural", locale="zh-CN", gender="Female", display_name="晓锐"),
        # 粤语
        VoiceInfo(name="zh-HK-HiuMaanNeural", locale="zh-HK", gender="Female", display_name="晓曼"),
        VoiceInfo(name="zh-HK-WanLungNeural", locale="zh-HK", gender="Male", display_name="云龙"),
        # 英文
        VoiceInfo(name="en-US-JennyNeural", locale="en-US", gender="Female", display_name="Jenny"),
        VoiceInfo(name="en-US-GuyNeural", locale="en-US", gender="Male", display_name="Guy"),
        VoiceInfo(name="en-US-AriaNeural", locale="en-US", gender="Female", display_name="Aria"),
        VoiceInfo(name="en-US-DavisNeural", locale="en-US", gender="Male", display_name="Davis"),
        VoiceInfo(name="en-US-SaraNeural", locale="en-US", gender="Female", display_name="Sara"),
        VoiceInfo(name="en-US-TonyNeural", locale="en-US", gender="Male", display_name="Tony"),
        VoiceInfo(name="en-GB-SoniaNeural", locale="en-GB", gender="Female", display_name="Sonia"),
        VoiceInfo(name="en-GB-RyanNeural", locale="en-GB", gender="Male", display_name="Ryan"),
        VoiceInfo(name="en-AU-NatashaNeural", locale="en-AU", gender="Female", display_name="Natasha"),
        VoiceInfo(name="en-IE-EmilyNeural", locale="en-IE", gender="Female", display_name="Emily"),
        # 日文
        VoiceInfo(name="ja-JP-NanamiNeural", locale="ja-JP", gender="Female", display_name="Nanami"),
        VoiceInfo(name="ja-JP-KeitaNeural", locale="ja-JP", gender="Male", display_name="Keita"),
        # 韩文
        VoiceInfo(name="ko-KR-SunHiNeural", locale="ko-KR", gender="Female", display_name="SunHi"),
        VoiceInfo(name="ko-KR-InJoonNeural", locale="ko-KR", gender="Male", display_name="InJoon"),
        # 法文
        VoiceInfo(name="fr-FR-DeniseNeural", locale="fr-FR", gender="Female", display_name="Denise"),
        VoiceInfo(name="fr-FR-HenriNeural", locale="fr-FR", gender="Male", display_name="Henri"),
        # 德文
        VoiceInfo(name="de-DE-KatjaNeural", locale="de-DE", gender="Female", display_name="Katja"),
        VoiceInfo(name="de-DE-ConradNeural", locale="de-DE", gender="Male", display_name="Conrad"),
        # 西班牙语
        VoiceInfo(name="es-ES-ElviraNeural", locale="es-ES", gender="Female", display_name="Elvira"),
        VoiceInfo(name="es-ES-AlvaroNeural", locale="es-ES", gender="Male", display_name="Alvaro"),
        # 葡萄牙语
        VoiceInfo(name="pt-BR-FranciscaNeural", locale="pt-BR", gender="Female", display_name="Francisca"),
        VoiceInfo(name="pt-BR-AntonioNeural", locale="pt-BR", gender="Male", display_name="Antonio"),
        # 俄语
        VoiceInfo(name="ru-RU-SvetlanaNeural", locale="ru-RU", gender="Female", display_name="Svetlana"),
        VoiceInfo(name="ru-RU-DmitryNeural", locale="ru-RU", gender="Male", display_name="Dmitry"),
        # 阿拉伯语
        VoiceInfo(name="ar-SA-ZariyahNeural", locale="ar-SA", gender="Female", display_name="Zariyah"),
        VoiceInfo(name="ar-SA-HamedNeural", locale="ar-SA", gender="Male", display_name="Hamed"),
        # 印地语
        VoiceInfo(name="hi-IN-SwaraNeural", locale="hi-IN", gender="Female", display_name="Swara"),
        VoiceInfo(name="hi-IN-MadhurNeural", locale="hi-IN", gender="Male", display_name="Madhur"),
    ]
    return VoiceListResponse(voices=voices, total=len(voices))


# ============================================================
# OpenAI 兼容的模型列表
# ============================================================
@app.get("/v1/models")
async def list_models():
    """返回可用模型列表"""
    return {
        "object": "list",
        "data": [
            {
                "id": "tts-1",
                "object": "model",
                "owned_by": "edge-tts",
            },
            {
                "id": "tts-1-hd",
                "object": "model",
                "owned_by": "edge-tts",
            },
        ]
    }


# ============================================================
# 用量检查端点 (模拟 text-to-speech.online)
# ============================================================
@app.get("/v1/usage")
async def get_usage():
    """返回使用量信息（模拟原站配额）"""
    return {
        "success": True,
        "usage": {
            "scope": "api",
            "plan": "free",
            "remainingPreview": 999,
            "remainingGenerate": 999,
            "totalPreview": 999,
            "totalGenerate": 999,
        }
    }


# ============================================================
# 根路径
# ============================================================
@app.get("/")
async def root():
    return {
        "service": "TTS 2API",
        "version": "1.0.0",
        "description": "OpenAI 兼容的文本转语音 API",
        "endpoints": {
            "tts": "POST /v1/audio/speech",
            "tts_stream": "POST /v1/audio/speech/stream",
            "tts_url": "GET /tts?text=你好&voice=alloy",
            "tts_url_post": "POST /tts",
            "voices": "GET /v1/audio/voices",
            "models": "GET /v1/models",
            "usage": "GET /v1/usage",
        },
        "docs": "/docs",
    }


# ============================================================
# 启动
# ============================================================
if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host=SERVER_HOST, port=SERVER_PORT)
