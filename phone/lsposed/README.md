# codepass LSPosed 模块

LSPosed 方案通过 hook `com.android.phone` 的短信分发流程，读取新短信并在检测到验证码后把短信原文发送到 codepass Windows 端（验证码由电脑端识别）。

这是实验性方案。Android 版本、厂商 ROM 和电话进程实现可能不同，不能保证所有设备兼容。没有 LSPosed 使用经验时，建议优先使用 [免 root Webhook](../no-root.md) 或 [Magisk 模块](../magisk/README.md)。

## 构建条件

- Java 17；
- Android SDK API 34、Build Tools（本机使用 36.0.0）；无需 NDK；
- Gradle 8.2.1（命令行）；
- LSPosed/Xposed API 82 编译依赖。

构建环境可通过脚本参数指定，配置方式见 [Android 构建说明](../../docs/BUILD_ANDROID.md)。开发工具及缓存不应纳入发行包。

模块为原生 Java 实现，Android 6+；Debug 与未签名 Release 仅留在开发构建目录。

## 构建 APK

在项目根目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File phone\lsposed\build.ps1
```

或在当前目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

默认产出 Debug APK，仅保留在开发构建输出中，不复制到公开发行目录。调试资源可能包含源码定位和开发环境信息，只能用于开发验证。

构建 Release：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File phone\lsposed\build.ps1 -Release
```

Release 产物为未签名 APK，正式安装/发布前必须使用发行证书签名。Debug 版仅供测试，签名配置不要提交到仓库，口令使用环境变量传递。

原生 hook 源码位于 `app/src/main/java/com/codepass/lsposed/CodepassModule.java`，验证码识别规则由同目录的 `CodeExtractor.java` 提供。若无法下载 Xposed API 依赖，可将 API 82 JAR 放入 `app/libs/api-82.jar`。

Debug 产物位于 `app/build/outputs/apk/debug/app-debug.apk`，未签名 Release 位于 `app/build/outputs/apk/release/app-release-unsigned.apk`，两者均不会复制到 `release/`。

### 签名发行 APK

正式分发前，先生成并妥善保管发行证书（不要提交到仓库，不要使用 Debug 证书）：

```powershell
keytool -genkeypair -v -keystore codepass-release.jks -alias codepass `
  -keyalg RSA -keysize 2048 -validity 10000 -storetype PKCS12
```

使用证书签名构建（口令只通过环境变量传入，脚本不保存）：

```powershell
$env:CODEPASS_ANDROID_STORE_PASSWORD = '<你的口令>'
$env:CODEPASS_ANDROID_KEY_PASSWORD   = '<你的口令>'
powershell -NoProfile -ExecutionPolicy Bypass -File phone\lsposed\build.ps1 -Release -SignAndroid -AndroidKeystore D:\keys\codepass-release.jks -AndroidKeyAlias codepass
```

脚本使用 `ANDROID_HOME` 中 `build-tools` 里的 `zipalign`/`apksigner`。产物为已对齐、已签名并通过校验的 `release/codepass-lsposed-release.apk`。请另行备份证书文件与口令：**Android 升级必须使用同一证书**，丢失后将无法覆盖安装更新。

模块将配置保存在私有 SharedPreferences，仅通过校验调用 UID 的 ConfigProvider 提供给电话进程。清空全部目标并保存后停止转发，不再使用共享偏好或旧 `lsposed.conf`。只使用旧文件配置的用户升级后需在应用内重新填写。

从 Debug 版切换到发行版时，因为证书不同，通常需先卸载 Debug 版（会清除应用内配置），再安装发行版。

## 安装和启用

1. 安装由发行者签名的 Release APK，开发者可在受控设备使用 Debug APK；
2. 打开 `codepass` 应用，填写电脑 IPv4、端口和令牌；
3. 点击“保存配置”（无需 root）；
4. 在 LSPosed 模块列表中启用 codepass；
5. 作用域只需选择 `com.android.phone`；
6. 重启手机或重启电话进程。

设置页采用浅色原生单列布局，配置读写在后台串行执行。读取完成前表单不能编辑；无需授予应用 root 权限。Provider 读取失败时停止转发，并可能有最多 60 秒的重试退避。

升级时旧偏好文件权限会收紧为 `0600`，但 root 和系统级程序仍可能读取设备数据。旧 `lsposed.conf` 不再读取，可自行移走。

## 配置项目

```sh
PC_IP="192.168.1.100"
PC_PORT="8787"
TOKEN="与 Windows 端相同的令牌"
PC2_IP=""
PC2_PORT="8787"
PC2_TOKEN=""
NTFY_SERVER="https://ntfy.sh"
NTFY_TOPIC=""
NTFY_TOKEN=""
MIN_LEN="4"
MAX_LEN="8"
```

只使用局域网时，将 `NTFY_TOPIC` 留空。Windows 局域网接收必须设置随机令牌，手机 `TOKEN` 与之完全一致。第二台电脑填写 `PC2_IP`、`PC2_PORT` 和 `PC2_TOKEN`。ntfy 服务器必须为有效 HTTPS 地址，发送不会跟随重定向。

模块检测到验证码后转发的是短信原文，验证码由 Windows 端识别；记录页的“原文”为收到的短信内容（超过 120 字符会截断，换行转为空格）。

## 日志和排查

模块不会主动把短信正文、验证码或令牌写入日志。在 **LSPosed 管理器的日志页面**搜索 `codepass-lsposed` 前缀查看 hook 状态。

日志通过 `XposedBridge.log` 写入；`codepass-lsposed` 是消息前缀，不是独立的 logcat tag。不同 ROM 不一定将 LSPosed 日志转发到 logcat，不能仅凭 tag 过滤无结果判断模块未加载。

正常情况下可看到（`source=provider` 表示读取成功，`source=none` 表示未读取到配置）：

```text
hooked InboundSmsHandler.dispatchIntent methods=1
sms action=… bodyLen=… codeLen=6 config[source=provider pc=true port=8787 token=true pc2=false ntfy=false min=4 max=8]
forward pc1 http=200
```

日志只记录长度和布尔值，不含令牌或验证码。

如果没有 hook 日志，请检查：

- LSPosed 是否启用模块；
- 作用域是否包含 `com.android.phone`；
- 是否重启电话进程；
- 当前 ROM 是否使用不同的电话进程包名或类名。

如果 hook 正常但没有收到验证码，应检查转发配置、网络及 ROM 的 PDU 分发实现。不要公开短信正文、完整配置或含敏感信息的诊断截图；无法验证兼容性时，可选择 Magisk 或免 root Webhook 方案。

## 设计边界

- hook 只观察短信分发，不阻止系统短信应用接收短信；
- 检测到验证码后转发的是短信原文，验证码由电脑端识别；
- 网络请求在独立线程中执行，连接和读取超时为 5 秒；
- 相同短信正文在 30 秒内只转发一次；
- 默认验证码长度为 4～8 位，可在应用中调整；
- 不同 ROM 可能更改 `InboundSmsHandler` 的类名、方法或 PDU 格式。
