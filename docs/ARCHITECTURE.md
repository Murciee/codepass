# Windows 端代码地图

本文是维护 Windows 客户端时的快速入口。先按功能定位文件，再读取相关方法；不要为了修改一个页面而通读全部 `windows\src`。

## 构建边界

- Windows 客户端目标为系统自带的 .NET Framework 4.x。
- `windows\build.ps1` 使用 Windows PowerShell 调用系统 `csc.exe`，并把 `windows\src\` 根目录下的所有 `*.cs` 一起编译。
- 当前没有 `.csproj`，也没有 XAML 编译步骤；界面 XAML 保存在 C# 逐字字符串中，运行时由 `XamlReader.Parse` 解析。
- `MainWindow.cs` 与 `MainWindow.Xaml.cs` 是同一个 `partial MainWindow`。新建的 `.cs` 文件必须放在 `windows\src\` 根目录，不能只放进子目录，否则当前构建通配符不会自动包含它。
- 构建脚本会把 `src\*.cs` 的哈希写进 `build-info.json`。修改源码后应使用 `windows\build.ps1 -SkipPackage` 先验证编译，再按发布流程构建。
- 当前编译器是 .NET Framework 4.0 自带的 C# 编译器，按 C# 5 语法编写；不要在现有构建链中使用字符串插值、`nameof`、表达式体成员等新语法。

## 文件职责

| 文件 | 职责 | 修改常见入口 |
| --- | --- | --- |
| `windows\src\Program.cs` | 进程入口、单实例、托盘、模块装配、最新验证码与界面通知协调、邮箱线程生命周期 | 启动/退出、构造注入、托盘菜单、模块启停 |
| `windows\src\HttpReceiver.cs` | HTTP 监听、鉴权、请求解析、活动连接与停止收尾 | 局域网接收、协议边界、关闭连接 |
| `windows\src\NtfyReceiver.cs` | ntfy 订阅、重连、流事件解析与成功后确认 | 公网补收、请求取消、检查点推进 |
| `windows\src\IncomingDelivery.cs` | 短信规则快照、短信/邮件统一投递、历史持久化、延迟展示门禁 | 提取/过滤、去重结果、过期/删除/退出取消 |
| `windows\src\ClipboardService.cs` | 独立消息窗口、后台 STA 写入重试、请求号、手动复制、清理计时器与释放 | 剪贴板所有权、旧请求取消、序列号保护 |
| `windows\src\MainWindow.cs` | 主窗口共享字段、构造初始化、控件查找/事件接线、响应式布局、窗口/托盘生命周期 | 控件初始化、共享状态、窗口行为、跨页面事件 |
| `windows\src\MainWindow.Settings.cs` | 页面切换、普通设置、短信接收关键字标签、配置读取/保存、导入导出、电脑/ntfy 连通性、手机配置生成 | 新增或修改短信/电脑接收设置、配置保存与校验 |
| `windows\src\MainWindow.Email.cs` | 邮箱组件装配、保存/提示事件接入、应用状态转发 | 邮箱页面与主窗口的接入边界 |
| `windows\src\EmailSettingsPage.cs` | 邮箱控件绑定、页面草稿、服务商预设、关键词、校验与可取消的连接测试 | 邮箱设置项、页面交互、连接测试生命周期 |
| `windows\src\MainWindow.Security.cs` | 密码锁、自动锁定状态和密钥字段创建 | 密码锁、自动锁定和令牌输入 |
| `windows\src\MainWindow.History.cs` | 记录频道筛选、历史列表、行操作菜单、单条删除、原文窗口和 toast 提示 | 记录列表、复制验证码、单条删除、原文显示和页面提示 |
| `windows\src\MainWindow.About.cs` | 脱敏诊断窗口、GitHub 页面、版本信息和检查更新 | 关于页、诊断预览、更新提示 |
| `windows\src\MainWindow.Configuration.cs` | 分区草稿读取、字段错误与提交反馈 | 校验保留输入、按区提交 |
| `windows\src\MainWindow.Usability.cs` | 测试取消与结果版本、快捷键、紧凑布局、最新验证码卡片和合并刷新 | 交互生命周期与可访问性 |
| `windows\src\SettingsCommit.cs` | 配置分区合并、后台串行队列、持久化与启动项事务 | 保留邮件水位、失败恢复、退出排空 |
| `windows\src\MessageDelivery.cs` | 短信封装、稳定身份、ntfy 检查点与事件去重 | 双通道幂等、断线补收 |
| `windows\src\RuntimeStatus.cs` | 连接/操作状态、临时暂停复制 | 状态展示契约 |
| `windows\src\ConnectionProbe.cs` | 可取消的网络测试、结构化结果 | HTTP/ntfy 测试与代理一致性 |
| `windows\src\PasswordSettingsDialog.cs` | 单窗口密码设置、修改和关闭 | 密码验证、保存失败保留原状态 |
| `windows\src\UiAppearance.cs` | 系统高对比度模板、减少动画预处理 | 系统颜色、即时关键帧 |
| `windows\src\MainWindow.Xaml.cs` | 主窗口运行时 XAML：全局样式、控件模板、页面布局、控件 `x:Name` | 颜色、间距、控件模板、页面布局；只改界面结构时优先看这里 |
| `windows\src\SecretField.cs` | 令牌/授权码的掩码输入、显示切换和提交事件 | 新增或修改密钥输入行为 |
| `windows\src\PasswordRevealer.cs` | 锁屏密码框的掩码/明文切换和同步 | 修改锁屏密码输入显示行为 |
| `windows\src\CodepassDialog.cs` | 自绘确认/提示对话框及其按钮、控件模板和动画入口 | 修改程序内提示框外观或按钮行为 |
| `windows\src\PasswordDialog.cs` | 设置/验证程序锁密码的对话框 | 修改密码设置/验证流程的界面 |
| `windows\src\DisplayMotion.cs` | 对话框显示动画和显示器刷新率读取 | 修改弹窗动画或刷新率兼容性 |
| `windows\src\Config.cs` | 配置模型、读取/保存、DPAPI 密钥保护、原子写入、旧配置兼容 | 配置字段、默认值、配置格式、保存失败恢复 |
| `windows\src\ConfigSettings.cs` | 普通配置项元数据、默认值初始化、INI 读写、字段复制、分区合并与数值范围 | 配置共用机制；具体字段的定义在 `Config.cs` 的 `[Setting]` 上 |
| `windows\src\History.cs` | 可见历史、单条删除/清空、过期清理、DPAPI 历史行与记录/凭据统一原子提交 | 记录上限、失败回滚、历史兼容；删除仅移除可见记录，保留接收凭据 |
| `windows\src\ReceiptLedger.cs` | 接收去重、待确认邮件、近期凭据过期、r3/d3 序列化与回滚快照 | 去重时间窗、邮件确认、升级兼容；不独立落盘 |
| `windows\src\EmailImap.cs` | 只读 IMAP 协议、连接/登录、文件夹、邮件头和正文解析 | IMAP 兼容性、FETCH/IDLE、MIME 或正文解析 |
| `windows\src\EmailWatcher.cs` | 邮箱主收件箱和分类目录后台循环、重连、过滤前置检查、状态回调 | 邮箱轮询/IDLE、邮箱状态、邮箱错误处理 |
| `windows\src\QqPopupWatcher.cs` | QQ 新邮件提醒窗口枚举和 UI Automation 标题读取 | QQ 弹窗联动、窗口轮询和兼容性 |
| `windows\src\CodeExtractor.cs` | 短信/邮件验证码提取、通知类短信过滤、自定义关键词和正则校验 | 验证码识别规则、过滤上限和正则超时 |
| `windows\src\Diagnostics.cs` | 搜索匹配、脱敏诊断报告、诊断事件统计和导出 | 诊断报告字段、隐私边界、历史搜索 |
| `windows\src\Updater.cs` | GitHub 最新版本查询和版本比较 | 检查更新地址、版本比较和更新提示 |

## 接收、投递和存储边界

- `Program` 创建 `HttpReceiver`、`NtfyReceiver`、`IncomingDelivery`、`ClipboardService` 实例，通过构造参数提供配置与必要的应用回调。模块管理自己的监听器、请求、计时器和请求号，不通过 `partial Program` 共享全部字段。
- HTTP/ntfy → `IncomingDelivery.ReceiveSms`；邮箱 → `Program.Incoming.SubmitReceivedEmail`。投递先完成持久化，再通过 `Program.PresentIncoming` 排到界面线程；记录已删除、过期、应用退出或邮箱代次失效时停止延迟投递。
- `Program.Clipboard` 提供手动/自动复制；`Dispose` 使旧请求失效并释放消息窗口。HTTP 停止会关闭监听器与未完成连接，ntfy 停止会取消当前请求并唤醒重连等待。
- `History` 独占 `ReceiptLedger`，账本操作在 `History.Gate` 内进行；需要应用提交锁的写入遵循 `Config.SaveGate → History.Gate`。新增记录与确认分别捕获凭据快照，保存失败恢复内存状态。
- 仍使用同一个历史文件与一次 `Config.WriteAtomically`，依次写入 `v3` 可见记录、`r3` 待确认凭据、`d3` 近期去重凭据。保留旧 TSV/v2/v3 与 r3/d3 读取兼容，不产生“记录保存成功、去重凭据保存失败”的分裂提交。
- 单条删除、清空和时间清理不撤销接收凭据。清空/删除成功后取消对应记录的延迟复制，失败保持原记录及有效状态。
- 记录页只保留最新验证码卡片、筛选/搜索、列表与常驻清理设置；行内验证码按记录引用区分最新和旧项，搜索筛选不把旧记录提升为最新。

## MainWindow 拆分边界

### `MainWindow.Xaml.cs`

这里目前只有 `UiXaml` 常量。它仍然是 C# 代码的一部分，不是独立 `.xaml` 文件，因此：

- 不改变 XAML 内容时，运行行为和资源加载方式不变；
- `MainWindow.cs` 通过 `UiAppearance.Prepare` 适配系统外观后，以 `XamlReader.Parse` 创建根 `Grid`；静默启动延迟到首次打开托盘窗口才创建完整界面，剪贴板拥有独立消息窗口；
- 控件的 `x:Name` 是 C# 事件接线和状态同步的契约，修改名称必须同步检查 `MainWindow.cs` 中的 `Find<T>`；
- 只调整布局、样式或控件结构时，通常只需读 `MainWindow.Xaml.cs`，再用控件名搜索 `MainWindow.cs` 的绑定逻辑。

### `MainWindow.cs` 及功能 partial 文件

构造函数、共享字段和窗口生命周期仍在 `MainWindow.cs`；页面行为按下面的文件寻找：

1. `MainWindow.cs`：构造函数与控件初始化、自动保存事件接线、响应式布局、共享窗口/托盘行为；
2. `MainWindow.Settings.cs`：页面切换、普通设置、短信接收关键字标签、配置同步、导入导出、电脑/ntfy 测试；
3. `MainWindow.Email.cs`：装配 `EmailSettingsPage`，接入统一保存队列、提示和状态；邮箱控件、草稿、事件、测试取消及结果版本归 `EmailSettingsPage`；
4. `MainWindow.Security.cs`：密码锁、自动锁定和密钥字段；隐私通知选项的配置同步在 `MainWindow.Settings.cs`；
5. `MainWindow.History.cs`：记录搜索/筛选、复制验证码、原文窗口和 toast；
6. `MainWindow.About.cs`：脱敏诊断、GitHub 和检查更新；
7. 独立辅助类：`SecretField.cs`、`PasswordRevealer.cs`、`CodepassDialog.cs`、`PasswordDialog.cs`、`DisplayMotion.cs`。

## 常见改动的最短路径

| 需求 | 先读 | 再查 |
| --- | --- | --- |
| 输入框/按钮间距、标签位置、窄窗口布局 | `MainWindow.Xaml.cs` | `MainWindow.cs` 中 `InitializeResponsiveForms`、`UpdateResponsiveForms` 和对应控件名 |
| 新增一个设置项 | `Config.cs` 的 `[Setting]` | `MainWindow.Xaml.cs`；邮箱项在 `EmailSettingsPage.cs` 注册 `Bind`，其它页面仍看对应 partial 的加载/草稿入口 |
| 修改邮箱页面交互 | `EmailSettingsPage.cs` | `MainWindow.Xaml.cs`；应用层接入看 `MainWindow.Email.cs`，收件行为看 `EmailWatcher.cs` |
| 新增邮箱服务或调整 IMAP 行为 | `EmailImap.cs`、`EmailWatcher.cs` | `Config.cs`、`EmailSettingsPage.cs` 的邮箱设置同步 |
| 调整验证码识别 | `CodeExtractor.cs` | `IncomingDelivery.cs` 的短信/邮件投递点、相关回归脚本 |
| 修改历史搜索、单条删除或清理 | `History.cs`、`MainWindow.History.cs` | `ReceiptLedger.cs` 的接收凭据、`IncomingDelivery.cs` 的延迟投递门禁；清理分钟数保存入口在 `MainWindow.cs` |
| 修改 HTTP 或 ntfy | `HttpReceiver.cs`、`NtfyReceiver.cs` | `MessageDelivery.cs` 的封装/检查点、`Program.cs` 的启停装配 |
| 修改托盘或剪贴板 | `Program.cs`、`ClipboardService.cs` | 最新记录、请求取消、通知和清理计时器 |
| 修改 QQ 联动 | `QqPopupWatcher.cs` | `EmailWatcher.cs` 的唤醒入口、`Program.cs` 的线程启停 |
| 修改脱敏诊断 | `Diagnostics.cs` | `MainWindow.About.cs` 的预览/导出入口 |

## 验证顺序

1. 先运行 `windows\build.ps1 -OutputDirectory windows\build\<本次检查目录> -SkipPackage`，确认 C# 编译和版本一致性通过。
2. 若改了界面布局，启动构建出的 EXE，检查主窗口、记录页、短信设置、邮箱设置、隐私设置和关于页。
3. 涉及行为或构建产物时，再运行 `windows\tests\Run-All.ps1 -ExePath <本次 EXE>`；不要把临时构建目录当作发布目录。
4. 发布前仍以 `docs\RELEASE_CHECKLIST.md` 为准，尤其是版本、构建元数据、截图证据和附件校验。

新增 `test-delivery-usability.ps1` 覆盖双通道去重、同秒 ntfy 重放、存储失败重试、草稿分区、迟到测试结果和密码校验。`phone/tests/test-forward.sh <独立夹具目录> <forward.sh路径>` 在离线假查询/假发送环境检查批量短信、空发件人、多行正文、独立确认、写入失败与 HTTP 重定向。

## 后续拆分原则

如果继续拆 `MainWindow.cs`，按用户可见页面或完整功能边界组织；已有 partial 可参照 `EmailSettingsPage` 渐进提取为页面组件，由组件管理控件和交互生命周期。每个文件应让维护者能同时看到一页的布局入口和行为入口；拆分不应改变控件名、配置格式、线程生命周期或发布流程。

## 新增普通配置项的最短路径

1. 在 `Config.cs` 声明强类型字段和 `[Setting]`：给出 INI 键、`SettingsSection`、默认值；整数可声明上下限，多行文本使用 `Base64 = true`。默认值初始化、INI 读写、`CopyFrom` 和普通字段的分区合并会自动覆盖该字段。
2. 增加对应 XAML 控件。邮箱页面在 `EmailSettingsPage` 构造函数注册一条 `Bind(配置键, 控件名, 校验标签)`，自动接入回填、草稿读取、分区归属和规则自动保存；连接区保持显式保存。数值校验及邮箱间隔说明使用同一份配置元数据。
3. 添加实际业务消费逻辑和对应行为回归。涉及多个字段的规则在分区读取后校验；邮箱服务商切换、加密字段、导出脱敏、迁移标记、窗口位置及邮箱水位具有专门语义，继续在专用路径处理，不能直接套用普通字段合并。

邮箱组件通过 `SaveRequested`、`ToastRequested`、`FeedbackRequested`、`SimulateRequested` 与宿主交互；连接测试、连接草稿读取及结果展示门禁从构造参数传入。布局仍组合在主窗口 XAML 中，共享主题和响应式表单机制，控件 `x:Name` 保持稳定。

`test-settings-components.ps1` 覆盖普通配置的往返、复制、旧键兼容、分区合并和水位隔离；直接实例化邮箱组件并注入假连接测试，验证草稿、取消、迟到结果和保存事件，已纳入 `Run-All.ps1`。
