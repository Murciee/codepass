# codepass LSPosed 模块

LSPosed 方案通过 hook `com.android.phone` 的短信分发流程，读取新短信并在检测到验证码后把短信原文发送到 codepass Windows 端（验证码由电脑端识别）。

设置页历史上曾迁移到 Flutter，与 Windows 客户端共用控件和主题（该 Flutter 实现现已归档）。**短信 hook 始终是原生 Java，电话进程不会运行 Flutter 引擎。** Flutter 版支持 Android 7+；Android 6 保留原生回退版。

这是实验性方案。Android 版本、厂商 ROM 和电话进程实现可能不同，不能保证所有设备兼容。没有 LSPosed 使用经验时，建议优先使用 [免 root Webhook](../no-root.md) 或 [Magisk 模块](../magisk/README.md)。

## 构建条件

- Java 17；
- Flutter 3.47.6；
- Android SDK API 36、Build Tools 36.0.0、NDK 28.2.13676358；
- 工程自带的 Gradle 9.3.1 Wrapper；
- LSPosed/Xposed API 82 编译依赖。

构建环境可通过脚本参数指定，配置方式见 [Android 构建说明](../../docs/BUILD_ANDROID.md)。开发工具及缓存不应纳入发行包。

> Flutter 设置页的源码与构建产物已归档到 `archive/flutter-client`（不随公开仓库发布）。仓库保留的 LSPosed 安装包为**原生回退版** `release/codepass-lsposed-debug.apk`，由 `build.ps1 -Legacy` 生成，仅用于受控设备测试。

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

构建未签名 Release：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File phone\lsposed\build.ps1 -Release
```

Release 产物为 `release/codepass-lsposed-flutter-release-unsigned.apk`，使用 AOT 优化，正式安装/发布前需要自行签名。Debug 版可用于测试，但性能和包体不能代表 Release。签名配置不要提交到仓库，口令使用环境变量传递。

原生 hook 的唯一源码仍在 `app/src/main/java/com/codepass/lsposed/CodepassModule.java`，验证码识别规则由同目录的 `CodeExtractor.java` 提供（hook 与设置页测试共用）；Flutter 的 Android 构建会复制这两个文件到生成目录编译，不维护第二份 hook。若无法下载 Xposed API 依赖，可将 API 82 JAR 放入 `app/libs/api-82.jar`。

原生回退版（Android 6+）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File phone\lsposed\build.ps1 -Legacy
```

产物为 Debug APK，除开发构建输出外会复制到 `release/codepass-lsposed-debug.apk` 便于安装到设备；它仍属开发验证包，不要作为正式发行包分发。

### 签名发行 APK（原生回退版）

正式分发前，先生成并妥善保管发行证书（不要提交到仓库，不要使用 Debug 证书）：

```powershell
keytool -genkeypair -v -keystore codepass-release.jks -alias codepass `
  -keyalg RSA -keysize 2048 -validity 10000 -storetype PKCS12
```

使用证书签名构建（口令只通过环境变量传入，脚本不保存）：

```powershell
$env:CODEPASS_ANDROID_STORE_PASSWORD = '<你的口令>'
$env:CODEPASS_ANDROID_KEY_PASSWORD   = '<你的口令>'
powershell -NoProfile -ExecutionPolicy Bypass -File phone\lsposed\build.ps1 -Legacy -Release -SignAndroid -AndroidKeystore D:\keys\codepass-release.jks -AndroidKeyAlias codepass
```

脚本使用 `ANDROID_HOME` 中 `build-tools` 里的 `zipalign`/`apksigner`。产物为已对齐、已签名并通过校验的 `release/codepass-lsposed-release.apk`。请另行备份证书文件与口令：**Android 升级必须使用同一证书**，丢失后将无法覆盖安装更新。

原生回退版把配置保存在应用内（SharedPreferences），无需 root；保存后即时生效，无需重启电话进程。读取按 ConfigProvider（应用内只读通道）→ XSharedPreferences（建议在作用域中同时勾选 codepass 自身）→ 旧版 `/data/adb/codepass-sms/lsposed.conf` 的顺序回退，日志中的 `config[source=…]` 显示实际来源。

## 安装和启用

1. 安装由发行者签名的 Release APK，开发者可在受控设备使用 Debug APK；
2. 打开 `codepass` 应用，填写电脑 IPv4、端口和令牌；
3. 点击“保存配置”（无需 root）；
4. 在 LSPosed 模块列表中启用 codepass；
5. 作用域选择 `com.android.phone`；如需备用读取通道（XSharedPreferences），可同时勾选 codepass 自身；
6. 重启手机或重启电话进程。

设置保存在应用内（SharedPreferences），通过 ConfigProvider 提供给电话进程；旧版 `/data/adb/codepass-sms/lsposed.conf` 作为兼容读取通道保留。界面不展示内部文件位置。

应用界面与 Windows、Magisk WebUI 统一为浅蓝灰背景、白色大圆角卡片和柔和蓝色按钮，输入框有焦点反馈，保存结果会在页面内提示。root 配置读写在后台线程执行，不阻塞页面滚动。

支持浅色、深色和跟随系统外观；窄屏采用单列布局。配置加载完成前不能编辑，避免授权完成后覆盖输入；root 读写跨 Activity 生命周期串行执行，保存先写临时文件再替换原配置，超过 30 秒会提示失败/超时。

前台设置页会请求当前分辨率下的最高可用刷新率，离开页面后取消请求；前台监听省电模式变化并交回系统选择。滚动和控件动画由 Flutter 调度，尊重系统减少动画设置，不使用固定帧率定时器。ROM 高刷白名单和系统策略仍可能限制实际刷新率，不改变设备全局显示设置。

配置保存在应用内（SharedPreferences），电话进程通过 ConfigProvider 读取；勾选自身作用域时该共享偏好文件可能被其他应用读取，其中可能包含局域网或 ntfy 令牌，请评估设备上的应用读取权限风险。

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

只使用局域网时，将 `NTFY_TOPIC` 留空。Windows 端设置了访问令牌后，`TOKEN` 必须完全一致；两端都留空时不鉴权（不建议）。需要把短信同时发到第二台电脑时，填写 `PC2_IP`、`PC2_PORT` 和 `PC2_TOKEN`（`PC2_IP` 留空表示不启用）。

模块检测到验证码后转发的是短信原文，验证码由 Windows 端识别；记录页的“原文”为收到的短信内容（超过 120 字符会截断，换行转为空格）。

## 日志和排查

模块不会主动把短信正文、验证码或令牌写入日志。在 **LSPosed 管理器的日志页面**搜索 `codepass-lsposed` 前缀查看 hook 状态。

日志通过 `XposedBridge.log` 写入；`codepass-lsposed` 是消息前缀，不是独立的 logcat tag。不同 ROM 不一定将 LSPosed 日志转发到 logcat，不能仅凭 tag 过滤无结果判断模块未加载。

正常情况下可看到（`config[source=…]` 显示配置来源：provider / xsp / file，取决于版本与作用域设置）：

```text
hooked InboundSmsHandler.dispatchIntent methods=1
sms action=… bodyLen=… codeLen=6 config[source=file pc=true port=8787 token=true pc2=false ntfy=false min=4 max=8]
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
