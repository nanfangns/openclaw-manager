import android.content.Context;
import java.util.Map;

loadJava("data/api.java");

public class TTSModel {

    public static Map<String, String> getCharacters() {
        return TTSApi.getCharacters();
    }

    private static String performConvertInternal(String text, String voice) throws Exception {
        return TTSApi.performConvert(text, voice);
    }

    public static String performConvert(String text, String voice, String uidIgnored) throws Exception {
        return performConvertInternal(text, voice);
    }

    public static String performConvert(String text, String voice) throws Exception {
        return performConvertInternal(text, voice);
    }
}

class PluginSettings {
    public static void openSettings(Context context) {
        try {
            HostBridge.toast("Edge TTS 服务地址: " + TTSApi.TTS_BASE_URL);
        } catch (Throwable ignored) {
        }
    }

    public static void openSettings(Object context) {
        openSettings(context instanceof Context ? (Context) context : null);
    }

    public static void openSettings() {
        openSettings((Context) null);
    }
}
