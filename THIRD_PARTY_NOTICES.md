# 第三方组件与许可证

本项目在源码和发布包中使用了以下第三方组件。各组件的版权与许可归其各自作者所有，本项目仅在其许可证允许的范围内使用。

## 随 Windows 发布包分发的组件

当前维护并发布的 Windows 客户端是**原生单文件程序**（`windows/dist/codepass.exe`，C# / .NET），仅依赖 Windows 自带的 .NET Framework 4.x，不随包分发任何第三方运行库。.NET Framework 由 Microsoft 提供，适用其自身许可条款。

## 归档的 Flutter 构建（不随公开发行包分发）

仓库的 `archive/` 目录保留了早期的 Flutter 客户端与 Flutter 设置页实现及其构建产物。这些内容**不随公开发行包分发**，仅供历史复核。它们使用以下组件：

| 组件 | 用途 | 许可证 |
| --- | --- | --- |
| Flutter 引擎（`flutter_windows.dll`、`data/` 资源） | 界面运行时 | BSD-3-Clause（Copyright 2014 The Flutter Authors） |
| Dart SDK（AOT 编译产物） | 后台与界面 | BSD-3-Clause（Copyright 2012, the Dart project authors） |
| Material Icons 字体（`MaterialIcons-Regular.otf`） | 图标 | Apache-2.0 |
| Microsoft Visual C++ 运行库（`msvcp140*.dll`、`vcruntime140*.dll`、`concrt140.dll`、`vccorlib140.dll`） | C++ 运行时 | Microsoft 可再发行代码条款（Microsoft Visual Studio 2022 Redistribution） |

上述 Flutter 构建的 `data/flutter_assets/NOTICES.Z` 包含 Flutter 及其内置依赖（如 Skia、ICU、`material_color_utilities`、`vector_math`、`characters`、`collection` 等）的完整许可证文本；若确需分发对应产物，请随包保留。

## 归档 Flutter 源码使用的 Dart 依赖

| 组件 | 版本 | 许可证 |
| --- | --- | --- |
| `cryptography` | 2.9.0 | Apache-2.0 |
| `crypto` | 3.0.7 | BSD-3-Clause（Copyright 2015, the Dart project authors） |
| `flutter_localizations`（Flutter SDK） | — | BSD-3-Clause |
| `intl` | 0.20.3 | BSD-3-Clause（Copyright 2013, the Dart project authors） |

完整版本固定在归档 Flutter 工程的 `archive/flutter-client/pubspec.lock`。

## 构建工具（不随发布包分发）

| 组件 | 许可证 |
| --- | --- |
| Gradle 与 Gradle Wrapper | Apache-2.0 |
| Android SDK / Build Tools / NDK | Android SDK 许可条款 |
| LSPosed / Xposed API 82（`compileOnly`，不打包进 APK） | Apache-2.0 |
| Microsoft Visual Studio C++ 生成工具 | Microsoft 许可条款 |

## 外部服务（不随本项目分发）

- **ntfy**：可选的自建或公共服务，由使用者自行部署或选择，适用其自身许可与隐私政策；
- **SmsForwarder 等免 root 转发应用**：由第三方提供，本项目不包含也不控制其代码。

## 商标说明

Android、Google、Flutter、Dart、Windows、Visual Studio、ntfy 等名称和商标归其各自所有者所有。本项目与上述公司或项目**没有任何隶属、赞助或背书关系**，使用这些名称仅用于说明兼容性或技术依赖。

本项目（codepass）的名称、图标与文档**不随 MIT 许可证授权给他人作为自身产品或服务的标识**，第三方不得暗示本项目为其背书，也不得使用相同名称发布修改版本以免造成混淆。

## 再分发提示

如果你再次分发本项目的发布包，请一并保留本文件与 `LICENSE`。若分发的是归档的 Flutter 构建产物，还需保留其 `NOTICES.Z` 与微软运行库相关的许可要求。请不要删除或修改第三方许可证声明。
