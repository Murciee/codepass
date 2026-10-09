# Android / Java 构建环境

本文说明构建 codepass 原生 LSPosed 模块所需的工具链。普通用户无需阅读本文，也不需要安装开发环境——直接使用发布目录中的安装包即可。

## 需要什么

| 组件 | 版本 | 环境变量 |
| --- | --- | --- |
| JDK | 17（Temurin 17.0.20.1+ 已验证） | `JAVA_HOME` |
| Android SDK | API 34（对应 `compileSdk 34`） | `ANDROID_HOME` / `ANDROID_SDK_ROOT` |
| Android Build Tools | 36.0.0 | 随 SDK |
| Gradle | 8.2.1（命令行） | `GRADLE_HOME` |
| Gradle 用户缓存 | 任意可写目录 | `GRADLE_USER_HOME` |
| Xposed API | 82（`compileOnly`，不打包进 APK） | 见 `phone/lsposed/README.md` |

## 构建脚本如何定位环境

`phone\lsposed\build.ps1` 按以下顺序解析 Android 构建环境：

1. 命令行参数 `-EnvironmentRoot`（应指向同时包含 `jdk/`、`sdk/`、`gradle/` 的目录）；
2. 环境变量 `CODEPASS_ANDROID_ENV`；
3. 由 `ANDROID_HOME` 的父目录推导；
4. 否则回退到仓库内的 `android-env/`（通常不存在）。

JDK 路径取自 `JAVA_HOME` 或上述目录下的 `jdk/`；Gradle 缓存目录取自 `GRADLE_USER_HOME`。脚本不会把个人路径写入源码或文档。

## 本开发机的安装位置

本机 Android 工具链安装在 `D:\Toolchains\`，并注册了用户级环境变量与 PATH：

```text
D:\Toolchains\android-env\   Java 17、Android SDK、Gradle
```

安装与验证脚本见 `D:\Toolchains\dev-env.ps1` 与 `D:\Toolchains\README.md`（本地文件，不随仓库发布）。

## 验证

签名产物生成后重新生成附件校验值，并运行最终发布门禁（缺附件、哈希过期或签名验证失败会阻止发布）：

```powershell
$files = 'codepass-windows.zip', 'codepass-sms-magisk.zip', 'codepass-lsposed-release.apk'
$lines = foreach ($name in $files) {
    $hash = Get-FileHash -LiteralPath (Join-Path release $name) -Algorithm SHA256 -ErrorAction Stop
    '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), $name
}
$lines | Set-Content -LiteralPath release\SHA256SUMS.txt -Encoding ascii
powershell -NoProfile -ExecutionPolicy Bypass -File tools\check-public-release.ps1 -RequireArtifacts
```

另外记录 `apksigner verify --verbose --print-certs` 输出的公开证书 SHA-256 指纹，与后续版本保持一致。私钥和口令不要公开。

新开一个 PowerShell 窗口（使环境变量生效）后执行：

```powershell
java -version
adb version
gradle -v
```

预期分别输出 JDK 17、Android platform-tools、Gradle 8.2.1。若命令找不到，请检查对应的环境变量与 PATH，或运行 `D:\Toolchains\dev-env.ps1`。

## 常见问题

- **Gradle 下载依赖失败**：可在用户级 `gradle.properties` 中设置 `systemProp.https.proxyHost` / `systemProp.https.proxyPort`；不要提交代理凭据。仓库目前不附带 Gradle Wrapper，请使用 Gradle 8.2.1 与 Java 17；
- **不要提交个人环境配置**：`local.properties`、签名材料与缓存目录均已由 `.gitignore` 排除。
