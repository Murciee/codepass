package com.codepass.lsposed;

import android.app.Activity;
import android.content.BroadcastReceiver;
import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.SharedPreferences;
import android.content.res.ColorStateList;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.graphics.drawable.RippleDrawable;
import android.graphics.drawable.StateListDrawable;
import android.hardware.display.DisplayManager;
import android.os.Bundle;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.os.PowerManager;
import android.text.InputType;
import android.view.Gravity;
import android.view.Display;
import android.view.View;
import android.view.WindowManager;
import android.widget.Button;
import android.widget.EditText;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.SocketTimeoutException;
import java.net.URL;
import java.nio.charset.Charset;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public final class MainActivity extends Activity {
    private static final int PAGE_BACKGROUND = Color.rgb(244, 246, 251);
    private static final int TEXT = Color.rgb(37, 48, 71);
    private static final int MUTED = Color.rgb(103, 116, 139);
    private static final int BORDER = Color.rgb(220, 227, 238);
    private static final int ACCENT = Color.rgb(79, 112, 181);
    private static final int INPUT_BACKGROUND = Color.rgb(248, 250, 253);
    // 配置键由 ConfigProvider 统一定义（该文件同时供 Flutter 版共享构建编译）。
    static final String[] CONFIG_KEYS = ConfigProvider.CONFIG_KEYS;
    // 兜底解析：中转工具把多行配置折叠成一行或出现额外文本时，仍能按 KEY="value" 提取（键名与 CONFIG_KEYS 同步）。
    private static final Pattern CONFIG_LINE_PATTERN = Pattern.compile(
            "(PC_IP|PC_PORT|TOKEN|PC2_IP|PC2_PORT|PC2_TOKEN|NTFY_SERVER|NTFY_TOPIC|NTFY_TOKEN|MIN_LEN|MAX_LEN)\\s*=\\s*\"((?:\\\\.|[^\"\\\\])*)\"");
    private final Map<String, EditText> fields = new HashMap<String, EditText>();
    // 跨 Activity 重建保持串行，旧页面保存完成后新页面才读取配置。
    private static final ExecutorService CONFIG_WORKER = Executors.newSingleThreadExecutor();
    private TextView status;
    private Button saveButton;
    private Button importButton;
    private Button testButton;
    private EditText testSmsInput;
    private TextView testResult;
    private DisplayManager displayManager;
    private boolean powerReceiverRegistered;
    private final BroadcastReceiver powerSaveReceiver = new BroadcastReceiver() {
        @Override public void onReceive(Context context, Intent intent) { adaptRefreshRate(); }
    };
    private final DisplayManager.DisplayListener displayListener = new DisplayManager.DisplayListener() {
        @Override public void onDisplayAdded(int id) { }
        @Override public void onDisplayRemoved(int id) { }
        @Override public void onDisplayChanged(int id) { adaptRefreshRate(); }
    };

    @Override
    protected void onCreate(Bundle state) {
        super.onCreate(state);

        getWindow().setStatusBarColor(PAGE_BACKGROUND);
        getWindow().setNavigationBarColor(PAGE_BACKGROUND);
        int systemUi = View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR;
        if (Build.VERSION.SDK_INT >= 26) {
            systemUi |= View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR;
        }
        getWindow().getDecorView().setSystemUiVisibility(systemUi);
        displayManager = (DisplayManager) getSystemService(Context.DISPLAY_SERVICE);

        LinearLayout content = new LinearLayout(this);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setBackgroundColor(PAGE_BACKGROUND);
        content.setPadding(dp(18), dp(22), dp(18), dp(32));

        LinearLayout header = card();
        header.setPadding(dp(18), dp(16), dp(18), dp(16));
        header.setBackground(roundRect(Color.rgb(237, 242, 252), BORDER, dp(22)));
        LinearLayout brand = new LinearLayout(this);
        brand.setGravity(Gravity.CENTER_VERTICAL);
        ImageView logo = new ImageView(this);
        logo.setImageResource(R.drawable.ic_codepass);
        brand.addView(logo, new LinearLayout.LayoutParams(dp(48), dp(48)));
        LinearLayout titles = new LinearLayout(this);
        titles.setOrientation(LinearLayout.VERTICAL);
        LinearLayout.LayoutParams titleParams = new LinearLayout.LayoutParams(-1, -2);
        titleParams.leftMargin = dp(12);
        brand.addView(titles, titleParams);
        TextView title = text("codepass", 24, TEXT);
        title.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        titles.addView(title, fullWidth());

        TextView subtitle = text("短信验证码转发设置", 14, MUTED);
        titles.addView(subtitle, fullWidthWithTop(4));
        header.addView(brand, fullWidth());
        TextView hint = text("目标作用域：com.android.phone（建议把本应用 codepass 也勾入作用域，作为备用读取通道）。\n保存后即时生效，无需重启电话进程或手机。", 12.5f, MUTED);
        hint.setLineSpacing(0, 1.15f);
        header.addView(hint, fullWidthWithTop(12));
        content.addView(header, fullWidth());

        LinearLayout importer = section(content, "从电脑导入配置");
        importButton = new Button(this);
        importButton.setText("读取电脑复制的配置并输入");
        styleButton(importButton, false);
        importButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View view) {
                importClipboardConfig();
            }
        });
        LinearLayout.LayoutParams importParams = fullWidthWithTop(12);
        importParams.height = dp(50);
        importer.addView(importButton, importParams);
        TextView importHint = text("先在电脑端点击“复制手机端配置”，把复制内容传到手机剪贴板（聊天工具、剪贴板同步均可），再点上面的按钮；只填入表单，不会自动保存。", 12.5f, MUTED);
        importHint.setLineSpacing(0, 1.15f);
        importer.addView(importHint, fullWidthWithTop(10));

        LinearLayout connection = section(content, "电脑端接收");
        addField(connection, "电脑 IPv4 地址", "PC_IP", "例如 192.168.1.100");
        addField(connection, "监听端口", "PC_PORT", "8787");
        addField(connection, "局域网令牌", "TOKEN", "可留空");

        LinearLayout connection2 = section(content, "电脑端接收（第二台，可选）");
        addField(connection2, "电脑 2 IPv4 地址", "PC2_IP", "留空表示不启用");
        addField(connection2, "监听端口", "PC2_PORT", "8787");
        addField(connection2, "局域网令牌", "PC2_TOKEN", "可留空");

        LinearLayout publicChannel = section(content, "公网通知服务（可选）");
        addField(publicChannel, "ntfy 服务器", "NTFY_SERVER", "https://ntfy.sh");
        addField(publicChannel, "ntfy 主题", "NTFY_TOPIC", "长随机主题名");
        addField(publicChannel, "ntfy 访问令牌", "NTFY_TOKEN", "私有服务器使用");

        LinearLayout recognition = section(content, "验证码识别");
        addField(recognition, "最短验证码长度", "MIN_LEN", "4");
        addField(recognition, "最长验证码长度", "MAX_LEN", "8");

        LinearLayout tester = section(content, "模拟短信测试");
        TextView testLabel = text("模拟短信内容", 13, MUTED);
        tester.addView(testLabel, fullWidthWithTop(12));
        testSmsInput = new EditText(this);
        testSmsInput.setText("您的验证码是 123456，5 分钟内有效。");
        testSmsInput.setTextSize(14);
        testSmsInput.setTextColor(TEXT);
        testSmsInput.setHintTextColor(MUTED);
        testSmsInput.setHint("模拟收到的短信正文");
        testSmsInput.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_FLAG_MULTI_LINE);
        testSmsInput.setMinLines(2);
        testSmsInput.setGravity(Gravity.TOP | Gravity.START);
        testSmsInput.setPadding(dp(12), dp(10), dp(12), dp(10));
        StateListDrawable testBackground = new StateListDrawable();
        testBackground.addState(new int[]{android.R.attr.state_focused}, roundRect(Color.WHITE, ACCENT, dp(12)));
        testBackground.addState(new int[]{}, roundRect(INPUT_BACKGROUND, BORDER, dp(12)));
        testSmsInput.setBackground(testBackground);
        tester.addView(testSmsInput, fullWidthWithTop(6));

        testButton = new Button(this);
        testButton.setText("模拟接收短信并发送");
        styleButton(testButton, false);
        testButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View view) {
                runSmsTest();
            }
        });
        LinearLayout.LayoutParams testParams = fullWidthWithTop(12);
        testParams.height = dp(50);
        tester.addView(testButton, testParams);

        testResult = text("使用当前页面填写的地址、端口和令牌发送（无需先保存）；发送内容与真实短信一致，发送的是短信原文，验证码由电脑端识别。", 12.5f, MUTED);
        testResult.setPadding(dp(12), dp(10), dp(12), dp(10));
        testResult.setBackground(roundRect(Color.WHITE, BORDER, dp(12)));
        testResult.setMinHeight(dp(44));
        testResult.setLineSpacing(0, 1.15f);
        tester.addView(testResult, fullWidthWithTop(10));

        Button save = new Button(this);
        saveButton = save;
        save.setText("保存配置");
        styleButton(save, true);
        save.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View view) {
                saveConfig();
            }
        });
        LinearLayout.LayoutParams saveParams = fullWidthWithTop(18);
        saveParams.height = dp(50);
        content.addView(save, saveParams);

        status = text("正在读取配置…", 13, MUTED);
        status.setGravity(Gravity.CENTER_VERTICAL);
        status.setPadding(dp(14), dp(10), dp(14), dp(10));
        status.setMinHeight(dp(44));
        status.setBackground(roundRect(Color.WHITE, BORDER, dp(14)));
        status.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);
        content.addView(status, fullWidthWithTop(10));

        ScrollView scroll = new ScrollView(this);
        scroll.setFillViewport(true);
        scroll.setBackgroundColor(PAGE_BACKGROUND);
        scroll.setClipToPadding(false);
        scroll.addView(content);
        setContentView(scroll);
        save.setEnabled(false);
        importButton.setEnabled(false);
        testButton.setEnabled(false);
        for (EditText field : fields.values()) field.setEnabled(false);
        CONFIG_WORKER.execute(new Runnable() {
            @Override public void run() {
                final Map<String, String> values = readConfig();
                runOnUiThread(new Runnable() {
                    @Override public void run() {
                        if (isFinishing() || isDestroyed()) return;
                        fill(values);
                        for (EditText field : fields.values()) field.setEnabled(true);
                        saveButton.setEnabled(true);
                        importButton.setEnabled(true);
                        testButton.setEnabled(true);
                        status.setText("填写连接信息后保存；只使用局域网时可留空 ntfy 主题。");
                    }
                });
            }
        });
    }

    @Override
    protected void onResume() {
        super.onResume();
        if (displayManager != null) {
            displayManager.registerDisplayListener(displayListener, new Handler(Looper.getMainLooper()));
        }
        IntentFilter filter = new IntentFilter(PowerManager.ACTION_POWER_SAVE_MODE_CHANGED);
        if (Build.VERSION.SDK_INT >= 33) {
            registerReceiver(powerSaveReceiver, filter, Context.RECEIVER_NOT_EXPORTED);
        } else {
            registerReceiver(powerSaveReceiver, filter);
        }
        powerReceiverRegistered = true;
        adaptRefreshRate();
    }

    @Override
    protected void onPause() {
        if (displayManager != null) displayManager.unregisterDisplayListener(displayListener);
        if (powerReceiverRegistered) {
            unregisterReceiver(powerSaveReceiver);
            powerReceiverRegistered = false;
        }
        WindowManager.LayoutParams attributes = getWindow().getAttributes();
        attributes.preferredDisplayModeId = 0;
        attributes.preferredRefreshRate = 0;
        getWindow().setAttributes(attributes);
        super.onPause();
    }

    private void adaptRefreshRate() {
        Display display = getWindowManager().getDefaultDisplay();
        if (display == null) return;
        PowerManager power = (PowerManager) getSystemService(Context.POWER_SERVICE);
        int modeId = 0;
        float refreshRate = 0;
        if (power == null || !power.isPowerSaveMode()) {
            Display.Mode current = display.getMode();
            for (Display.Mode mode : display.getSupportedModes()) {
                if (mode.getPhysicalWidth() == current.getPhysicalWidth()
                        && mode.getPhysicalHeight() == current.getPhysicalHeight()
                        && mode.getRefreshRate() > refreshRate) {
                    modeId = mode.getModeId();
                    refreshRate = mode.getRefreshRate();
                }
            }
        }
        WindowManager.LayoutParams attributes = getWindow().getAttributes();
        if (attributes.preferredDisplayModeId != modeId || attributes.preferredRefreshRate != refreshRate) {
            attributes.preferredDisplayModeId = modeId;
            attributes.preferredRefreshRate = refreshRate;
            getWindow().setAttributes(attributes);
        }
    }

    private LinearLayout section(LinearLayout parent, String title) {
        LinearLayout panel = card();
        TextView heading = text(title, 15, TEXT);
        heading.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        panel.addView(heading, fullWidth());
        parent.addView(panel, fullWidthWithTop(14));
        return panel;
    }

    private LinearLayout card() {
        LinearLayout panel = new LinearLayout(this);
        panel.setOrientation(LinearLayout.VERTICAL);
        panel.setBackground(roundRect(Color.WHITE, Color.rgb(227, 232, 242), dp(20)));
        panel.setPadding(dp(18), dp(18), dp(18), dp(20));
        return panel;
    }

    private void addField(LinearLayout parent, String label, String key, String hint) {
        TextView labelView = text(label, 13, MUTED);
        parent.addView(labelView, fullWidthWithTop(12));

        EditText input = new EditText(this);
        input.setSingleLine(true);
        input.setHint(hint);
        input.setTextSize(14);
        input.setTextColor(TEXT);
        input.setHintTextColor(MUTED);
        input.setPadding(dp(12), 0, dp(12), 0);
        StateListDrawable background = new StateListDrawable();
        background.addState(new int[]{android.R.attr.state_focused}, roundRect(Color.WHITE, ACCENT, dp(12)));
        background.addState(new int[]{}, roundRect(INPUT_BACKGROUND, BORDER, dp(12)));
        input.setBackground(background);
        if ("TOKEN".equals(key) || "PC2_TOKEN".equals(key) || "NTFY_TOKEN".equals(key)) {
            input.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_VARIATION_PASSWORD);
        }
        if ("PC_PORT".equals(key) || "PC2_PORT".equals(key) || "MIN_LEN".equals(key) || "MAX_LEN".equals(key)) {
            input.setInputType(InputType.TYPE_CLASS_NUMBER);
        }
        fields.put(key, input);
        LinearLayout.LayoutParams params = fullWidthWithTop(6);
        params.height = dp(48);
        parent.addView(input, params);
    }

    private TextView text(String value, float size, int color) {
        TextView view = new TextView(this);
        view.setText(value);
        view.setTextSize(size);
        view.setTextColor(color);
        return view;
    }

    private void styleButton(Button button, boolean primary) {
        button.setAllCaps(false);
        button.setTextSize(14);
        button.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        button.setTextColor(primary ? Color.WHITE : TEXT);
        button.setGravity(Gravity.CENTER);
        button.setMinHeight(0);
        button.setMinWidth(0);
        button.setPadding(dp(18), 0, dp(18), 0);
        button.setElevation(0);
        button.setStateListAnimator(null);
        GradientDrawable face = roundRect(primary ? ACCENT : Color.WHITE, primary ? ACCENT : BORDER, dp(14));
        RippleDrawable ripple = new RippleDrawable(ColorStateList.valueOf(primary ? 0x33FFFFFF : 0x224F70B5),
                face, roundRect(Color.WHITE, Color.WHITE, dp(14)));
        button.setBackground(ripple);
        button.setBackgroundTintList(null);
        button.setTextColor(new ColorStateList(new int[][]{{-android.R.attr.state_enabled}, {}},
                new int[]{MUTED, primary ? Color.WHITE : TEXT}));
    }

    private GradientDrawable roundRect(int fill, int stroke, int radius) {
        GradientDrawable drawable = new GradientDrawable();
        drawable.setColor(fill);
        drawable.setCornerRadius(radius);
        drawable.setStroke(dp(1), stroke);
        return drawable;
    }

    private void fill(Map<String, String> values) {
        String[] defaults = {"", "8787", "", "", "8787", "", "https://ntfy.sh", "", "", "4", "8"};
        for (int i = 0; i < CONFIG_KEYS.length; i++) {
            String value = values.get(CONFIG_KEYS[i]);
            fields.get(CONFIG_KEYS[i]).setText(value == null ? defaults[i] : value);
        }
    }

    private Map<String, String> readConfig() {
        Map<String, String> values = new HashMap<String, String>();
        try {
            SharedPreferences prefs = ConfigProvider.appPrefs(this);
            for (String key : CONFIG_KEYS) {
                String value = prefs.getString(key, null);
                if (value != null) {
                    values.put(key, value);
                }
            }
        } catch (Throwable ignored) {
        }
        return values;
    }

    private void saveConfig() {
        String port = fields.get("PC_PORT").getText().toString().trim();
        try {
            int parsedPort = Integer.parseInt(port);
            if (parsedPort < 1 || parsedPort > 65535) {
                status.setText("保存失败：端口必须是 1～65535。");
                return;
            }
        } catch (NumberFormatException e) {
            status.setText("保存失败：端口必须是数字。");
            return;
        }
        String port2 = fields.get("PC2_PORT").getText().toString().trim();
        if (port2.length() > 0) {
            try {
                int parsedPort2 = Integer.parseInt(port2);
                if (parsedPort2 < 1 || parsedPort2 > 65535) {
                    status.setText("保存失败：第二台电脑端口必须是 1～65535。");
                    return;
                }
            } catch (NumberFormatException e) {
                status.setText("保存失败：第二台电脑端口必须是数字。");
                return;
            }
        }
        final Map<String, String> snapshot = new HashMap<String, String>();
        for (String key : CONFIG_KEYS) {
            snapshot.put(key, fields.get(key).getText().toString().trim());
        }
        saveButton.setEnabled(false);
        status.setText("正在保存…");
        CONFIG_WORKER.execute(new Runnable() {
            @Override public void run() {
                String message;
                try {
                    // appPrefs 首次获取时尝试 MODE_WORLD_READABLE（LSPosed 勾选自身作用域后放行），
                    // 失败回退私有模式；两种模式下电话进程都可通过 ConfigProvider 读取。
                    SharedPreferences prefs = ConfigProvider.appPrefs(MainActivity.this);
                    SharedPreferences.Editor editor = prefs.edit();
                    for (String key : CONFIG_KEYS) {
                        editor.putString(key, snapshot.get(key));
                    }
                    message = editor.commit()
                            ? "配置已保存并即时生效。"
                            : "保存失败：写入存储失败，请重试。";
                } catch (Throwable t) {
                    message = "保存失败：" + t;
                }
                final String result = message;
                runOnUiThread(new Runnable() {
                    @Override public void run() {
                        if (isFinishing() || isDestroyed()) return;
                        saveButton.setEnabled(true);
                        status.setText(result);
                    }
                });
            }
        });
    }

    private void importClipboardConfig() {
        ClipboardManager manager = (ClipboardManager) getSystemService(Context.CLIPBOARD_SERVICE);
        String clipText = null;
        try {
            if (manager != null && manager.hasPrimaryClip()) {
                ClipData clip = manager.getPrimaryClip();
                if (clip != null && clip.getItemCount() > 0) {
                    CharSequence value = clip.getItemAt(0).coerceToText(this);
                    if (value != null) {
                        clipText = value.toString();
                    }
                }
            }
        } catch (Throwable ignored) {
        }
        if (clipText == null || clipText.trim().length() == 0) {
            Toast.makeText(this, "剪贴板为空：先在电脑端复制配置，再把内容传到手机剪贴板。", Toast.LENGTH_LONG).show();
            return;
        }
        Map<String, String> values = parseConfigText(clipText);
        if (values.isEmpty()) {
            Toast.makeText(this, "剪贴板里没有找到 PC_IP=、PC_PORT= 等配置行。", Toast.LENGTH_LONG).show();
            return;
        }
        for (Map.Entry<String, String> entry : values.entrySet()) {
            EditText field = fields.get(entry.getKey());
            if (field != null) {
                field.setText(entry.getValue());
            }
        }
        StringBuilder missing = new StringBuilder();
        for (String key : CONFIG_KEYS) {
            if (key.startsWith("PC2_")) {
                // 第二台电脑为可选项，导入旧配置时不计入“未找到”。
                continue;
            }
            if (!values.containsKey(key)) {
                if (missing.length() > 0) {
                    missing.append("、");
                }
                missing.append(key);
            }
        }
        String detail = missing.length() == 0 ? "" : "；未找到 " + missing;
        status.setText("已从剪贴板导入 " + values.size() + " 项配置" + detail + "，请核对后点击“保存配置”。");
        Toast.makeText(this, "已导入 " + values.size() + " 项配置" + detail + "。", Toast.LENGTH_LONG).show();
    }

    /** 解析电脑端“复制手机端配置”生成的 KEY="value" 文本，只接受已知配置项。 */
    private Map<String, String> parseConfigText(String text) {
        Map<String, String> values = new HashMap<String, String>();
        for (String rawLine : text.split("\n")) {
            String line = rawLine.trim();
            if (line.length() == 0 || line.startsWith("#")) {
                continue;
            }
            int split = line.indexOf('=');
            if (split <= 0) {
                continue;
            }
            String key = line.substring(0, split).trim();
            if (!isConfigKey(key)) {
                continue;
            }
            String value = line.substring(split + 1).trim();
            if (value.length() >= 2 && value.startsWith("\"") && value.endsWith("\"")) {
                value = value.substring(1, value.length() - 1);
            }
            values.put(key, unescape(value));
        }
        if (values.size() < CONFIG_KEYS.length) {
            Matcher matcher = CONFIG_LINE_PATTERN.matcher(text);
            while (matcher.find()) {
                values.put(matcher.group(1), unescape(matcher.group(2)));
            }
        }
        return values;
    }

    private static boolean isConfigKey(String key) {
        for (String known : CONFIG_KEYS) {
            if (known.equals(key)) {
                return true;
            }
        }
        return false;
    }

    /** 模拟收到一条短信：先按本地规则检测验证码，再按与 hook 相同的规则发送短信原文，结果显示在测试结果栏。 */
    private void runSmsTest() {
        String body = testSmsInput.getText().toString().trim();
        if (body.length() == 0) {
            testResult.setText("请先填写模拟短信内容。");
            return;
        }
        int minLen = boundedInt(fields.get("MIN_LEN").getText().toString(), 4, 1, 32);
        int maxLen = boundedInt(fields.get("MAX_LEN").getText().toString(), 8, 1, 32);
        final String code = CodeExtractor.extract(body, minLen, maxLen);
        if (code.length() == 0) {
            testResult.setText("未识别出验证码（当前设置 " + minLen + "～" + maxLen + " 位）：请检查模拟短信内容或验证码长度。");
            return;
        }

        final String pcIp = fields.get("PC_IP").getText().toString().trim();
        final String token = fields.get("TOKEN").getText().toString().trim();
        final String pc2Ip = fields.get("PC2_IP").getText().toString().trim();
        final String pc2Token = fields.get("PC2_TOKEN").getText().toString().trim();
        final String ntfyServer = fields.get("NTFY_SERVER").getText().toString().trim();
        final String ntfyTopic = fields.get("NTFY_TOPIC").getText().toString().trim();
        final String ntfyToken = fields.get("NTFY_TOKEN").getText().toString().trim();
        final boolean sendPc = pcIp.length() > 0;
        final boolean sendPc2 = pc2Ip.length() > 0;
        final boolean sendNtfy = ntfyTopic.length() > 0;
        if (!sendPc && !sendPc2 && !sendNtfy) {
            testResult.setText("未发送：请填写电脑 IPv4 地址（或 ntfy 主题）。");
            return;
        }
        final int port = sendPc ? parsePort(fields.get("PC_PORT").getText().toString()) : -1;
        final int port2 = sendPc2 ? parsePort(fields.get("PC2_PORT").getText().toString()) : -1;

        testButton.setEnabled(false);
        testButton.setText("发送中…");
        testResult.setText("已识别验证码 " + code + "，正在发送短信原文…");
        CONFIG_WORKER.execute(new Runnable() {
            @Override
            public void run() {
                StringBuilder report = new StringBuilder();
                report.append("识别验证码：").append(code).append('\n');
                boolean delivered = false;
                if (sendPc) {
                    String result = postOnce("http://" + pcIp + ":" + port + "/sms", body, token, "", false);
                    report.append("电脑端：").append(result).append('\n');
                    delivered = delivered || result.contains("已收到");
                } else {
                    report.append("电脑端：未填写地址\n");
                }
                if (sendPc2) {
                    String result = postOnce("http://" + pc2Ip + ":" + port2 + "/sms", body, pc2Token, "", false);
                    report.append("电脑端（第二台）：").append(result).append('\n');
                    delivered = delivered || result.contains("已收到");
                }
                if (sendNtfy && ntfyServer.length() == 0) {
                    report.append("ntfy：未发送（服务器地址为空）\n");
                } else if (sendNtfy) {
                    String result = postOnce(trimTrailingSlash(ntfyServer) + "/" + ntfyTopic, body, "", ntfyToken, true);
                    report.append("ntfy：").append(result).append('\n');
                    delivered = delivered || result.contains("已发布");
                }
                if (delivered) {
                    report.append("请在电脑端剪贴板或“记录”页确认；本测试使用页面当前填写值，真实转发前请先保存。");
                }
                final String message = report.toString().trim();
                runOnUiThread(new Runnable() {
                    @Override
                    public void run() {
                        if (isFinishing() || isDestroyed()) return;
                        testButton.setEnabled(true);
                        testButton.setText("模拟接收短信并发送");
                        testResult.setText(message);
                    }
                });
            }
        });
    }

    /** 发送一次并返回可读结果；超时、拒收与连接失败都转为中文说明。 */
    private static String postOnce(String target, String body, String token, String bearer, boolean ntfy) {
        HttpURLConnection connection = null;
        try {
            byte[] payload = body.getBytes(Charset.forName("UTF-8"));
            connection = (HttpURLConnection) new URL(target).openConnection();
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
            connection.setFixedLengthStreamingMode(payload.length);
            OutputStream output = connection.getOutputStream();
            output.write(payload);
            output.close();
            int status = connection.getResponseCode();
            if (status == 200) {
                return ntfy ? "已发布（HTTP 200）" : "已收到（HTTP 200）";
            }
            if (status == 403 && !ntfy) {
                return "令牌不一致或未通过校验（HTTP 403）";
            }
            return "HTTP " + status;
        } catch (SocketTimeoutException e) {
            return "连接超时（5 秒内无响应）";
        } catch (Throwable t) {
            return "连接失败（" + t.getClass().getSimpleName() + "）";
        } finally {
            if (connection != null) {
                connection.disconnect();
            }
        }
    }

    private static String trimTrailingSlash(String value) {
        String result = value == null ? "" : value.trim();
        while (result.endsWith("/")) {
            result = result.substring(0, result.length() - 1);
        }
        return result;
    }

    /** 测试通道按与 hook 相同的规则解析端口：空或非法回退 8787，越界夹取到 1～65535。 */
    private static int parsePort(String text) {
        return boundedInt(text, 8787, 1, 65535);
    }

    private static int boundedInt(String text, int fallback, int minimum, int maximum) {
        try {
            int value = Integer.parseInt(text.trim());
            return Math.max(minimum, Math.min(maximum, value));
        } catch (Throwable ignored) {
            return fallback;
        }
    }

    private static String unescape(String value) {
        StringBuilder result = new StringBuilder();
        boolean escaped = false;
        for (int i = 0; i < value.length(); i++) {
            char current = value.charAt(i);
            if (escaped) {
                if (current == '\\' || current == '"' || current == '$' || current == '`') {
                    result.append(current);
                } else {
                    result.append('\\').append(current);
                }
                escaped = false;
            } else if (current == '\\') {
                escaped = true;
            } else {
                result.append(current);
            }
        }
        if (escaped) {
            result.append('\\');
        }
        return result.toString();
    }

    private int dp(int value) {
        return (int) (value * getResources().getDisplayMetrics().density + 0.5f);
    }

    private LinearLayout.LayoutParams fullWidth() {
        return new LinearLayout.LayoutParams(-1, -2);
    }

    private LinearLayout.LayoutParams fullWidthWithTop(int top) {
        LinearLayout.LayoutParams params = fullWidth();
        params.topMargin = dp(top);
        return params;
    }
}
