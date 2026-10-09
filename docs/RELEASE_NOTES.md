# 发布说明（模板）

> 本文件是发布到 GitHub Release 时的说明模板。每次发布时复制一份并替换内容。
> 发布时请如实描述签名状态与已测试环境，不要夸大可靠性。

## codepass <版本号>

### 这是什么

把 Android 短信验证码转发到 Windows 剪贴板的开源工具。**这是个人项目，不提供售后支持和兼容性承诺。**

### 本版变化

- 待填写

### 下载

| 文件 | 说明 |
| --- | --- |
| `codepass-windows.zip` | Windows 原生客户端（解压后运行 `codepass.exe`，单文件、无需安装） |
| `codepass-sms-magisk.zip` | Magisk 模块（适用于已安装 Magisk 的设备） |
| `codepass-lsposed-debug.apk` | LSPosed 模块（开发验证包，仅供受控设备测试，不作为普通安装包分发） |

> **Windows 包当前未进行代码签名**，Windows 或安全软件可能提示未知发布者。不要为此关闭系统安全功能。
>
> **未签名的 Android APK 无法正常安装**，请使用已签名版本，或自行签名。

### 已测试环境

- 待填写（例如：Windows 11 23H2 x64 + Android 15 某机型）

### 未测试 / 不保证

- 未在所有 Windows 版本、DPI 和显示器组合上验证；
- 未在所有 Android 厂商 ROM 上验证锁屏、省电、重启与断网场景；
- LSPosed hook 依赖 ROM 内部实现，可能不兼容。

### 使用前请阅读

- [免责声明](../DISCLAIMER.md)
- [隐私说明](../PRIVACY.md)
- [第三方组件与许可证](../THIRD_PARTY_NOTICES.md)
- [安全问题报告](../SECURITY.md)

### 反馈

提交 Issue 时**请勿粘贴真实短信正文、验证码或访问令牌**。维护者不承诺响应时限。
