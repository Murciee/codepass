# Android / Java 构建环境

本文说明构建 codepass 手机端产物（LSPosed 模块、Flutter 设置页）所需的工具链，以及构建脚本如何定位这些工具。普通用户无需阅读本文，也不需要安装开发环境——直接使用发布目录中的安装包即可。

## 需要什么

| 组件 | 版本 | 环境变量 |
| --- | --- | --- |
| JDK | 17（Temurin 17.0.20.1+ 已验证） | `JAVA_HOME` |
| Android SDK | API 36 | `ANDROID_HOME` / `ANDROID_SDK_ROOT` |
| Android Build Tools | 36.0.0 | 随 SDK |
| Android NDK | 28.2.13676358 | 随 SDK |
| Gradle | 8.2.1（命令行） | `GRADLE_HOME` |
| Gradle 用户缓存 | 任意可写目录 | `GRADLE_USER_HOME` |
| Flutter | 3.47.6（仅 Flutter 设置页构建需要） | `FLUTTER_ROOT` |
| Pub 缓存 | 任意可写目录 | `PUB_CACHE` |
| Xposed API | 82（`compileOnly`，不打包进 APK） | 见 `phone/lsposed/README.md` |

> Flutter 设置页的源码与产物已归档到开发机本地的 `archive/flutter-client`（不随公开仓库发布）。因此**公开仓库默认只能构建原生回退版**（`phone\lsposed\build.ps1 -Legacy`）；Flutter 版构建需要本地保留归档目录。

## 构建脚本如何定位环境

`phone\lsposed\build.ps1` 与归档的 `archive/flutter-client/build.ps1` 按以下顺序解析 Android 构建环境：

1. 命令行参数 `-EnvironmentRoot`（应指向同时包含 `jdk/`、`sdk/`、`gradle/` 的目录）；
2. 环境变量 `CODEPASS_ANDROID_ENV`；
3. 由 `ANDROID_HOME` 的父目录推导；
4. 否则回退到仓库内的 `android-env/`（通常不存在）。

JDK 路径取自 `JAVA_HOME` 或上述目录下的 `jdk/`；Flutter 路径取自 `-FlutterRoot` 或 `FLUTTER_ROOT`；缓存目录取自 `GRADLE_USER_HOME` / `PUB_CACHE`。脚本不会把个人路径写入源码或文档。

## 本开发机的安装位置

本机把三套工具链集中安装在 `D:\Toolchains\`，并注册了用户级环境变量与 PATH，供各 Agent / 会话直接调用：

```text
D:\Toolchains\android-env\   Java 17、Android SDK、Gradle
D:\Toolchains\flutter-env\   Flutter 3.47.6、pub-cache、gradle-home
D:\Toolchains\rust-env\      Rust toolchain（历史 Rust 端口使用）
```

安装与验证脚本见 `D:\Toolchains\dev-env.ps1` 与 `D:\Toolchains\README.md`（本地文件，不随仓库发布）。

## 验证

新开一个 PowerShell 窗口（使环境变量生效）后执行：

```powershell
java -version
adb version
gradle -v
flutter --version
rustc --version
```

预期分别输出 JDK 17、Android platform-tools、Gradle 8.2.1、Flutter 3.47.6、Rust 版本。若命令找不到，请检查对应的环境变量与 PATH，或运行 `D:\Toolchains\dev-env.ps1`。

## 常见问题

- **Gradle 下载依赖失败**：企业代理或网络限制下，可通过 `-Proxy http://host:port` 使用代理；
- **Flutter Android 构建在 SDK 路径含非 ASCII 字符时失败**：`java.util.Properties` 按 ISO-8859-1 解码路径，中文路径会乱码；把 SDK 映射到纯 ASCII 路径（目录联接）后再构建；
- **不要提交个人环境配置**：`local.properties`、签名材料与缓存目录均已由 `.gitignore` 排除。
