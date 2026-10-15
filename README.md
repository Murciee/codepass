<div align="center">

<img src="assets/codepass-sms.png" width="112" alt="codepass" />

# codepass

**短信与邮箱验证码，自动复制到 Windows 剪贴板**

Android 短信或邮箱验证码 → Windows → 剪贴板

[![最新版本](https://img.shields.io/github/v/release/Murciee/codepass?label=%E6%9C%80%E6%96%B0%E7%89%88%E6%9C%AC&color=2f75f0)](https://github.com/Murciee/codepass/releases/latest)
[![Windows](https://img.shields.io/badge/Windows-10%20%2F%2011-2f75f0)](#快速开始)
[![Android](https://img.shields.io/badge/Android-6%2B-2f75f0)](#手机接入)
[![License](https://img.shields.io/badge/License-MIT-2f75f0)](LICENSE)

[功能](#核心功能) · [截图](#功能截图) · [下载](#下载) · [快速开始](#快速开始) · [手机接入](#手机接入) · [安全](#安全与隐私)

**简体中文** · [English](README.en.md)

</div>

> [!WARNING]
> 个人开源项目，不承诺售后与兼容性。Windows 包未做代码签名。使用前请阅读 [免责声明](DISCLAIMER.md) 与 [隐私说明](PRIVACY.md)。

## 核心功能

- **📱 短信验证码** — Android 转发，支持局域网、EasyTier 与 ntfy；可按接收关键字筛选
- **📧 邮箱验证码** — QQ、163、126、Gmail、Outlook 及自定义 IMAP，不改变邮件已读状态
- **📋 自动复制** — 到达即可粘贴；可选通知与剪贴板自动清空
- **🕘 记录管理** — 搜索、分类筛选与原文查看；默认 10 分钟自动清理
- **🔐 隐私保护** — 通知内容分级、密码锁与脱敏诊断
- **🔄 应用内更新** — 检查到新版本后一键下载并安装
- **🖥️ 托盘常驻** — 随 Windows 静默启动，托盘菜单快速复制、清空记录、打开设置

## 功能截图

<table>
  <tr>
    <td width="50%" align="center"><b>验证码记录</b><br><img src="assets/v1.0.7/history-search.png" width="100%" alt="记录搜索与筛选、最新验证码"></td>
    <td width="50%" align="center"><b>邮箱设置</b><br><img src="assets/v1.0.7/email-settings.png" width="100%" alt="邮箱连接与接收规则"></td>
  </tr>
  <tr>
    <td width="50%" align="center"><b>短信设置</b><br><img src="assets/v1.0.7/sms-settings.png" width="100%" alt="短信连接与电脑端接收"></td>
    <td width="50%" align="center"><b>隐私设置</b><br><img src="assets/v1.0.7/privacy-settings.png" width="100%" alt="通知隐私与密码锁"></td>
  </tr>
</table>

## 下载

当前 Windows 版本 **v1.1.0**，手机模块为 v1.0.3。前往 [GitHub Releases](https://github.com/Murciee/codepass/releases/latest) 下载。

| 文件 | 用途 |
| --- | --- |
| `codepass-windows.zip` | Windows 客户端，解压运行 `codepass.exe` |
| `codepass-sms-magisk.zip` | Magisk 手机模块 |
| `codepass-lsposed-release.apk` | LSPosed 手机模块 |
| `SHA256SUMS.txt` | 附件校验值 |

升级只需从托盘退出后覆盖 `codepass.exe`，配置与记录自动保留；v1.0.7 起也可在「关于」页一键更新。

## 快速开始

### 短信

1. 打开「短信设置」，填写随机访问令牌与电脑 IPv4，重启程序。
2. 手机端将转发地址设为 `http://电脑IPv4:8787/sms`，令牌放入 `X-Token` 请求头（各方案见 [手机接入](#手机接入)）。
3. 点击「测试连通性」，发送测试短信完成端到端验证。

### 邮箱

打开「邮箱设置」，选择服务并填写账号与 IMAP 授权码，测试连接后勾选启用并点击「保存连接」。Gmail 配置见 [应用专用密码](docs/GMAIL_APP_PASSWORD.md)。

关闭窗口会隐藏到托盘，完全退出请使用托盘菜单。

## 手机接入

| 手机环境 | 方案 |
| --- | --- |
| 未 root | [免 root Webhook](phone/no-root.md) |
| Magisk | [Magisk 模块](phone/magisk/README.md) |
| 已 root | [root 脚本](phone/README.md) |
| LSPosed | [LSPosed 模块](phone/lsposed/README.md) |

短信转发只需启用一种方案；邮箱验证码在本机直接收取，无需手机端。

## 安全与隐私

- 局域网接收请设置随机令牌，不要把端口映射到公网。
- ntfy 使用 HTTPS；公共主题名不是可靠的访问控制。
- 配置、日志与通知可能包含敏感内容，请妥善保管。
- 记录使用 Windows 当前用户加密保护；通知脱敏不改变剪贴板内容。

## 开发维护

代码结构、构建流程与维护入口见 [代码地图](docs/ARCHITECTURE.md)。

## 许可

本项目使用 [MIT License](LICENSE)。另请阅读 [免责声明](DISCLAIMER.md)、[隐私说明](PRIVACY.md)、[安全问题](SECURITY.md) 和 [维护说明](SUPPORT.md)。

## 社区

感谢 [LINUX DO](https://linux.do) 社区提供开放、友善的技术交流平台。

## Star History

[![Star History Chart](https://api.star-history.com/svg?repos=Murciee/codepass&type=Date)](https://star-history.com/#Murciee/codepass&Date)
