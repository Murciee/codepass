package com.codepass.lsposed;

import android.content.Context;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.telephony.SmsMessage;
import android.provider.Telephony;

import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.Charset;
import android.util.Base64;
import java.util.HashMap;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

import de.robv.android.xposed.IXposedHookLoadPackage;
import de.robv.android.xposed.XC_MethodHook;
import de.robv.android.xposed.XposedBridge;
import de.robv.android.xposed.XposedHelpers;
import de.robv.android.xposed.callbacks.XC_LoadPackage;

/**
 * Legacy Xposed entry point. The legacy API is intentionally used here because
 * it is available on both older LSPosed installations and current LSPosed.
 *
 * 配置仅通过受 UID 限制的 ConfigProvider 读取；读取失败即停止转发。
 * 清空全部目标并保存后不再回退到旧共享偏好或文件，防止短信继续发往旧目标。
 */
public final class CodepassModule implements IXposedHookLoadPackage {
    private static final String TAG = "codepass-lsposed";
    private static final String PHONE_PACKAGE = "com.android.phone";
    private static final String SMS_DELIVER_ACTION = "android.provider.Telephony.SMS_DELIVER";
    // 与 ConfigProvider.CONFIG_KEYS 保持一致（跨进程各自持有副本，修改时需同步）。
    private static final String[] CONFIG_KEYS = {
            "PC_IP", "PC_PORT", "TOKEN", "PC2_IP", "PC2_PORT", "PC2_TOKEN", "NTFY_SERVER", "NTFY_TOPIC", "NTFY_TOKEN", "MIN_LEN", "MAX_LEN"};
    private static final ExecutorService WORKER = Executors.newSingleThreadExecutor();

    /** 从 InboundSmsHandler 构造器捕获的进程 Context，用于访问 ConfigProvider。 */
    private static volatile Context processContext;
    private static volatile long providerRetryAt;
    private static String lastFingerprint = "";
    private static String lastBody = "";
    private static long lastForwardAt;

    @Override
    public void handleLoadPackage(final XC_LoadPackage.LoadPackageParam lpparam) throws Throwable {
        XposedBridge.log(TAG + ": module loaded in " + lpparam.packageName);
        if (!PHONE_PACKAGE.equals(lpparam.packageName)) {
            return;
        }

        Class<?> handler;
        try {
            handler = XposedHelpers.findClass(
                    "com.android.internal.telephony.InboundSmsHandler",
                    lpparam.classLoader);
        } catch (Throwable t) {
            XposedBridge.log(TAG + ": cannot find InboundSmsHandler: " + t);
            return;
        }

        try {
            XposedBridge.hookAllConstructors(handler, new XC_MethodHook() {
                @Override
                protected void afterHookedMethod(MethodHookParam param) {
                    if (param.args == null) {
                        return;
                    }
                    for (Object arg : param.args) {
                        if (arg instanceof Context) {
                            processContext = (Context) arg;
                            return;
                        }
                    }
                }
            });
        } catch (Throwable t) {
            XposedBridge.log(TAG + ": cannot hook InboundSmsHandler constructor: " + t);
        }

        try {
            Set<XC_MethodHook.Unhook> hooks = XposedBridge.hookAllMethods(
                    handler, "dispatchIntent", new XC_MethodHook() {
                        @Override
                        protected void beforeHookedMethod(MethodHookParam param) {
                            onDispatchIntent(param);
                        }
                    });
            XposedBridge.log(TAG + ": hooked InboundSmsHandler.dispatchIntent methods=" + hooks.size());
        } catch (Throwable t) {
            XposedBridge.log(TAG + ": cannot hook dispatchIntent: " + t);
        }

        try {
            Set<XC_MethodHook.Unhook> rawHooks = XposedBridge.hookAllMethods(
                    handler, "dispatchSmsDeliveryIntent", new XC_MethodHook() {
                        @Override
                        protected void beforeHookedMethod(MethodHookParam param) {
                            onRawDelivery(param);
                        }
                    });
            XposedBridge.log(TAG + ": hooked dispatchSmsDeliveryIntent methods=" + rawHooks.size());
        } catch (Throwable t) {
            XposedBridge.log(TAG + ": cannot hook dispatchSmsDeliveryIntent: " + t);
        }
    }

    private static void onDispatchIntent(XC_MethodHook.MethodHookParam param) {
        try {
            if (param.args == null || param.args.length == 0 || !(param.args[0] instanceof Intent)) {
                return;
            }
            final Intent intent = (Intent) param.args[0];
            final String action = intent.getAction();
            final SmsContent sms = readSmsContent(intent);
            final String body = sms.body;
            final String sender = sms.sender;
            if (body.length() == 0) {
                if (SMS_DELIVER_ACTION.equals(action)) {
                    XposedBridge.log(TAG + ": " + SMS_DELIVER_ACTION + " without readable body");
                }
                return;
            }
            submitSms(body, sender, action);
        } catch (Throwable t) {
            XposedBridge.log(TAG + ": SMS hook failed: " + t);
        }
    }

    private static void onRawDelivery(XC_MethodHook.MethodHookParam param) {
        try {
            if (param.args == null) return;
            byte[][] pdus = null;
            String format = null;
            for (Object arg : param.args) {
                if (arg instanceof byte[][]) pdus = (byte[][]) arg;
                else if (arg instanceof String && ("3gpp".equals(arg) || "3gpp2".equals(arg))) format = (String) arg;
            }
            if (pdus == null || pdus.length == 0) return;
            SmsContent sms = readSmsPdus(pdus, format);
            if (sms.body.length() > 0) submitSms(sms.body, sms.sender, "dispatchSmsDeliveryIntent");
        } catch (Throwable t) {
            XposedBridge.log(TAG + ": raw delivery read failed: " + t);
        }
    }

    private static void submitSms(final String body, final String sender, final String action) {
        if (body == null || body.length() == 0 || isDuplicate(body)) return;
        WORKER.submit(new Runnable() {
            @Override
            public void run() {
                try {
                    Config config = Config.load();
                    String code = CodeExtractor.extract(body, config.minLen, config.maxLen);
                    XposedBridge.log(TAG + ": sms action=" + action + " bodyLen=" + body.length()
                            + " codeLen=" + code.length() + " config[" + config.describe() + "]");
                    if (code.length() > 0) forward(config, body, sender);
                } catch (Throwable t) {
                    XposedBridge.log(TAG + ": sms worker failed: " + t);
                }
            }
        });
    }

    private static SmsContent readSmsPdus(byte[][] pdus, String format) {
        StringBuilder body = new StringBuilder();
        String sender = "";
        for (byte[] raw : pdus) {
            if (raw == null) continue;
            try {
                SmsMessage sms = format == null || format.length() == 0
                        ? SmsMessage.createFromPdu(raw) : SmsMessage.createFromPdu(raw, format);
                if (sms == null) continue;
                if (sender.length() == 0) sender = cleanSender(sms.getOriginatingAddress());
                String part = sms.getMessageBody();
                String display = sms.getDisplayMessageBody();
                if (display != null && (part == null || display.length() > part.length())) part = display;
                if (part != null) body.append(part);
            } catch (Throwable ignored) {
            }
        }
        return new SmsContent(body.toString(), sender);
    }

    private static SmsContent readSmsContent(Intent intent) {
        Bundle extras = intent.getExtras();
        if (extras == null) {
            return new SmsContent("", "");
        }

        Object rawPdus = extras.get("pdus");
        String format = extras.getString("format");
        StringBuilder body = new StringBuilder();
        String sender = "";

        // 先走 Android 官方解析器，厂商 InboundSmsHandler 通常会在这里保留完整长短信。
        try {
            SmsMessage[] messages = Telephony.Sms.Intents.getMessagesFromIntent(intent);
            if (messages != null) {
                for (SmsMessage sms : messages) {
                    if (sms == null) continue;
                    if (sender.length() == 0) sender = cleanSender(sms.getOriginatingAddress());
                    if (sms.getMessageBody() != null) body.append(sms.getMessageBody());
                }
            }
        } catch (Throwable ignored) {
            // 继续使用下面的 PDU 和 extra 回退，兼容旧 ROM。
        }

        String pduBody = "";
        if (rawPdus instanceof Object[]) {
            Object[] pdus = (Object[]) rawPdus;
            StringBuilder pduText = new StringBuilder();
            for (Object raw : pdus) {
                if (!(raw instanceof byte[])) {
                    continue;
                }
                try {
                    SmsMessage sms;
                    if (format == null || format.length() == 0) {
                        sms = SmsMessage.createFromPdu((byte[]) raw);
                    } else {
                        sms = SmsMessage.createFromPdu((byte[]) raw, format);
                    }
                    if (sms != null) {
                        if (sender.length() == 0) {
                            sender = cleanSender(sms.getOriginatingAddress());
                        }
                        if (sms.getMessageBody() != null) {
                            pduText.append(sms.getMessageBody());
                        }
                    }
                } catch (Throwable ignored) {
                    // A vendor-specific PDU must not break normal SMS delivery.
                }
            }
            pduBody = pduText.toString();
        }

        if (body.length() == 0 || pduBody.length() > body.length()) {
            body.setLength(0);
            body.append(pduBody);
        }

        String[] bodyKeys = {"message_body", "body", "sms_body", "message", "text"};
        for (String key : bodyKeys) {
            String candidate = extraText(extras, key);
            if (candidate != null && candidate.length() > body.length()) {
                body.setLength(0);
                body.append(candidate);
            }
        }
        if (sender.length() == 0) {
            sender = cleanSender(extraText(extras, "originating_address"));
        }
        if (sender.length() == 0) {
            sender = cleanSender(extraText(extras, "address"));
        }
        if (sender.length() == 0) {
            sender = cleanSender(extraText(extras, "sender"));
        }
        return new SmsContent(body.toString(), sender);
    }

    private static String extraText(Bundle extras, String key) {
        Object value = extras == null ? null : extras.get(key);
        return value instanceof CharSequence ? value.toString() : null;
    }

    private static String cleanSender(String value) {
        if (value == null) {
            return "";
        }
        return value.replace("\r", "").replace("\n", "").trim();
    }

    private static final class SmsContent {
        final String body;
        final String sender;

        SmsContent(String body, String sender) {
            this.body = body == null ? "" : body;
            this.sender = sender == null ? "" : sender;
        }
    }

    private static synchronized boolean isDuplicate(String body) {
        long now = System.currentTimeMillis();
        String fingerprint = Integer.toHexString(body.hashCode());
        boolean sameMessage = fingerprint.equals(lastFingerprint)
                || (lastBody.length() > 0
                && (lastBody.contains(body) || body.contains(lastBody)));
        if (sameMessage && now - lastForwardAt < 30000L) {
            return true;
        }
        lastFingerprint = fingerprint;
        lastBody = body;
        lastForwardAt = now;
        return false;
    }

    private static void forward(Config config, String text, String sender) {
        if (config.pcIp.length() == 0 && config.pc2Ip.length() == 0 && config.ntfyTopic.length() == 0) {
            XposedBridge.log(TAG + ": no target configured, pc/pc2 ip and ntfy topic are all empty");
            return;
        }
        if (config.pcIp.length() > 0) {
            // 电脑端令牌留空时不校验; 此时手机端同样可留空 (post 仅在令牌非空时携带 X-Token)。
            post("pc1", "http://" + config.pcIp + ":" + config.pcPort + "/sms", text, config.token, "", sender);
        }
        if (config.pc2Ip.length() > 0) {
            post("pc2", "http://" + config.pc2Ip + ":" + config.pc2Port + "/sms", text, config.pc2Token, "", sender);
        }
        if (config.ntfyTopic.length() > 0) {
            String server = trimTrailingSlash(config.ntfyServer);
            if (isHttpsEndpoint(server)) {
                post("ntfy", server + "/" + config.ntfyTopic, text, "", config.ntfyToken, sender);
            } else {
                XposedBridge.log(TAG + ": ntfy disabled because server is not HTTPS");
            }
        }
    }

    private static void post(String kind, String target, String body, String token) {
        post(kind, target, body, token, "", "");
    }

    private static void post(String kind, String target, String body, String token, String bearer) {
        post(kind, target, body, token, bearer, "");
    }

    private static void post(String kind, String target, String body, String token, String bearer, String sender) {
        HttpURLConnection connection = null;
        try {
            byte[] payload = body.getBytes(Charset.forName("UTF-8"));
            connection = (HttpURLConnection) new URL(target).openConnection();
            connection.setInstanceFollowRedirects(false);
            connection.setRequestMethod("POST");
            connection.setConnectTimeout(5000);
            connection.setReadTimeout(5000);
            connection.setDoOutput(true);
            connection.setRequestProperty("Content-Type", "text/plain; charset=utf-8");
            if (token.length() > 0) {
                connection.setRequestProperty("X-Token", token);
            }
            if (bearer.length() > 0) {
                connection.setRequestProperty("Authorization", "Bearer " + bearer);
            }
            if (sender != null && sender.length() > 0) {
                connection.setRequestProperty("X-SMS-Sender-B64",
                        Base64.encodeToString(sender.getBytes(Charset.forName("UTF-8")), Base64.NO_WRAP));
                if (isAscii(sender)) {
                    connection.setRequestProperty("X-SMS-Sender", sender);
                }
                if (kind.equals("ntfy")) {
                    connection.setRequestProperty("X-Title", sender);
                }
            }
            connection.setFixedLengthStreamingMode(payload.length);
            OutputStream output = connection.getOutputStream();
            output.write(payload);
            output.close();
            int httpCode = connection.getResponseCode();
            XposedBridge.log(TAG + ": forward " + kind + " http=" + httpCode);
        } catch (Throwable t) {
            XposedBridge.log(TAG + ": forward " + kind + " failed: " + t.getClass().getSimpleName());
        } finally {
            if (connection != null) {
                connection.disconnect();
            }
        }
    }

    private static boolean isAscii(String value) {
        for (int i = 0; i < value.length(); i++) {
            if (value.charAt(i) > 0x7f) {
                return false;
            }
        }
        return true;
    }

    private static String trimTrailingSlash(String value) {
        String result = value == null ? "" : value.trim();
        while (result.endsWith("/")) {
            result = result.substring(0, result.length() - 1);
        }
        return result;
    }

    private static boolean isHttpsEndpoint(String value) {
        try {
            URL url = new URL(value);
            return url.getProtocol().equalsIgnoreCase("https")
                    && url.getHost().length() > 0 && url.getUserInfo() == null;
        } catch (Throwable ignored) {
            return false;
        }
    }

    private static Context currentContext() {
        Context captured = processContext;
        if (captured != null) {
            return captured;
        }
        try {
            Object application = Class.forName("android.app.AndroidAppHelper")
                    .getMethod("currentApplication").invoke(null);
            if (application instanceof Context) {
                processContext = (Context) application;
                return (Context) application;
            }
        } catch (Throwable ignored) {
        }
        try {
            Object application = Class.forName("android.app.ActivityThread")
                    .getMethod("currentApplication").invoke(null);
            if (application instanceof Context) {
                processContext = (Context) application;
                return (Context) application;
            }
        } catch (Throwable ignored) {
        }
        return null;
    }

    private static final class Config {
        final String pcIp;
        final int pcPort;
        final String token;
        final String pc2Ip;
        final int pc2Port;
        final String pc2Token;
        final String ntfyServer;
        final String ntfyTopic;
        final String ntfyToken;
        final int minLen;
        final int maxLen;
        final String source;

        Config(Map<String, String> values, String source) {
            this.source = source;
            pcIp = value(values, "PC_IP", "");
            pcPort = boundedInt(value(values, "PC_PORT", "8787"), 8787, 1, 65535);
            token = value(values, "TOKEN", "");
            pc2Ip = value(values, "PC2_IP", "");
            pc2Port = boundedInt(value(values, "PC2_PORT", "8787"), 8787, 1, 65535);
            pc2Token = value(values, "PC2_TOKEN", "");
            ntfyServer = value(values, "NTFY_SERVER", "https://ntfy.sh");
            ntfyTopic = value(values, "NTFY_TOPIC", "");
            ntfyToken = value(values, "NTFY_TOKEN", "");
            minLen = boundedInt(value(values, "MIN_LEN", "4"), 4, 1, 32);
            maxLen = boundedInt(value(values, "MAX_LEN", "8"), Math.max(8, minLen), minLen, 32);
        }

        static Config load() {
            Map<String, String> provider = loadFromProvider();
            if (provider != null) {
                return new Config(provider, "provider");
            }
            return new Config(new HashMap<String, String>(), "none");
        }

        private static Map<String, String> loadFromProvider() {
            if (System.currentTimeMillis() < providerRetryAt) {
                return null;
            }
            try {
                Context context = currentContext();
                if (context == null) {
                    providerRetryAt = System.currentTimeMillis() + 60000L;
                    XposedBridge.log(TAG + ": provider skipped, context not ready");
                    return null;
                }
                Bundle bundle = context.getContentResolver().call(
                        Uri.parse("content://" + ConfigProvider.AUTHORITY),
                        ConfigProvider.METHOD_VALUES, null, null);
                if (bundle == null) {
                    providerRetryAt = System.currentTimeMillis() + 60000L;
                    XposedBridge.log(TAG + ": provider returned no data");
                    return null;
                }
                Map<String, String> values = new HashMap<String, String>();
                for (String key : CONFIG_KEYS) {
                    String value = bundle.getString(key);
                    if (value != null) {
                        values.put(key, value);
                    }
                }
                providerRetryAt = 0L;
                return values.isEmpty() ? null : values;
            } catch (Throwable t) {
                providerRetryAt = System.currentTimeMillis() + 60000L;
                XposedBridge.log(TAG + ": provider read failed: " + t);
                return null;
            }
        }

        String describe() {
            return "source=" + source + " pc=" + (pcIp.length() > 0) + " port=" + pcPort
                    + " token=" + (token.length() > 0) + " pc2=" + (pc2Ip.length() > 0)
                    + " ntfy=" + (ntfyTopic.length() > 0)
                    + " min=" + minLen + " max=" + maxLen;
        }

        private static String value(Map<String, String> values, String key, String fallback) {
            String result = values.get(key);
            return result == null ? fallback : result.trim();
        }

        private static int boundedInt(String text, int fallback, int minimum, int maximum) {
            try {
                int result = Integer.parseInt(text);
                return Math.max(minimum, Math.min(maximum, result));
            } catch (Throwable ignored) {
                return fallback;
            }
        }
    }
}
