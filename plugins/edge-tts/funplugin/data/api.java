import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.net.HttpURLConnection;
import java.net.URL;
import java.net.URLEncoder;
import java.util.LinkedHashMap;
import java.util.Map;
import org.json.JSONObject;

class TTSApi {

    // ========== 在此修改你的 TTS 服务地址 ==========
    static final String TTS_BASE_URL = "https://tts.nanfangxu.eu.cc";
    // ================================================

    private static String readStream(java.io.InputStream in) throws Exception {
        if (in == null) return "";
        BufferedReader br = new BufferedReader(new InputStreamReader(in, "UTF-8"));
        StringBuilder sb = new StringBuilder();
        String line;
        while ((line = br.readLine()) != null) sb.append(line);
        br.close();
        return sb.toString();
    }

    static Map<String, String> getCharacters() {
        Map<String, String> characters = new LinkedHashMap<>();

        // 中文语音
        characters.put("晓晓 (女·温柔)", "zh-CN-XiaoxiaoNeural");
        characters.put("云希 (男·阳光)", "zh-CN-YunxiNeural");
        characters.put("云扬 (男·播音)", "zh-CN-YunyangNeural");
        characters.put("云健 (男·浑厚)", "zh-CN-YunjianNeural");
        characters.put("晓伊 (女·活泼)", "zh-CN-XiaoyiNeural");
        characters.put("晓涵 (女·知性)", "zh-CN-XiaohanNeural");
        characters.put("晓萱 (女·甜美)", "zh-CN-XiaoxuanNeural");
        characters.put("晓墨 (女·古典)", "zh-CN-XiaomoNeural");
        characters.put("云枫 (男·沉稳)", "zh-CN-YunfengNeural");
        characters.put("云皓 (男·少年)", "zh-CN-YunhaoNeural");
        characters.put("云夏 (男·青年)", "zh-CN-YunxiaNeural");
        characters.put("晓辰 (女·亲切)", "zh-CN-XiaochenNeural");
        characters.put("晓梦 (女·温柔)", "zh-CN-XiaomengNeural");
        characters.put("晓悠 (女·可爱)", "zh-CN-XiaoyouNeural");
        characters.put("晓睿 (女·成熟)", "zh-CN-XiaoruiNeural");
        characters.put("晓秋 (女·知性)", "zh-CN-XiaoqiuNeural");

        // 英文语音
        characters.put("Jenny (EN·Female)", "en-US-JennyNeural");
        characters.put("Guy (EN·Male)", "en-US-GuyNeural");
        characters.put("Aria (EN·Female)", "en-US-AriaNeural");
        characters.put("Davis (EN·Male)", "en-US-DavisNeural");

        // 日文语音
        characters.put("Nanami (JA·Female)", "ja-JP-NanamiNeural");
        characters.put("Keita (JA·Male)", "ja-JP-KeitaNeural");

        // 韩文语音
        characters.put("SunHi (KO·Female)", "ko-KR-SunHiNeural");
        characters.put("InJoon (KO·Male)", "ko-KR-InJoonNeural");

        return characters;
    }

    static String performConvert(String text, String voice) throws Exception {
        if (text == null || text.trim().length() == 0) {
            throw new IllegalArgumentException("文本不能为空");
        }
        text = text.trim();
        if (text.length() > 4000) text = text.substring(0, 4000);

        if (voice == null || voice.trim().length() == 0) {
            voice = "zh-CN-XiaoxiaoNeural";
        }
        voice = voice.trim();

        String encodedText = URLEncoder.encode(text, "UTF-8");
        String apiUrl = TTS_BASE_URL + "/tts?text=" + encodedText + "&voice=" + voice;

        HttpURLConnection conn = null;
        try {
            conn = (HttpURLConnection) new URL(apiUrl).openConnection();
            conn.setRequestMethod("GET");
            conn.setConnectTimeout(15000);
            conn.setReadTimeout(60000);
            conn.setRequestProperty("User-Agent", "FunPlugin/1.0");

            int code = conn.getResponseCode();
            if (code != 200) {
                throw new IOException("TTS 服务返回错误: HTTP " + code);
            }

            String body = readStream(conn.getInputStream());

            // 解析 JSON: {"url":"https://...mp3","voice":"...","format":"mp3"}
            JSONObject json = new JSONObject(body);
            if (json.has("url")) {
                String url = json.getString("url");
                if (url.startsWith("http://") || url.startsWith("https://")) {
                    return url;
                }
            }

            throw new IOException("TTS 响应中未找到音频 URL");
        } finally {
            if (conn != null) conn.disconnect();
        }
    }
}
