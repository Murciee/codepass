package com.codepass.lsposed;

import android.content.ContentProvider;
import android.content.ContentValues;
import android.content.Context;
import android.content.SharedPreferences;
import android.database.Cursor;
import android.net.Uri;
import android.os.Binder;
import android.os.Bundle;
import android.os.Process;

/**
 * 只读配置通道：电话进程（radio, uid 1001）在 hook 触发时通过 content:// 调用读取
 * 本应用保存的配置。读取来源不依赖把本应用勾入 LSPosed 作用域。为防止配置外泄，
 * 只有系统进程、电话进程与自身进程能取到具体数值；ping 仅返回可达性，不暴露状态。
 */
public final class ConfigProvider extends ContentProvider {
    public static final String AUTHORITY = "com.codepass.lsposed.config";
    public static final String PREFS_NAME = "codepass";
    public static final String METHOD_VALUES = "values";
    public static final String METHOD_PING = "ping";
    // 与 CodepassModule.CONFIG_KEYS 保持一致（跨进程各自持有副本，修改时需同步）。
    static final String[] CONFIG_KEYS = {"PC_IP", "PC_PORT", "TOKEN", "PC2_IP", "PC2_PORT", "PC2_TOKEN", "NTFY_SERVER", "NTFY_TOPIC", "NTFY_TOKEN", "MIN_LEN", "MAX_LEN"};
    private static final int UID_SYSTEM = 1000;
    private static final int UID_PHONE = 1001;

    @Override
    public boolean onCreate() {
        return true;
    }

    @Override
    public Bundle call(String method, String arg, Bundle extras) {
        if (METHOD_PING.equals(method)) {
            // 任何人可调用，仅用于确认 Provider 可达；不返回配置状态。
            Bundle ping = new Bundle();
            ping.putBoolean("ok", true);
            return ping;
        }
        if (METHOD_VALUES.equals(method)) {
            int uid = Binder.getCallingUid();
            if (uid != UID_SYSTEM && uid != UID_PHONE && uid != Process.myUid()) {
                throw new SecurityException("config is not accessible to uid " + uid);
            }
            SharedPreferences prefs = prefs();
            Bundle values = new Bundle();
            for (String key : CONFIG_KEYS) {
                values.putString(key, prefs.getString(key, null));
            }
            return values;
        }
        return null;
    }

    private SharedPreferences prefs() {
        return appPrefs(getContext());
    }

    private static volatile SharedPreferences sharedPrefs;

    /** 仅使用私有偏好文件；电话进程通过受 UID 限制的 Provider 读取。 */
    public static SharedPreferences appPrefs(Context context) {
        SharedPreferences result = sharedPrefs;
        if (result == null) {
            synchronized (ConfigProvider.class) {
                result = sharedPrefs;
                if (result == null) {
                    result = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
                    // 升级时收紧旧版本可能留下的 world-readable 文件权限。
                    java.io.File file = new java.io.File(context.getApplicationInfo().dataDir,
                            "shared_prefs/" + PREFS_NAME + ".xml");
                    if (file.exists()) {
                        try {
                            android.system.Os.chmod(file.getAbsolutePath(), 0600);
                        } catch (android.system.ErrnoException e) {
                            throw new IllegalStateException("Cannot protect configuration file", e);
                        }
                    }
                    sharedPrefs = result;
                }
            }
        }
        return result;
    }

    @Override
    public Cursor query(Uri uri, String[] projection, String selection, String[] selectionArgs, String sortOrder) {
        throw new UnsupportedOperationException("ConfigProvider is read-only and only supports call()");
    }

    @Override
    public String getType(Uri uri) {
        return null;
    }

    @Override
    public Uri insert(Uri uri, ContentValues values) {
        throw new UnsupportedOperationException("ConfigProvider is read-only");
    }

    @Override
    public int delete(Uri uri, String selection, String[] selectionArgs) {
        throw new UnsupportedOperationException("ConfigProvider is read-only");
    }

    @Override
    public int update(Uri uri, ContentValues values, String selection, String[] selectionArgs) {
        throw new UnsupportedOperationException("ConfigProvider is read-only");
    }
}
