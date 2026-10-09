package com.codepass.lsposed;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * 短信验证码提取规则。
 * hook（com.android.phone 进程）与设置页“模拟短信测试”共用，保证测试结果与真实转发使用同一套识别逻辑。
 */
public final class CodeExtractor {
    private CodeExtractor() {
    }

    public static String extract(String text, int minLen, int maxLen) {
        int min = Math.max(1, Math.min(32, minLen));
        int max = Math.max(1, Math.min(32, maxLen));
        if (max < min) {
            max = min;
        }

        String bounds = "[0-9]{" + min + "," + max + "}";
        Pattern keyword = Pattern.compile(
                "(?i)(验证码|校验码|动态码|动态密码|verification\\s*code|code|otp)"
                        + "[^0-9]{0,12}(" + bounds + ")");
        Matcher first = keyword.matcher(text);
        if (first.find()) {
            return first.group(2);
        }

        Matcher fallback = Pattern.compile("(^|[^0-9])(" + bounds + ")([^0-9]|$)").matcher(text);
        return fallback.find() ? fallback.group(2) : "";
    }
}
