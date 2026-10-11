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

[下载](#下载) · [快速开始](#快速开始局域网模式) · [方案选择](#推荐安装方式) · [公网使用](#外网使用ntfy) · [构建](#构建) · [常见问题](#常见问题) · [文档索引](#文档索引)

**简体中文** · [English](README.en.md)

</div>

> [!WARNING]
> 本项目不提供售后支持，也不承诺兼容性与持续维护。Windows 包未做代码签名；LSPosed 正式 APK 使用固定发行证书签名，Debug/未签名 APK 不作为发行附件。使用前请阅读 [免责声明](DISCLAIMER.md) 与 [隐私说明](PRIVACY.md)。

```text
Android 新短信 → 局域网 / EasyTier / ntfy → Windows 识别验证码 → 剪贴板
```

<div align="center">

![codepass v1.0.4 记录页](assets/v1.0.4/history-search.png)

当前侧栏为「记录 / 短信设置 / 邮箱设置 / 隐私设置 / 关于」。

</div>

## 功能截图

以下截图来自 v1.0.4 的合成界面验证，记录内容为虚构数据，仅用于说明布局和操作位置。

### 记录搜索与自动清理

![记录页：全部/短信/邮件筛选、搜索和 10 分钟自动清理](assets/v1.0.4/history-search.png)

### 邮箱状态与窄窗口布局

![邮箱页：实时状态置顶，窄窗口将标签移到输入框上方](assets/v1.0.4/email-status-narrow.png)

### 短信设置与隐私设置

![短信设置窄窗口布局](assets/v1.0.4/sms-settings-narrow.png)

![隐私设置布局](assets/v1.0.4/privacy-settings.png)

### 脱敏诊断入口

![关于页：预览脱敏诊断入口](assets/v1.0.4/diagnostics-preview-entry.png)

## 主要功能

| 能力 | 说明 |
| --- | --- |
| 📋 自动复制 | 识别到验证码即写入剪贴板，可选桌面通知与自动清除 |
| 🌐 两种通道 | 局域网 HTTP（默认 `8787`）；可选 ntfy 公网中转；也支持 EasyTier 组网 |
| 🖥️ 托盘常驻 | 原生单文件 `codepass.exe`，仅依赖系统自带 .NET Framework 4.x |
| 🔐 隐私与安全 | 短信与邮件独立通知脱敏、程序密码锁，集中在「隐私设置」；另支持访问令牌与配置导入导出 |
| 🕘 最近记录 | 最多 100 条，默认自动清除 10 分钟前收到的记录；支持全部 / 短信 / 邮件筛选、搜索和单击复制 |
| 📱 多种手机方案 | 免 root Webhook、root 脚本、Magisk、LSPosed，任选一种 |
| 📧 邮箱验证码 | 可选：IMAP 读取验证码邮件，可用 QQ 新邮件提醒唤醒检查；不改动邮件已读状态 |
| 🎨 界面 | 邮箱状态置顶，连接 / 规则 / 高级设置分区；窄窗口标签置于输入上方，按钮自动换行 |
| 🩺 脱敏诊断 | 关于页先预览再保存报告；仅包含版本、开关、数量和归类状态，不导出原始日志 |

## 下载

当前 Windows 版本为 **v1.0.4**，手机端继续使用 v1.0.3，无需因本次 Windows 更新重装手机模块。安装包从 [GitHub Releases](https://github.com/Murciee/codepass/releases) 获取，以实际发布的附件为准。

| 附件 | 用途 |
| --- | --- |
| `codepass-windows.zip` | Windows 原生单文件客户端，解压后运行 `codepass.exe`，无需安装额外运行库 |
| `codepass-sms-magisk.zip` | 已安装 Magisk 的手机使用，通过模块管理器安装 |
| `codepass-lsposed-release.apk` | 发行证书签名的 LSPosed 原生模块，需已有 LSPosed 环境 |
| `SHA256SUMS.txt` | 三个附件的 SHA-256 校验值；用于核对文件，不代替签名或来源验证 |

本次更新的是 Windows 客户端；手机附件维持 v1.0.3，继续兼容。

仅需要安装所选手机方案，不必同时安装 APK 与 Magisk 模块。`release/` 不进入源码 Git 提交，安装包需作为 Release 附件单独上传。

**升级提示：** Windows 先从托盘退出，只覆盖 `codepass.exe`，保留自己的配置和记录。从 Debug APK 切换到发行 APK 通常需要先卸载旧包，会清除应用内配置；新版 LSPosed 不再读取旧 `lsposed.conf`，请在应用内填写配置。

### 发布验证边界

本次 Windows 构建、五套隔离回归 **358 项（0 失败）**及独立只读复核通过，包含搜索、脱敏诊断、预览锁定、硬链接保护、静默启动、自动清理和宽窄窗口几何检查。新版界面截图已纳入本 README；截图记录使用虚构数据。手机模块沿用已签名 v1.0.3，本次未重建 Android。真机安装、实际短信 hook、锁屏/重启和 Magisk 安装仍需设备验证。详见 [v1.0.4 验证记录](docs/v1.0.4-validation-20261011.md)。

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

1. **Windows 端**：运行 `codepass.exe`（单文件，配置存在 EXE 同目录）。先设置随机「访问令牌」，再点「检测本机地址」选择电脑 IPv4，从托盘退出并重启。防火墙提示时允许「专用网络」。
2. **手机端**：Webhook 目标地址 `http://电脑IPv4:8787/sms`，方法 `POST`，正文用短信正文变量；令牌放进 `X-Token` 请求头或 `?token=` 参数。
3. **验证**：点「测试连通性」后，用手机发一条测试短信，识别到的验证码会写入剪贴板并显示在记录页。

```text
http://电脑IPv4:8787/sms
X-Token: 你的令牌
```

> 关闭窗口只是隐藏到托盘；完全退出请右键托盘图标。修改绑定地址、端口、ntfy 或令牌有无后需重启；更换已有令牌立即生效，清空令牌会立即拒收局域网请求。

## Windows 设置

| 设置项 | 说明 |
| --- | --- |
| 监听端口 / 访问令牌 | 手机访问端口（默认 `8787`）与局域网共享令牌 |
| 随 Windows 启动 | 登录后静默驻留托盘，不弹出主界面；双击托盘图标打开，手动启动仍显示窗口 |
| 记录自动清理 | 记录页填写保留分钟数，默认 10 分钟；超过时限的短信/邮件记录一起清除，0 停用；不删除邮箱中的邮件 |
| 验证码长度 | 默认 4～8 位，可设 1～32 |
| 短信长度上限 | 默认 500 字符，可设 1～10000；超过上限整条忽略，不收录、不提示、不复制，邮箱不受影响 |
| 自动清除剪贴板 | 写入后延时清空，`0` 表示不清除 |
| 桌面通知 / 隐私设置 | 短信和邮件分别选择「仅提示收到 / 隐藏验证码、显示部分内容 / 显示完整通知内容」；默认仅提示收到，通知有长度上限 |
| 信息过滤 | 逐行关键字或 .NET 正则，命中即忽略该短信 |
| 邮箱接收 | 可选：IMAP 读取收件箱与分类文件夹中的验证码邮件；收件箱支持 IDLE 时实时接收，分类目录按间隔检查；排除已发送/草稿/已删除，超过约 30 分钟的旧邮件跳过 |
| 邮件复查 | 修改主题关键词/白名单后自动复查近 30 分钟，也可点击“重新检查近 30 分钟”；每文件夹最多 200 封候选，不自动复制复查得到的旧验证码 |
| 记录原文 | 发件人居中；原文悬停可预览，双击打开可滚动、可选择文字的全文窗口；未保存的邮件正文显示「未保存正文」，复查/首次补查记录标明仅历史 |
| 记录搜索与筛选 | 默认显示全部；可切换短信 / 邮件。搜索服务名、发件人、验证码、时间或已保存原文，不区分大小写；空格分隔多个词，全部命中才显示；「重置筛选」恢复全部 |
| QQ 弹窗联动 | 默认关闭：只读取 QQ 新邮件提醒窗口标题来唤醒收件箱和分类目录检查，不读取聊天正文或验证码 |
| 程序密码锁 | 保护设置与记录页，可选自动锁定 |
| 关于与检查更新 | 版本号、GitHub 主页；新版本打开下载页，手动更新 |
| 关于 → 预览脱敏诊断 | 查看完整报告后「保存此预览」为文本，自行分享；不自动上传，也不包含账号、地址、正文、验证码、令牌、规则文本或原始日志 |
| ntfy 设置 | 公网中转的服务器、主题与令牌 |

要点：输入框失焦或回车即自动保存；验证码长度、短信长度上限、通知内容、更换已有令牌与邮箱 / QQ 弹窗设置立即生效。切换邮箱服务会暂停接收并清空旧服务授权码，填好新服务凭据后再启用。绑定地址、端口、ntfy 或令牌有无变更后重启。数据文件 `config.ini` / `history.txt` / `app.log` 位于 EXE 同目录。

短信与邮件的通知隐私选项集中在「隐私设置」，各自独立，不改变剪贴板与历史原文。旧配置未指定邮件模式时，邮件继承原短信模式。隐藏验证码时先掩码再截断；JSON 或无法可靠定位验证码的通知退回隐藏内容。清空记录会同步清空托盘的最新验证码。

邮箱页分别显示收件箱、分类目录、最近处理、复查和连接测试结果。相同邮件 UID 的同一种正文结构、Base64 编码错误或正文 FETCH BAD 连续三次后会跳过该坏件，继续处理后续邮件；网络、授权、服务器 NO、头部读取、未知异常及保存失败不会因此跳过。保存失败会在设置页保留提示，可点击重试，导出操作不会清除运行配置的保存错误；启动时将一天前的本程序原子写临时文件移入回收站。

邮箱设置按「实时状态 → 邮箱连接 → 邮件接收规则 → 邮箱高级设置」排列。垃圾箱、正文保存、检查间隔和 QQ 联动集中在高级设置；短信页分为电脑接收、手机连接与处理、ntfy、过滤规则和配置管理。较窄窗口自动把标签移到输入框上方，窗口最小宽度为 780；放宽窗口后恢复左右布局。

排障优先使用「关于 → 预览脱敏诊断」。报告包含系统/程序版本、设置开关、规则条数、记录数量、归类状态，以及本次运行最近 40 条可识别的归类事件；不会读取磁盘日志或完整配置。保存内容与当前预览一致，运行状态变化后需重新打开预览获取新报告。未保存的邮件正文无法被搜索。

记录页底部可填写自动清理分钟数，默认 10，范围 0～43200；`0` 停用。修改后立即检查，后台每 15 秒检查一次，退出期间过期的记录会在下次启动清理。清理只移除本程序保存的短信/邮件记录，不删除邮箱中的邮件、不修改系统剪贴板历史。升级时旧配置也默认启用 10 分钟清理；需要长期保留请先改为 `0`。

## 外网使用：ntfy

手机与电脑不在同一网络时，用 ntfy 中转：两端填相同的 HTTPS 服务器、主题、令牌，Windows 改完配置后重启。优先使用带身份认证的私有服务；公共主题名不是可靠的访问控制。详见 [`phone/ntfy.md`](phone/ntfy.md)。

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
- **邮箱验证码没反应**：确认 IMAP 已连接、主题命中“验证码”“安全代码”等关键词，并查看连接状态下面的处理原因。先收到再添加关键词的邮件，可点击“重新检查近 30 分钟”；Gmail 可按 [`docs/GMAIL_APP_PASSWORD.md`](docs/GMAIL_APP_PASSWORD.md) 设置。
- **找不到某个服务的验证码**：在记录页选「全部」，输入服务名或验证码；邮件未保存正文时只能按发件人、验证码或时间查找。搜索只覆盖当前保留的最多 100 条记录。
- **需要分享排障信息**：打开「关于 → 预览脱敏诊断」，核对后保存报告。不要用「导出设置」代替诊断，也不要分享完整 `app.log`。

## 安全与风险

- 局域网接收必须设置随机访问令牌；令牌为空只监听本机，不要映射端口到公网；
- ntfy 主题与令牌不得提交到公开仓库，服务器须用 HTTPS；
- 当前 `history.txt` 使用 Windows 当前用户 DPAPI 加密；旧版明文在下次成功保存时迁移，换机或换用户不能直接读取。配置、日志和导出的迁移配置仍需妥善保护；
- 程序密码锁仅保护界面，历史加密与密码锁独立；通知脱敏不改变剪贴板，诊断导出不自动上传。

**本项目为个人开源工具，按「现状」提供，不承诺支持与维护。** 使用前请阅读 [免责声明](DISCLAIMER.md)、[隐私说明](PRIVACY.md)、[第三方组件](THIRD_PARTY_NOTICES.md)、[安全问题](SECURITY.md)、[维护边界](SUPPORT.md)；源码以 [MIT 许可](LICENSE)发布。

## 构建

| 目标 | 命令 | 产物 |
| --- | --- | --- |
| Windows 原生单文件 | `windows\build.ps1` | `windows/dist/codepass.exe` |
| Windows 一键合成回归 | `powershell -NoProfile -ExecutionPolicy Bypass -File windows\tests\Run-All.ps1` | 独立 EXE、测试日志与 `summary.json`，位于 `windows/build/regression-*` |
| LSPosed 原生模块（开发测试） | `phone\lsposed\build.ps1` | `phone/lsposed/app/build/outputs/apk/debug/app-debug.apk` |
| Magisk 模块 | `phone\magisk\build.ps1` | `release/codepass-sms-magisk.zip` |

LSPosed 正式包：用 `-Release -SignAndroid -AndroidKeystore <文件> -AndroidKeyAlias <别名>` 搭配 `CODEPASS_ANDROID_STORE_PASSWORD` / `CODEPASS_ANDROID_KEY_PASSWORD` 环境变量签名，输出 `release/codepass-lsposed-release.apk`。构建环境通过 `JAVA_HOME`、`ANDROID_HOME` 等变量提供，详见 [`docs/BUILD_ANDROID.md`](docs/BUILD_ANDROID.md)。

Windows 构建校验中英文 README、CHANGELOG 与发布说明的版本，输出程序集版本戳、构建标识与 `build-info.json`（含 EXE SHA-256）。一键回归使用合成邮箱/短信与独立目录，验证后回收本次合成数据，保留 EXE 和报告；不连接真实邮箱。详见 [Windows 回归说明](docs/windows-regression.md)。

## 仓库结构

```text
windows/  Windows 原生客户端（当前维护主线）   phone/  手机端方案
release/  发布产物                            docs/   发布 / 构建 / 合规文档
tools/    发布前检查脚本                      assets/ 文档与界面用图
```

仓库只保留当前维护的 Windows 原生客户端、Magisk 模块和 LSPosed 原生模块。

## 文档索引

- 使用：[免 root](phone/no-root.md) · [Magisk](phone/magisk/README.md) · [LSPosed](phone/lsposed/README.md) · [root / 脚本](phone/README.md) · [ntfy](phone/ntfy.md)
- 发布与合规：[发布清单](docs/RELEASE_CHECKLIST.md) · [发布说明模板](docs/RELEASE_NOTES.md) · [构建环境](docs/BUILD_ANDROID.md) · [更新记录](CHANGELOG.md)
- Windows 验证：[一键回归与构建追溯](docs/windows-regression.md) · [v1.0.4 验证记录](docs/v1.0.4-validation-20261011.md)

## 社区

感谢 [LINUX DO](https://linux.do) 社区提供开放、友善的技术交流平台

## Star History

[![Star History Chart](https://api.star-history.com/svg?repos=Murciee/codepass&type=Date)](https://star-history.com/#Murciee/codepass&Date)


<div align="center">

**[⬆ 回到顶部](#codepass)**

</div>
