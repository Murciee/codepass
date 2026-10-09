<div align="center">

<img src="assets/codepass-sms.png" width="112" alt="codepass" />

# codepass

**Android 短信验证码同步至 Windows 剪贴板。**

Android 手机转发短信 → Windows 自动识别验证码 → 写入剪贴板。

[![最新版本](https://img.shields.io/github/v/release/Murciee/codepass?label=%E6%9C%80%E6%96%B0%E7%89%88%E6%9C%AC&color=2f75f0)](https://github.com/Murciee/codepass/releases/latest)
[![下载量](https://img.shields.io/github/downloads/Murciee/codepass/total?label=%E4%B8%8B%E8%BD%BD%E9%87%8F&color=2f75f0)](https://github.com/Murciee/codepass/releases)
[![平台](https://img.shields.io/badge/Windows-10%20%2F%2011-2f75f0)](#快速开始局域网模式)
[![Android](https://img.shields.io/badge/Android-6%2B-2f75f0)](#推荐安装方式)
[![License](https://img.shields.io/badge/License-MIT-2f75f0)](LICENSE)
[![Stars](https://img.shields.io/github/stars/Murciee/codepass?style=social)](https://github.com/Murciee/codepass/stargazers)

[快速开始](#快速开始局域网模式) · [方案选择](#推荐安装方式) · [公网使用](#外网使用ntfy) · [构建](#构建) · [常见问题](#常见问题) · [文档索引](#文档索引)

**简体中文** · [English](README.en.md)

</div>

> [!WARNING]
> **免费测试版**，不提供售后支持，也不承诺兼容性与持续维护；Windows 包未做代码签名，未签名 APK 无法直接安装。使用前请阅读 [免责声明](DISCLAIMER.md) 与 [隐私说明](PRIVACY.md)。

```text
Android 新短信 → 局域网 / EasyTier / ntfy → Windows 识别验证码 → 剪贴板
```

## 主要功能

| 能力 | 说明 |
| --- | --- |
| 📋 自动复制 | 识别到验证码即写入剪贴板，可选桌面通知与自动清除 |
| 🌐 两种通道 | 局域网 HTTP（默认 `8787`）；可选 ntfy 公网中转；也支持 EasyTier 组网 |
| 🖥️ 托盘常驻 | 原生单文件 `codepass.exe`，仅依赖系统自带 .NET Framework 4.x |
| 🔐 隐私与安全 | 访问令牌、通知脱敏、程序密码锁、配置导入导出 |
| 🕘 最近记录 | 保留最近 100 条，单击验证码即可复制 |
| 📱 多种手机方案 | 免 root Webhook、root 脚本、Magisk、LSPosed，任选一种 |
| 🎨 界面 | Windows 原生 Fluent 圆角界面；Magisk 轻量 WebUI |

## 推荐安装方式

普通用户建议「局域网 + 免 root Webhook」：配置简单、延迟低、无需 root。

| 手机环境 | 推荐方案 | 说明 |
| --- | --- | --- |
| 🌱 未 root | [免 root 指南](phone/no-root.md) | 用 SmsForwarder 等 Webhook 应用 |
| 🧱 已安装 Magisk | [Magisk 指南](phone/magisk/README.md) | 开机运行，可用 WebUI |
| ⌨️ 已 root | [root 手机端](phone/README.md) | 便于手动配置与调试 |
| 🔌 已安装 LSPosed | [LSPosed 指南](phone/lsposed/README.md) | 实验性，兼容性取决于 ROM |

只启用一种方案，避免重复发送。

## 快速开始：局域网模式

1. **Windows 端**：运行 `codepass.exe`（单文件，配置存在 EXE 同目录）。首次运行如遇防火墙提示，允许「专用网络」。在「设置」页点「检测本机地址」记下 IPv4，并填一段随机「访问令牌」。
2. **手机端**：Webhook 目标地址 `http://电脑IPv4:8787/sms`，方法 `POST`，正文用短信正文变量；令牌放进 `X-Token` 请求头或 `?token=` 参数。
3. **验证**：点「测试连通性」后，用手机发一条测试短信，识别到的验证码会写入剪贴板并显示在记录页。

```text
http://电脑IPv4:8787/sms
X-Token: 你的令牌
```

> 关闭窗口只是隐藏到托盘，接收不中断；完全退出请右键托盘图标。修改端口或 ntfy 后需重启生效，访问令牌即时生效。

## Windows 设置

| 设置项 | 说明 |
| --- | --- |
| 监听端口 / 访问令牌 | 手机访问端口（默认 `8787`）与局域网共享令牌 |
| 随 Windows 启动 | 当前用户登录后自动启动，无需管理员权限 |
| 验证码长度 | 默认 4～8 位，可设 1～32 |
| 自动清除剪贴板 | 写入后延时清空，`0` 表示不清除 |
| 桌面通知 / 隐私保护 | 是否通知，以及通知内容的三档脱敏 |
| 信息过滤 | 逐行关键字或 .NET 正则，命中即忽略该短信 |
| 程序密码锁 | 保护设置与记录页，可选自动锁定 |
| 关于与检查更新 | 版本号、GitHub 主页、检查更新（自动下载、重启完成） |
| ntfy 设置 | 公网中转的服务器、主题与令牌 |

要点：输入框失焦或回车即自动保存；令牌、验证码长度、通知内容立即生效，端口与 ntfy 在下次启动应用。数据文件 `config.ini` / `history.txt` / `app.log` 位于 EXE 同目录。

## 外网使用：ntfy

手机与电脑不在同一网络时，用 ntfy 中转：在 Windows 与手机端填相同的服务器、主题、令牌，改完配置后从托盘退出并重启。推荐自建或使用 HTTPS 服务，主题名需足够随机。详见 [`phone/ntfy.md`](phone/ntfy.md)。

## 网络接口

```http
POST http://电脑IPv4:8787/sms      # 正文：验证码 / 完整短信 / JSON {"code":"654321"}
GET  http://电脑IPv4:8787/health   # 健康检查，返回 {"ok":true}
```

令牌经 `X-Token` 头或 `?token=` 传入；设置了令牌时两个接口都会校验，不匹配返回 `403`。请求体上限约 1 MiB。本地 HTTP 不加密，不要映射到公网。

## 常见问题

- **手机连不上电脑**：确认同一 Wi-Fi / EasyTier、地址为电脑当前 IPv4、端口与令牌一致，并放行防火墙（EasyTier 用其虚拟网卡地址）。
- **收到短信但没识别**：确认短信已到达电脑，并检查「验证码长度」范围。
- **没有通知、剪贴板也没有**：多为剪贴板被占用。常见占用程序：游戏加速器、剪贴板工具（Ditto、PowerToys、Snipaste）、远程控制（ToDesk、向日葵、网易UU、`rdpclip`）、部分输入法与办公软件；重启电脑可快速恢复。
- **改端口 / ntfy 不生效**：这两项只在启动时应用，需从托盘退出后重启。
- **锁屏后停止转发**：关电池优化、允许自启动与后台运行，并在最近任务中锁定。

## 安全与风险

- 始终设置随机访问令牌；令牌为空时局域网接口不鉴权，且不要映射到公网；
- ntfy 主题与令牌不得提交到公开仓库，服务器须用 HTTPS；
- `app.log`、`history.txt` 与手机配置可能含验证码或令牌，请妥善保护；
- 程序密码锁仅保护界面，不加密本地数据；通知脱敏不等于完整隐私保护。

**本项目为免费测试阶段的个人开源工具，按「现状」提供，不承诺支持与维护。** 使用前请阅读 [免责声明](DISCLAIMER.md)、[隐私说明](PRIVACY.md)、[第三方组件](THIRD_PARTY_NOTICES.md)、[安全问题](SECURITY.md)、[维护边界](SUPPORT.md)；源码以 [MIT 许可](LICENSE)发布。

## 构建

| 目标 | 命令 | 产物 |
| --- | --- | --- |
| Windows 原生单文件 | `windows\build.ps1 -Legacy` | `windows/dist/codepass.exe` |
| LSPosed 原生回退版 | `phone\lsposed\build.ps1 -Legacy` | `release/codepass-lsposed-debug.apk` |
| Magisk 模块 | `phone\magisk\build.ps1` | `release/codepass-sms-magisk.zip` |

LSPosed 正式包：先加 `-Release` 生成未签名 APK，再用 `-SignAndroid -AndroidKeystore <文件> -AndroidKeyAlias <别名>` 搭配 `CODEPASS_ANDROID_STORE_PASSWORD` / `CODEPASS_ANDROID_KEY_PASSWORD` 环境变量签名，输出 `release/codepass-lsposed-release.apk`。构建环境通过 `JAVA_HOME`、`ANDROID_HOME` 等变量提供，详见 [`docs/BUILD_ANDROID.md`](docs/BUILD_ANDROID.md)。

## 仓库结构

```text
windows/  Windows 原生客户端（当前维护主线）   phone/  手机端方案
release/  发布产物                            docs/   发布 / 构建 / 合规文档
tools/    发布前检查脚本                      assets/ 文档与界面用图
```

历史 Flutter / Rust 实现与旧产物收纳在本地 `archive/`（不随仓库发布）。

## 文档索引

- 使用：[免 root](phone/no-root.md) · [Magisk](phone/magisk/README.md) · [LSPosed](phone/lsposed/README.md) · [root / 脚本](phone/README.md) · [ntfy](phone/ntfy.md)
- 发布与合规：[发布清单](docs/RELEASE_CHECKLIST.md) · [发布说明模板](docs/RELEASE_NOTES.md) · [构建环境](docs/BUILD_ANDROID.md) · [更新记录](CHANGELOG.md)

## Star History

[![Star History Chart](https://api.star-history.com/svg?repos=Murciee/codepass&type=Date)](https://star-history.com/#Murciee/codepass&Date)

<div align="center">

**[⬆ 回到顶部](#codepass)**

</div>
