# 发布说明（模板）

> 本文件是发布到 GitHub Release 时的说明模板。每次发布时复制一份并替换内容。
> 发布时请如实描述签名状态与已测试环境，不要夸大可靠性。

## codepass v1.0.3（公开测试，发布前填写验证结果）

### 这是什么

把 Android 短信验证码转发到 Windows 剪贴板的开源工具。**这是个人项目，不提供售后支持和兼容性承诺。**

### 本版变化

- 只保留 Windows 原生客户端、LSPosed 原生模块与 Magisk 主线，构建不再依赖本地归档目录；
- Windows 无令牌时只监听回环，局域网接收要求令牌；更新改为检查版本并打开下载页，手动覆盖；
- 内置手机端 ntfy 强制 HTTPS；LSPosed 使用私有配置与受 UID 限制的 Provider，不再回退旧目标；
- Windows ZIP 不含本机配置或记录，开发 APK 不进入正式发行目录。

### 下载

| 文件 | 说明 |
| --- | --- |
| `codepass-windows.zip` | Windows 原生客户端（解压后运行 `codepass.exe`，单文件、无需安装） |
| `codepass-sms-magisk.zip` | Magisk 模块（适用于已安装 Magisk 的设备） |
| `codepass-lsposed-release.apk` | 已用固定发行证书签名并验证的 LSPosed 原生模块 |
| `SHA256SUMS.txt` | 附件的 SHA-256 校验值；签名或替换附件后重新生成 |

> **Windows 包当前未进行代码签名**，Windows 或安全软件可能提示未知发布者。不要为此关闭系统安全功能。
>
> **Android 正式 APK 已通过 v1/v2/v3 签名和对齐校验**，请勿混入 Debug 或未签名开发 APK。

公开发行证书 SHA-256 指纹：

```text
ffcebedc89bb91a985628d1f83661733bf6f7833bf1dbecfbd3a22038c488b4d
```

从 Debug APK 切换到正式 APK 通常需要先卸载旧包，会清除应用内配置。仅使用旧 `lsposed.conf` 的用户需在新版应用内重新填写；清空全部目标并保存可停止转发。

Windows 更新时仅覆盖 `codepass.exe`，保留自己的 `config.ini`、`history.txt`。首次设置访问令牌或更换绑定地址/端口后需重启。

### 已完成的本地验证（不等于真机兼容验证）

- 原生 Windows C#/.NET 构建通过，WPF 主界面 XAML 解析通过；
- 23 项合成测试通过：绑定范围、HTTP 令牌校验、健康检查、短信接口、错误路由、截断/超大请求、HTTPS 和版本比较；
- Java 17 + Gradle 8.2.1 原生 Android Release 编译通过，APK 已完成固定发行证书签名、对齐和 v1/v2/v3 签名验证；
- Magisk ZIP 打包、WebUI JavaScript 语法、shell LF 换行检查通过；
- 独立只读代码复核完成，未发现阻断问题；
- **发布前仍须补充**：APK 真机安装/覆盖升级、实际 LSPosed Provider 访问与短信 hook、锁屏/重启/断网、Magisk 真机安装和实际短信送达测试。不要把上述本地验证写成真机已测。

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
