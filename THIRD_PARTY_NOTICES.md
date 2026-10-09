# 第三方组件与许可证

本项目在源码和发布包中使用了以下第三方组件。各组件的版权与许可归其各自作者所有，本项目仅在其许可证允许的范围内使用。

## 随 Windows 发布包分发的组件

当前维护并发布的 Windows 客户端是**原生单文件程序**（`windows/dist/codepass.exe`，C# / .NET），仅依赖 Windows 自带的 .NET Framework 4.x，不随包分发任何第三方运行库。.NET Framework 由 Microsoft 提供，适用其自身许可条款。

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

Android、Google、Windows、Visual Studio、ntfy 等名称和商标归其各自所有者所有。本项目与上述公司或项目**没有任何隶属、赞助或背书关系**，使用这些名称仅用于说明兼容性或技术依赖。

本项目（codepass）的名称、图标与文档**不随 MIT 许可证授权给他人作为自身产品或服务的标识**，第三方不得暗示本项目为其背书，也不得使用相同名称发布修改版本以免造成混淆。

## 再分发提示

如果你再次分发本项目的发布包，请一并保留本文件与 `LICENSE`。请不要删除或修改第三方许可证声明。
