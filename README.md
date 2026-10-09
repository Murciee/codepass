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
> **发布状态：免费测试版。** 本项目是个人免费分享的开源工具，不是商业产品，**不提供售后支持，也不承诺兼容性与持续维护**。自动化测试不能替代终端兼容性验证；Windows 包未进行代码签名，未签名 APK 不能直接安装。
>
> **使用前请阅读 [免责声明与用户须知](DISCLAIMER.md) 和 [隐私说明](PRIVACY.md)。** 本工具会处理短信验证码，存在泄露、识别错误和转发失败等风险；重要操作请以手机原始短信为准。

```text
Android 新短信 → 局域网 / EasyTier / ntfy → Windows 识别验证码 → 剪贴板
```

## 主要功能

| 能力 | 说明 |
| --- | --- |
| 📋 自动复制 | 收到验证码后写入 Windows 剪贴板，可选桌面通知与自动清除 |
| 🌐 两种通道 | 局域网 HTTP（默认 `8787`）；可选 ntfy 公网中转；也可通过 EasyTier 组网 |
| 🖥️ 托盘常驻 | 原生单文件 `codepass.exe`，仅依赖系统自带的 .NET Framework 4.x；关闭窗口隐藏到托盘，接收不中断 |
| 🔐 隐私与安全 | 访问令牌、通知内容三档脱敏、程序密码锁、配置导入导出 |
| 🕘 最近记录 | 保留最近 100 条记录，单击验证码即可复制（带轻提示） |
| 📱 多种手机方案 | 免 root Webhook、root 脚本、Magisk 模块、LSPosed 模块，任选一种 |
| 🎨 界面 | Windows 为原生 Fluent 圆角界面；Magisk 为轻量 WebUI；LSPosed 设置页的 Flutter 版实现已归档，仅保留本地历史复核 |

## 推荐安装方式

普通用户建议使用“局域网 + 免 root Webhook”方案：配置简单、延迟低、无需解锁 Bootloader 或获取 root 权限。

| 手机环境 | 推荐方案 | 说明 |
| --- | --- | --- |
| 🌱 未 root | [免 root 指南](phone/no-root.md) | 使用 SmsForwarder 等 Webhook 应用 |
| 🧱 已安装 Magisk | [Magisk 指南](phone/magisk/README.md) | 开机运行，可通过支持的管理器打开 WebUI |
| ⌨️ 已 root，偏好脚本 | [root 手机端](phone/README.md) | 便于手动配置与调试 |
| 🔌 已安装 LSPosed | [LSPosed 指南](phone/lsposed/README.md) | 实验性方案，兼容性取决于 ROM |

只启用一种短信转发方案，避免重复发送。root/Magisk 方案还要求 ROM 允许读取短信数据库。

## 快速开始：局域网模式

### 🖥️ 1. 准备 Windows 端

需要 Windows 10/11 x64 电脑及 Android 手机。运行 `codepass.exe` 即可，单文件、无需安装；配置、历史与日志保存在 EXE 所在目录，该目录需允许当前用户写入。首次运行时，如果 Windows 防火墙询问网络权限，请允许“专用网络”。

在“设置”页完成以下配置：

1. 点击“检测本机地址”，记下电脑 IPv4 地址；
2. 在“访问令牌”填入一段随机字符串（留空时接口不做校验，不建议，见[安全建议](#安全建议)）；
3. 修改设置后会自动保存。**访问令牌保存后立即生效；修改端口或 ntfy 设置需从托盘退出并重新启动程序**；
4. 点击“测试连通性”，确认服务正常；
5. 将电脑地址、端口和令牌填到手机端；root 脚本用户还可点击“复制手机端配置”，把参数块复制到剪贴板。

访问令牌必须与电脑端保持一致。

### 📱 2. 配置手机端

以 Webhook 应用为例，目标地址为：

```text
http://电脑IPv4地址:8787/sms
```

令牌可以通过请求头或 URL 参数传递：

```text
X-Token: 你的令牌
```

```text
http://电脑IPv4地址:8787/sms?token=你的令牌
```

请求方式选择 `POST`，正文使用短信正文变量（具体写法以所用应用为准）。详细配置见 [免 root 指南](phone/no-root.md)。保存规则后，用手机端测试功能验证连接。

### ✅ 3. 验证结果

托盘菜单中的“复制最新验证码”可用于复制最近收到的验证码。真实短信到达后，识别出的验证码会写入剪贴板并显示在记录页；未识别为验证码的通知类短信会被过滤，不会因为正文中的连续数字产生记录。

窗口行为：关闭窗口只是隐藏到托盘，后台接收继续运行；再次从托盘打开窗口即可。完全退出请右键托盘图标选择“退出”。最小化仅最小化窗口。若启用了“自动锁定”，重新打开窗口、从最小化恢复或关闭窗口后再次打开时会要求解锁；未启用“自动锁定”时，只有点击“立即锁定”才会锁定。

> 电脑端“测试连通性”只验证电脑到该地址的 HTTP 访问，不能代替手机到电脑的网络测试。最终请用手机发送一条测试短信验证完整链路。

## Windows 设置说明

| 设置项 | 说明 |
| --- | --- |
| 监听端口 | 手机访问电脑的 TCP 端口，默认 `8787` |
| 访问令牌 | 局域网接口的共享令牌，默认掩码显示，可点击右侧“显示 / 隐藏”按钮切换 |
| 随 Windows 启动 | 当前用户登录后自动启动，无需管理员权限 |
| 电脑 IPv4 地址 | 手机访问电脑时使用的地址，可点击“检测本机地址”自动填入 |
| 验证码长度 | 自动识别的数字长度，默认 4～8 位，可设为 1～32 |
| 自动清除剪贴板 | 写入后多少秒清空剪贴板，`0` 表示不清除 |
| 桌面通知 | 收到验证码后是否显示托盘通知 |
| 隐私保护 | 通知内容：只提示收到（默认）、显示内容但验证码替换为星号、完整显示短信内容 |
| 信息过滤 | 可填写多行关键字或 .NET 正则表达式；短信命中任一规则后直接忽略，不记录也不复制 |
| 程序密码锁 | 为设置和记录页启用密码锁，可选自动锁定，并提供“立即锁定” |
| 关于与检查更新 | 左侧“关于”分类提供版本号、GitHub 主页和检查更新；发现新版本会自动下载并在重启后完成更新 |
| ntfy 设置 | 公网转发服务器、主题和访问令牌 |

补充说明：

- **保存行为**：输入框失焦或按回车、勾选切换、下拉选择及密钥输入完成后会自动保存；访问令牌、验证码长度、通知内容等立即生效；端口和 ntfy 订阅在下次启动时应用。
- **监听地址**：服务监听 `0.0.0.0:<端口>`（所有网卡）。设置页中的“电脑 IPv4 地址”只用于生成手机端配置与显示，不改变监听网卡。
- **自动清除**：到达设定秒数后会清空剪贴板，不检查剪贴板内容是否已被其他程序改写。
- **通知脱敏**：默认只提示“收到一条新的验证码”。选择“显示内容，验证码替换为星号”会保留短信全文但把识别到的验证码替换为星号（*）。选择“完整显示短信内容”会在通知中保留验证码，可能在锁屏或旁观时泄露；内容以接收端实际收到的正文为准，超长文本受系统通知长度限制。
- **程序密码锁**：仅保护界面访问，不加密本地配置、历史或日志。密码使用 PBKDF2-HMAC-SHA1（100000 轮、16 字节盐、32 字节校验值）；“自动锁定”默认关闭；锁数据损坏时不会自动解除锁，需要原始完整配置恢复。
- **记录页**：窗口默认打开“记录”页，展示最近 100 条记录的“时间 / 发件人 / 验证码 / 原文”。发件人从短信正文首个 `【XXXX】` 字段提取，没有匹配时显示“未知发件人”；单击验证码即可复制并显示轻提示（点击其他区域不会触发复制）。
- **左侧分类**：设置区分为“常规设置 / 安全设置 / 关于”；常规设置合并电脑接收、手机端连接、ntfy 和配置管理，配置管理位于页面底部；主窗口支持拖动边缘调整大小并记住下次启动的窗口尺寸。安全设置包含密码锁管理与立即锁定。
- **关于**：左侧“关于”页面仅保留版本号、GitHub 主页和“检查更新”；发现新版本后会自动下载并在重启后完成更新，无需手动操作。
- **短信过滤**：内置过滤包含电信诈骗提醒、关闭境外服务、发送短信办理、客服热线等明显通知语义的长短信；还可在常规设置中逐行填写自定义关键字或 .NET 正则表达式，命中任一规则后直接忽略短信。无效正则不会阻断收短信。
- **数据文件**：`config.ini`（配置）、`history.txt`（最近记录）、`app.log`（运行日志）位于 EXE 所在目录；日志超过约 2 MiB 会清空后继续写入。

导出配置时可选择是否包含局域网令牌、ntfy 令牌和密码锁信息；导入脱敏配置会保留当前的令牌、ntfy 主题和密码锁，并保留当前的安全设置。

## 外网使用：ntfy

手机和电脑不在同一网络时，可以使用 ntfy 作为公网中转：

1. 使用公共服务 `https://ntfy.sh`，或部署自己的 HTTPS ntfy 服务；
2. 创建一个长度足够、不可猜测的主题名；
3. 在 Windows 和手机端填写相同的服务器、主题和访问令牌；
4. 在 Windows 端填写好配置（修改后自动保存），然后从托盘退出并重新启动；手机模块按其文档应用配置。

Windows 端订阅 ntfy 的流式接口，收到 `message` 事件时提取正文；按消息 ID 去重（保留最近 200 条），断线后每 5 秒重连，重连不会重放已处理的消息。

公共主题名不是完整的访问控制机制，不应使用可猜测名称。ntfy 服务器必须使用 HTTPS；处理验证码时优先选择具有身份认证和访问控制的私有 HTTPS 服务。自建部署方法见 [`phone/ntfy.md`](phone/ntfy.md)。

## 网络接口

局域网接收接口：

```http
POST http://电脑IPv4地址:8787/sms
Content-Type: text/plain
```

正文可以是验证码、完整短信或 JSON：

```text
654321
```

```json
{"code":"654321"}
```

发送时会尝试下列顺序提取：整段即验证码、JSON 的 `code` 字段、验证码/校验码/动态密码/OTP 等关键词上下文、以及独立的 4～8 位数字（优先 6 位）。接口返回 `{"ok":true}` 表示请求已被接收；是否识别到验证码请查看记录页或日志。

健康检查接口：

```http
GET http://电脑IPv4地址:8787/health
X-Token: 与电脑端相同的令牌
```

成功返回 `{"ok":true}`，不写入记录，也不触发剪贴板。

令牌通过请求头 `X-Token` 或 URL 参数 `?token=` 传递；**设置了访问令牌时，`/sms` 与 `/health` 都会校验令牌**，令牌为空时不做校验。令牌不匹配返回 HTTP `403`。请求体上限约 1 MiB，并发连接上限 32。本地 HTTP 不提供加密，不要直接将监听端口暴露到公网。

## 常见问题

### 📶 手机无法连接电脑

请依次检查：

1. 手机和电脑是否位于同一 Wi-Fi 或同一 EasyTier 网络；
2. 手机填写的地址是否为电脑当前 IPv4 地址；
3. 端口和令牌是否与 Windows 端一致；
4. Windows 防火墙是否允许专用网络或 EasyTier 网卡的入站连接；
5. 电脑端“测试连通性”是否成功。

使用 EasyTier 时，应填写 Windows EasyTier 虚拟网卡的 IPv4 地址，而不是普通 Wi-Fi 地址。

### 🔍 收到短信但没有识别

确认短信正文已经发送到电脑，并检查“验证码长度”设置。部分短信使用字母和数字混合格式，或验证码长度超出当前范围。

### 📋 收到短信后没有通知，剪贴板里也没有内容

写入成功会弹出“验证码已复制”；重试约 10 秒仍失败（且开启桌面通知）时会弹出“验证码已收到，复制失败”。如果 `app.log` 出现 `clipboard error`（例如 `OpenClipboard failed, win32=5`，旧版本为 `CLIPBRD_E_CANT_OPEN`），说明该电脑的剪贴板被其他程序占用或锁住：

1. 重启电脑可快速恢复；
2. 常见占用程序：游戏加速器（如网易UU）、剪贴板管理/增强工具（Ditto、PowerToys、Snipaste 等）、远程控制软件（ToDesk、向日葵、远程桌面的 `rdpclip`）、部分输入法与办公软件；
3. 若反复出现，逐一退出上述程序定位；若复制失败的提示频繁出现，请确认 codepass 是在**当前登录用户会话**中运行（不要通过“无论用户是否登录都运行”的计划任务启动）。

程序写入剪贴板时改用 Win32 直接写入（不依赖延迟渲染），被短期占用会自动重试约 10 秒、并校验写入结果；只有持续占用导致最终失败时才弹出“复制失败”提示，记录页仍保留该验证码，可手动复制。

### 🔄 修改了端口或 ntfy，但没有生效

端口与 ntfy 订阅只在启动时应用。请右键托盘图标选择“退出”，再重新运行程序。访问令牌保存后即时生效，无需重启。

### 🔒 锁屏后停止转发

免 root 应用需要关闭电池优化、允许自启动和后台运行，并在最近任务中锁定。不同品牌的设置名称可能不同。

### 🧩 Magisk 没有 WebUI

Magisk WebUI 需要管理器提供相应支持；不支持时仍可手动编辑 `config.conf`。详见 [Magisk 指南](phone/magisk/README.md)。

## 安全建议

- 始终为局域网接口设置随机访问令牌；令牌为空时接口不做校验；
- ntfy 主题和访问令牌不得发布到公开仓库；
- ntfy 服务器必须使用 HTTPS；
- `app.log`、`history.txt` 和手机配置文件可能包含验证码或令牌，应妥善保护；
- LSPosed 配置需要由 `com.android.phone` 读取，权限模型与 Magisk 不同，详见其专用说明。

## 风险、免责与许可

本项目是**免费、测试阶段的个人开源工具**，按“现状”提供，不附带任何明示或默示担保，也不承诺售后支持或持续维护。在适用法律允许的最大范围内，作者不对因使用本工具产生的损失负责；**本说明不排除依照适用法律不得排除或限制的责任**。开始使用即表示你已理解并接受这些内容。

使用前请阅读：

- [`DISCLAIMER.md`](DISCLAIMER.md)：完整免责声明与风险清单；
- [`PRIVACY.md`](PRIVACY.md)：处理哪些数据、保存在哪里、如何清理；
- [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md)：第三方组件与许可证；
- [`SECURITY.md`](SECURITY.md)：安全问题报告方式；
- [`SUPPORT.md`](SUPPORT.md)：维护边界；
- [`CHANGELOG.md`](CHANGELOG.md)：更新记录。

源代码以 [MIT 许可证](LICENSE)发布。**请注意 MIT 许可证同样允许他人商用、修改和再分发**；第三方组件仍适用其各自的许可证。

## 已知限制与发布要求

- **安全默认值**：Windows 客户端**不强制**访问令牌；令牌留空时局域网接口不做鉴权。请始终设置随机令牌并限制防火墙范围；不得直接映射到公网。
- **隐私边界**：局域网 HTTP 不加密传输；程序密码锁不加密配置或历史。自动清除只清空当前剪贴板内容，不影响系统剪贴板历史、云同步或其他应用持有的副本；通知脱敏不等于完整短信隐私保护。
- **可靠性**：当前不提供持久化发送队列、端到端送达保证或断线消息补发保证。休眠、断网、进程退出和 ROM 限制可能导致丢失；ntfy 断线重连只按消息 ID 去重，不保证补发；多种转发方案同时运行可能导致重复。
- **识别范围**：支持数字验证码的启发式识别，不保证所有短信格式正确。重要操作应核对验证码来源，不能将识别结果视为短信真实性证明。
- **发行签名**：Android 正式包需要固定发行证书，升级时必须沿用原证书；Windows 发行包应完成代码签名和完整性校验。Debug 和未签名包仅供开发验证。
- **兼容验证**：正式版本应覆盖 Windows 10/11、不同 DPI/显示器、Android 主流 ROM、锁屏/省电/重启以及网络中断场景。未验证环境不承诺兼容。
- **开源与治理**：本项目以 [MIT 许可证](LICENSE)发布，允许使用、修改、商用和再分发，但要求使用者自行承担风险并保留版权与许可声明；第三方依赖与运行库声明见 [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md)。隐私说明、安全问题报告、维护边界与发布检查分别见 [`PRIVACY.md`](PRIVACY.md)、[`SECURITY.md`](SECURITY.md)、[`SUPPORT.md`](SUPPORT.md) 和 [`docs/RELEASE_CHECKLIST.md`](docs/RELEASE_CHECKLIST.md)。

## 构建

以下命令在仓库根目录执行。SDK、编译器及签名工具仅在开发/发行环境需要，普通 Windows 用户无需安装开发环境。

| 目标 | 默认产物 | 构建条件 |
| --- | --- | --- |
| Windows（原生单文件） | `windows/dist/codepass.exe` | Windows 自带的 .NET Framework 4.x 编译器（`csc.exe`），无需额外 SDK |
| Android / LSPosed | 原生 Debug APK（`release/codepass-lsposed-debug.apk`） | Java 17、Android SDK 36 / Build Tools 36 / NDK 28.2、Xposed API 82；Flutter 设置页构建需 Flutter 3.47.6（源码已归档） |
| Magisk | `release/codepass-sms-magisk.zip` | Windows PowerShell，ZIP 打包无需 Android SDK |

Windows（当前维护的原生单文件客户端）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File windows\build.ps1 -Legacy
```

> `-Legacy` 构建本 README 描述的原生单文件客户端，产物为 `windows/dist/codepass.exe`，并自动打包 `release/codepass-windows.zip`；它是当前持续维护与发布的 Windows 客户端。`windows\build.ps1` 不带参数时委托到**已归档**的 Flutter 版客户端（源码见本地 `archive/flutter-client`，不随公开仓库发布），仅用于历史构建复核；两者默认都监听 `8787`，不要同时运行。

LSPosed 原生回退版（仓库保留的安装包）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File phone\lsposed\build.ps1 -Legacy
```

Magisk 模块：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File phone\magisk\build.ps1
```

`phone\lsposed\build.ps1 -Legacy` 生成原生回退版 Debug APK，并复制到 `release/codepass-lsposed-debug.apk`，供受控设备安装测试，不作为普通用户发行包。原生回退版同样支持 `-Release -SignAndroid` 直接产出已签名发行 APK，步骤见 [LSPosed 指南](phone/lsposed/README.md)。默认（不带 `-Legacy`）的 Flutter 设置页构建已归档（源码见本地 `archive/flutter-client`）；需要时可加 `-Release` 生成 Release 候选 APK，并在配置固定发行证书后用 `-SignAndroid -AndroidKeystore <文件> -AndroidKeyAlias <别名>` 签名，签名密码通过 `CODEPASS_ANDROID_STORE_PASSWORD` 和 `CODEPASS_ANDROID_KEY_PASSWORD` 环境变量提供。Debug 包可能包含源码定位和开发环境信息，不能用于公开分发或评估 Release 性能。

构建环境通过 `JAVA_HOME`、`ANDROID_HOME`、`FLUTTER_ROOT` 等环境变量，或 `-FlutterRoot`、`-EnvironmentRoot`、`-Proxy` 参数提供，不在源码或文档中保存个人环境配置；安装与验证方式见 [`docs/BUILD_ANDROID.md`](docs/BUILD_ANDROID.md)。

## 仓库结构

```text
codepass/
├─ windows/     原生单文件 Windows 客户端（当前维护、发布主线）
│  ├─ src/      C# 源码
│  ├─ dist/     构建产物 codepass.exe
│  └─ build.ps1 -Legacy 为维护的构建入口
├─ phone/       手机端方案（Webhook / root / Magisk / LSPosed）
├─ release/     发布产物（Windows ZIP、Magisk ZIP、LSPosed APK）
├─ docs/        发布、构建与合规文档
├─ tools/       发布前检查脚本
├─ assets/      文档与界面用图
└─ .github/     Issue 模板等 GitHub 配置
```

历史 Flutter / Rust 实现、旧构建产物与本地备份统一收纳在开发机本地 `archive/` 目录，并在 `.gitignore` 中排除，不随公开仓库发布。

## 文档索引

项目说明与发布：

- [`DISCLAIMER.md`](DISCLAIMER.md)：免责声明与风险清单；
- [`PRIVACY.md`](PRIVACY.md)：隐私说明；
- [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md)：第三方组件与许可证；
- [`SECURITY.md`](SECURITY.md)：安全问题报告；
- [`SUPPORT.md`](SUPPORT.md)：支持与维护边界；
- [`CHANGELOG.md`](CHANGELOG.md)：更新记录；
- [`docs/RELEASE_CHECKLIST.md`](docs/RELEASE_CHECKLIST.md)：发布前检查清单；
- [`docs/RELEASE_NOTES.md`](docs/RELEASE_NOTES.md)：发布说明模板。

使用与构建：

- [`phone/no-root.md`](phone/no-root.md)：免 root Webhook 配置；
- [`phone/README.md`](phone/README.md)：root、脚本和 Magisk 部署；
- [`phone/magisk/README.md`](phone/magisk/README.md)：Magisk 模块安装和配置；
- [`phone/lsposed/README.md`](phone/lsposed/README.md)：LSPosed 构建和启用；
- [`phone/ntfy.md`](phone/ntfy.md)：自建 ntfy 服务；
- [`docs/BUILD_ANDROID.md`](docs/BUILD_ANDROID.md)：Android / Java / Flutter 构建环境安装与验证。

## Star History

[![Star History Chart](https://api.star-history.com/svg?repos=Murciee/codepass&type=Date)](https://star-history.com/#Murciee/codepass&Date)

<div align="center">

**[⬆ 回到顶部](#codepass)**

</div>
