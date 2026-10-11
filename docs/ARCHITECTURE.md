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
| `windows\src\Program.cs` | 进程入口、单实例、托盘、HTTP 接收、ntfy 接收、后台线程生命周期，以及把状态通知给界面 | 启动/退出、接收通道、托盘菜单、后台线程启停 |
| `windows\src\MainWindow.cs` | 主窗口共享字段、构造初始化、控件查找/事件接线、响应式布局、窗口/托盘生命周期 | 控件初始化、共享状态、窗口行为、跨页面事件 |
| `windows\src\MainWindow.Settings.cs` | 页面切换、普通设置、配置读取/保存、导入导出、电脑/ntfy 连通性、手机配置生成 | 新增或修改短信/电脑接收设置、配置保存与校验 |
| `windows\src\MainWindow.Email.cs` | 邮箱服务商预设、关键词标签、邮箱预览、状态刷新和连接测试 | 邮箱设置交互、状态显示、邮箱连接测试 |
| `windows\src\MainWindow.Security.cs` | 密码锁、自动锁定状态和密钥字段创建 | 密码锁、自动锁定和令牌输入 |
| `windows\src\MainWindow.History.cs` | 记录频道筛选、历史列表、搜索结果行、原文窗口和 toast 提示 | 记录列表、复制验证码、原文显示和页面提示 |
| `windows\src\MainWindow.About.cs` | 脱敏诊断窗口、GitHub 页面、版本信息和检查更新 | 关于页、诊断预览、更新提示 |
| `windows\src\MainWindow.Xaml.cs` | 主窗口运行时 XAML：全局样式、控件模板、页面布局、控件 `x:Name` | 颜色、间距、控件模板、页面布局；只改界面结构时优先看这里 |
| `windows\src\SecretField.cs` | 令牌/授权码的掩码输入、显示切换和提交事件 | 新增或修改密钥输入行为 |
| `windows\src\PasswordRevealer.cs` | 锁屏密码框的掩码/明文切换和同步 | 修改锁屏密码输入显示行为 |
| `windows\src\CodepassDialog.cs` | 自绘确认/提示对话框及其按钮、控件模板和动画入口 | 修改程序内提示框外观或按钮行为 |
| `windows\src\PasswordDialog.cs` | 设置/验证程序锁密码的对话框 | 修改密码设置/验证流程的界面 |
| `windows\src\DisplayMotion.cs` | 对话框显示动画和显示器刷新率读取 | 修改弹窗动画或刷新率兼容性 |
| `windows\src\Config.cs` | 配置模型、读取/保存、DPAPI 密钥保护、原子写入、旧配置兼容 | 配置字段、默认值、配置格式、保存失败恢复 |
| `windows\src\History.cs` | 短信/邮件历史记录、去重、过期清理、DPAPI 历史行 | 历史格式、记录上限、去重和自动清理 |
| `windows\src\EmailImap.cs` | 只读 IMAP 协议、连接/登录、文件夹、邮件头和正文解析 | IMAP 兼容性、FETCH/IDLE、MIME 或正文解析 |
| `windows\src\EmailWatcher.cs` | 邮箱主收件箱和分类目录后台循环、重连、过滤前置检查、状态回调 | 邮箱轮询/IDLE、邮箱状态、邮箱错误处理 |
| `windows\src\QqPopupWatcher.cs` | QQ 新邮件提醒窗口枚举和 UI Automation 标题读取 | QQ 弹窗联动、窗口轮询和兼容性 |
| `windows\src\CodeExtractor.cs` | 短信/邮件验证码提取、通知类短信过滤、自定义关键词和正则校验 | 验证码识别规则、过滤上限和正则超时 |
| `windows\src\Diagnostics.cs` | 搜索匹配、脱敏诊断报告、诊断事件统计和导出 | 诊断报告字段、隐私边界、历史搜索 |
| `windows\src\Updater.cs` | GitHub 最新版本查询和版本比较 | 检查更新地址、版本比较和更新提示 |

## MainWindow 拆分边界

### `MainWindow.Xaml.cs`

这里目前只有 `UiXaml` 常量。它仍然是 C# 代码的一部分，不是独立 `.xaml` 文件，因此：

- 不改变 XAML 内容时，运行行为和资源加载方式不变；
- `MainWindow.cs` 仍通过 `XamlReader.Parse(UiXaml)` 创建根 `Grid`；
- 控件的 `x:Name` 是 C# 事件接线和状态同步的契约，修改名称必须同步检查 `MainWindow.cs` 中的 `Find<T>`；
- 只调整布局、样式或控件结构时，通常只需读 `MainWindow.Xaml.cs`，再用控件名搜索 `MainWindow.cs` 的绑定逻辑。

### `MainWindow.cs` 及功能 partial 文件

构造函数、共享字段和窗口生命周期仍在 `MainWindow.cs`；页面行为按下面的文件寻找：

1. `MainWindow.cs`：构造函数与控件初始化、自动保存事件接线、响应式布局、共享窗口/托盘行为；
2. `MainWindow.Settings.cs`：页面切换、普通设置、配置同步、导入导出、电脑/ntfy 测试；
3. `MainWindow.Email.cs`：邮箱服务商、关键词、邮箱状态和邮箱连接测试；
4. `MainWindow.Security.cs`：密码锁、自动锁定和密钥字段；隐私通知选项的配置同步在 `MainWindow.Settings.cs`；
5. `MainWindow.History.cs`：记录搜索/筛选、复制验证码、原文窗口和 toast；
6. `MainWindow.About.cs`：脱敏诊断、GitHub 和检查更新；
7. 独立辅助类：`SecretField.cs`、`PasswordRevealer.cs`、`CodepassDialog.cs`、`PasswordDialog.cs`、`DisplayMotion.cs`。

## 常见改动的最短路径

| 需求 | 先读 | 再查 |
| --- | --- | --- |
| 输入框/按钮间距、标签位置、窄窗口布局 | `MainWindow.Xaml.cs` | `MainWindow.cs` 中 `InitializeResponsiveForms`、`UpdateResponsiveForms` 和对应控件名 |
| 新增一个设置项 | `Config.cs` | `MainWindow.Xaml.cs`、`MainWindow.Settings.cs` 的字段/加载/保存/校验；若是邮箱项再看 `MainWindow.Email.cs` |
| 修改邮箱页面交互 | `MainWindow.Email.cs` | `MainWindow.Xaml.cs`、`Config.cs`、`EmailWatcher.cs` |
| 新增邮箱服务或调整 IMAP 行为 | `EmailImap.cs`、`EmailWatcher.cs` | `Config.cs`、`MainWindow.Email.cs` 的邮箱设置同步 |
| 调整验证码识别 | `CodeExtractor.cs` | `Program.cs` 的短信/邮件接收调用点、相关回归脚本 |
| 修改历史搜索或清理 | `History.cs` | `Diagnostics.cs`、`MainWindow.cs` 的记录页调用 |
| 修改托盘、HTTP 或 ntfy | `Program.cs` | `MainWindow.cs` 的状态刷新和退出路径 |
| 修改 QQ 联动 | `QqPopupWatcher.cs` | `EmailWatcher.cs` 的唤醒入口、`Program.cs` 的线程启停 |
| 修改脱敏诊断 | `Diagnostics.cs` | `MainWindow.About.cs` 的预览/导出入口 |

## 验证顺序

1. 先运行 `windows\build.ps1 -OutputDirectory windows\build\<本次检查目录> -SkipPackage`，确认 C# 编译和版本一致性通过。
2. 若改了界面布局，启动构建出的 EXE，检查主窗口、记录页、短信设置、邮箱设置、隐私设置和关于页。
3. 涉及行为或构建产物时，再运行 `windows\tests\Run-All.ps1 -ExePath <本次 EXE>`；不要把临时构建目录当作发布目录。
4. 发布前仍以 `docs\RELEASE_CHECKLIST.md` 为准，尤其是版本、构建元数据、截图证据和附件校验。

## 后续拆分原则

如果继续拆 `MainWindow.cs`，按用户可见页面或完整功能边界拆成更多 `partial` 文件，而不是按“所有事件处理器/所有辅助方法”拆。每个文件应让维护者能同时看到一页的布局入口和行为入口；拆分不应改变控件名、配置格式、线程生命周期或发布流程。
