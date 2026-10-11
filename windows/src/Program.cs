// codepass - SMS verification code -> Windows clipboard
// 目标框架: .NET Framework 4.x (Win10/11 系统自带, 无需安装运行时)
// 编译: 见同目录 build.ps1
//
// 通道:
//   1) 局域网 HTTP:  手机 POST http://<本机IP>:<port>/sms
//                    局域网监听时必须配置令牌（X-Token 或 ?token=）；回环监听可不鉴权
//   2) 公网 ntfy:     订阅 https://ntfy.sh/<topic>  (可选, 外出/移动数据兜底)
//
// 收到后自动提取验证码, 写剪贴板 + 弹气泡 + 记入历史; 托盘图标双击开窗。
// 界面: WPF, Windows 商店(Fluent)圆角风。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using Microsoft.Win32;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using MessageBox = Codepass.CodepassDialog;

namespace Codepass
{
    static class Log
    {
        static readonly object Gate = new object();
        static string _path;
        const long MaxBytes = 2 * 1024 * 1024;

        public static void Init(string p) { _path = p; }

        public static void Write(string msg)
        {
            Diagnostics.ObserveLog(msg);
            try
            {
                lock (Gate)
                {
                    try
                    {
                        FileInfo fi = new FileInfo(_path);
                        if (fi.Exists && fi.Length > MaxBytes) File.WriteAllText(_path, "");
                    }
                    catch { }
                    File.AppendAllText(_path,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine,
                        new UTF8Encoding(true));
                }
            }
            catch { }
        }
    }

    static class Program
    {
        internal static Config cfg;
        internal static string appDir;
        internal static MainWindow mainWindow;

        static System.Windows.Application app;
        static Mutex singleInstance;
        static System.Windows.Forms.NotifyIcon tray;
        static System.Windows.Forms.ContextMenuStrip trayMenu;
        static DispatcherTimer clearTimer;
        static DispatcherTimer historyRetentionTimer;
        static Thread httpThread;
        static TcpListener httpListener;
        static readonly object HttpGate = new object();
        static volatile string pendingClipboardCode;
        static readonly object ClipboardWriteGate = new object();
        static long clipboardRequest;
        internal sealed class LatestReceipt
        {
            public readonly string Code;
            public readonly bool EmailChannel;
            public readonly long ReceivedUtcTicks;
            internal LatestReceipt(string code, bool emailChannel, long receivedUtcTicks)
            { Code = code; EmailChannel = emailChannel; ReceivedUtcTicks = receivedUtcTicks; }
        }
        static volatile LatestReceipt latestReceipt;

        internal static void SetLatestCode(string code, bool emailChannel)
        {
            latestReceipt = string.IsNullOrEmpty(code) ? null : new LatestReceipt(code, emailChannel, DateTime.UtcNow.Ticks);
        }

        internal static LatestReceipt GetLatestReceipt()
        {
            lock (Config.SaveGate)
            {
                if (cfg != null) ExpireLatestReceipt(cfg.HistoryRetentionMinutes, DateTime.UtcNow);
                return latestReceipt;
            }
        }

        internal static void RestoreLatestFromHistory()
        {
            lock (Config.SaveGate)
            {
            HistoryItem item = History.LatestUnexpiredRecord(cfg == null ? 0 : cfg.HistoryRetentionMinutes, DateTime.UtcNow);
            SetLatestRecord(item);
            }
        }
        internal static void SetLatestRecord(HistoryItem item)
        {
            latestReceipt = item == null || string.IsNullOrEmpty(item.Code) ? null
                : new LatestReceipt(item.Code, item.Channel == "email", item.ReceivedUtcTicks);
        }

        internal static void ExpireLatestReceipt(int minutes, DateTime nowUtc)
        {
            lock (Config.SaveGate)
            {
            LatestReceipt latest = latestReceipt;
            if (minutes > 0 && latest != null && latest.ReceivedUtcTicks < nowUtc.AddMinutes(-minutes).Ticks)
                RestoreLatestFromHistory();
            }
        }
        static volatile bool running = true;
        internal static bool StartupSyncFailed { get; private set; }
        internal static bool Running { get { return running; } }

        static int activeClients;
        const int MaxClients = 32;
        static volatile bool httpLanBound;

        static readonly byte[] CrlfCrlf = new byte[] { 13, 10, 13, 10 };
        static readonly byte[] LfLf = new byte[] { 10, 10 };

        static readonly HashSet<string> SeenIds = new HashSet<string>();
        static readonly Queue<string> SeenOrder = new Queue<string>();
        static readonly object SeenGate = new object();

        [STAThread]
        static void Main(string[] args)
        {
            bool silent = IsSilentStartup(args);
            // 单实例, 避免重复启动导致端口冲突
            bool createdNew;
            singleInstance = new Mutex(true, "Codepass_SingleInstance", out createdNew);
            if (!createdNew)
            {
                 if (silent) return;
                 MessageBox.Show(null, "codepass 已经在运行，请从系统托盘打开。", "codepass",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            // 老版 .NET Framework 默认只协商到 TLS 1.0, 访问 ntfy.sh 等站点会 SSL 失败
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            }
            catch { }

            appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Log.Init(Path.Combine(appDir, "app.log"));
            Config.RecycleStaleTemporaryFiles(appDir);

            cfg = Config.Load(Path.Combine(appDir, "config.ini"));
            if (cfg.Port < 1 || cfg.Port > 65535)
            {
                cfg.Port = 8787;
                Log.Write("invalid port in config; fallback to 8787");
            }
            if (!IsHttpsEndpoint(cfg.NtfyServer))
            {
                cfg.NtfyTopic = "";
                cfg.NtfyToken = "";
                Log.Write("ntfy disabled: server is not HTTPS");
            }
            try { SetStartup(cfg.StartWithWindows); }
            catch (Exception ex)
            {
                StartupSyncFailed = true;
                Log.Write("startup setting sync failed: " + ex.Message);
            }
            if (args != null)
            {
                foreach (string a in args)
                    if (string.Equals(a, "--dry", StringComparison.OrdinalIgnoreCase)) cfg.Dry = true;
            }
            History.Init(Path.Combine(appDir, "history.txt"));
            try { History.PruneExpired(cfg.HistoryRetentionMinutes, DateTime.UtcNow); }
            catch (IOException) { Log.Write("history expiry pending: storage failure"); }
            RestoreLatestFromHistory();

            Log.Write("========== start ==========");
            Log.Write("port=" + cfg.Port
                + " ntfy=" + (cfg.NtfyTopic.Length == 0 ? "(off)" : "(on)")
                + " token=" + (cfg.Token.Length == 0 ? "(off)" : "(on)")
                + " ntfy_token=" + (cfg.NtfyToken.Length == 0 ? "(off)" : "(on)")
                + " email=" + (cfg.EmailEnabled ? "(on)" : "(off)")
                + " qq_popup=" + (cfg.QqPopupEnabled ? "(on)" : "(off)")
                + " dry=" + cfg.Dry);

            // WinForms 托盘菜单需要视觉样式, 否则右键菜单是经典外观
            try { System.Windows.Forms.Application.EnableVisualStyles(); } catch { }

            app = new System.Windows.Application();
            app.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
            app.SessionEnding += new System.Windows.SessionEndingCancelEventHandler(
                 delegate(object s, System.Windows.SessionEndingCancelEventArgs e)
                 {
                     // 注销/关机时不要因为"关闭即隐藏"而挡住系统
                    if (mainWindow != null && !mainWindow.ExitApp())
                    {
                        e.Cancel = true;
                        return;
                    }
                    running = false;
                 });

            try
            {
                mainWindow = new MainWindow(cfg, Path.Combine(appDir, "config.ini"));
                PrepareMainWindow(mainWindow, silent);
            }
            catch (Exception ex)
            {
                Log.Write("UI init error: " + ex.ToString());
                 MessageBox.Show(null, "codepass 界面初始化失败：\n" + ex.Message, "codepass",
                     System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            BuildTray();
            StartHttp();
            if (cfg.NtfyTopic.Length > 0) StartNtfy();
            RefreshEmailWatchers();

            Log.Write("started.");
            historyRetentionTimer = new DispatcherTimer();
            historyRetentionTimer.Interval = TimeSpan.FromSeconds(15);
            historyRetentionTimer.Tick += delegate { ApplyHistoryRetention(); };
            historyRetentionTimer.Start();
            RunMainWindow(app, mainWindow, silent);

            running = false;
            StopWatchersForExit();
            try { if (clearTimer != null) { clearTimer.Stop(); clearTimer = null; } } catch { }
            try { if (historyRetentionTimer != null) { historyRetentionTimer.Stop(); historyRetentionTimer = null; } } catch { }
            try { if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; } } catch { }
            try { if (trayMenu != null) { trayMenu.Dispose(); trayMenu = null; } } catch { }
            try { if (singleInstance != null) singleInstance.ReleaseMutex(); } catch { }
            Log.Write("========== exit ==========");
        }

        internal static bool QuitApp()
        {
            if (mainWindow != null && !mainWindow.ExitApp()) return false;
            running = false;
            if (app != null) app.Shutdown();
            return true;
        }

        internal static void SetStartup(bool enabled)
        {
            const string runPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(runPath))
            {
                if (key == null) throw new InvalidOperationException("无法打开当前用户启动项");
                if (enabled)
                {
                    string exe = Assembly.GetExecutingAssembly().Location;
                    if (String.IsNullOrEmpty(exe)) throw new InvalidOperationException("无法确定程序路径");
                    key.SetValue("codepass", StartupCommand(exe), RegistryValueKind.String);
                }
                else
                {
                    key.DeleteValue("codepass", false);
                }
            }
            StartupSyncFailed = false;
        }

        internal static string StartupCommand(string exe) { return "\"" + exe + "\" --startup"; }

        internal static bool IsSilentStartup(string[] args)
        {
            if (args != null) foreach (string arg in args)
                if (string.Equals(arg, "--startup", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        internal static void RunMainWindow(System.Windows.Application application, MainWindow window, bool silent)
        {
            PrepareMainWindow(window, silent);
            application.MainWindow = window;
            if (silent) { window.ShowInTaskbar = false; application.Run(); }
            else application.Run(window);
        }

        internal static void PrepareMainWindow(MainWindow window, bool silent)
        {
            if (silent) window.ShowInTaskbar = false;
            new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
        }

        internal static void ApplyHistoryRetention()
        {
            try
            {
                if (cfg != null && History.PruneExpired(cfg.HistoryRetentionMinutes, DateTime.UtcNow) > 0 && mainWindow != null)
                {
                    if (mainWindow.Dispatcher.CheckAccess()) mainWindow.RefreshHistory();
                    else mainWindow.Dispatcher.BeginInvoke(new Action(delegate { mainWindow.RefreshHistory(); }));
                }
            }
            catch (IOException)
            {
                if (cfg != null) ExpireLatestReceipt(cfg.HistoryRetentionMinutes, DateTime.UtcNow);
                EmailStatusChanged();
            }
        }

        static void BuildTray()
        {
            tray = new System.Windows.Forms.NotifyIcon();
            try
            {
                string executable = Assembly.GetExecutingAssembly().Location;
                Icon appIcon = Icon.ExtractAssociatedIcon(executable);
                tray.Icon = appIcon == null ? (Icon)SystemIcons.Application.Clone() : (Icon)appIcon.Clone();
                if (appIcon != null) appIcon.Dispose();
            }
            catch { tray.Icon = (Icon)SystemIcons.Application.Clone(); }
             tray.Text = "codepass";
            tray.Visible = true;

            System.Windows.Forms.ContextMenuStrip menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("复制最新验证码", null, new EventHandler(delegate(object s, EventArgs e) { CopyLatestCode(); }));
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add("退出", null, new EventHandler(delegate(object s, EventArgs e) { QuitApp(); }));
            tray.ContextMenuStrip = menu;
            trayMenu = menu;
            tray.DoubleClick += new EventHandler(delegate(object s, EventArgs e) { if (mainWindow != null) mainWindow.ShowFromTray(); });
        }

        // 枚举本机局域网 IPv4, 优先有线/无线网卡
        // 枚举本机局域网 IPv4（有线/无线网卡优先，去重，保持枚举顺序）
        internal static List<string> DetectLocalIpList()
        {
            List<string> fallback = new List<string>();
            List<string> preferred = new List<string>();
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        if (IPAddress.IsLoopback(ip.Address)) continue;
                        string s = ip.Address.ToString();
                        if (s.StartsWith("169.254.")) continue;   // 链路本地
                        if (fallback.Contains(s)) continue;
                        fallback.Add(s);
                        if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                            || ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                            preferred.Add(s);
                    }
                }
            }
            catch { }

            List<string> all = new List<string>();
            all.AddRange(preferred);
            foreach (string s in fallback)
            {
                if (!all.Contains(s)) all.Add(s);
            }
            return all;
        }

        // 默认取第一个（结果与旧版一致）
        internal static string DetectLocalIp()
        {
            try
            {
                List<string> fallback = new List<string>();
                List<string> preferred = new List<string>();
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        if (IPAddress.IsLoopback(ip.Address)) continue;
                        string s = ip.Address.ToString();
                        if (s.StartsWith("169.254.")) continue;   // 链路本地
                        fallback.Add(s);
                        if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                            || ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                            preferred.Add(s);
                    }
                }
                if (preferred.Count > 0) return preferred[0];
                if (fallback.Count > 0) return fallback[0];
            }
            catch { }
            return "127.0.0.1";
        }

        // ---------- 核心: 收到文本 -> 提取验证码 -> 记录 + 写剪贴板 ----------

        internal static void HandleIncoming(string text, string src)
        {
            HandleIncoming(text, src, "");
        }

        internal static void HandleIncoming(string text, string src, string sender)
        {
            HandleIncoming(text, src, sender, true, true, false);
        }

        /// <summary>邮箱/弹窗来源的验证码：按邮箱设置决定是否弹提示与自动复制。</summary>
        internal static void HandleEmail(string text, string sender, string src)
        {
            HandleIncoming(text, src, sender, cfg.EmailAutoCopy, cfg.EmailNotify, true);
        }

        internal static void SubmitEmail(string text, string sender, string code, string messageKey, Config identity,
            bool allowAutoCopy, Func<bool> canCommit, string mailboxKey = null, long mailUid = 0, bool updateLatest = true)
        {
            DeliverIncoming(text, "email", sender, identity.EmailAutoCopy && allowAutoCopy, identity.EmailNotify,
                true, code, messageKey, identity, canCommit, mailboxKey, mailUid, updateLatest);
        }

        internal static void SubmitReceivedEmail(string text, string sender, string code, string messageKey, Config identity,
            bool allowAutoCopy, Func<bool> canCommit, string mailboxKey, long mailUid, bool updateLatest, DateTime receivedUtc)
        {
            DeliverIncoming(text, "email", sender, identity.EmailAutoCopy && allowAutoCopy, identity.EmailNotify,
                true, code, messageKey, identity, canCommit, mailboxKey, mailUid, updateLatest, receivedUtc);
        }

        internal static void HandleIncoming(string text, string src, string sender,
            bool autoCopy, bool notify, bool emailChannel)
        {
            lock (Config.SaveGate)
            {
            if (!emailChannel && SmsExceedsLengthLimit(text, cfg.SmsMaxLength))
            {
                Log.Write("[" + src + "] SMS skipped: length limit exceeded");
                return;
            }
            if (!emailChannel && CodeExtractor.IsFiltered(text, cfg.FilterKeywords, cfg.FilterRegex))
            {
                Log.Write("[" + src + "] message filtered");
                return;
            }
            string code = CodeExtractor.Extract(text, cfg.MinLen, cfg.MaxLen);
            if (code == null)
            {
                Log.Write("[" + src + "] 未识别到验证码");
                return;
            }
            DeliverIncoming(text, src, sender, autoCopy, notify, emailChannel, code, "", cfg, null);
            }
        }

        // 按字符计数；合法代理对（如 emoji）算一个字符，空格与换行也计入。
        internal static bool SmsExceedsLengthLimit(string text, int limit)
        {
            if (text == null) return false;
            if (limit < 1 || limit > Config.MaximumSmsMaxLength) limit = Config.DefaultSmsMaxLength;
            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) i++;
                if (++count > limit) return true;
            }
            return false;
        }

        static void DeliverIncoming(string text, string src, string sender, bool autoCopy, bool notify, bool emailChannel,
            string code, string messageKey, Config identity, Func<bool> canCommit, string mailboxKey = null, long mailUid = 0, bool updateLatest = true, DateTime? receivedUtc = null)
        {
            lock (Config.SaveGate)
            {
            if (canCommit != null && !canCommit()) return;
            Log.Write("[" + src + "] code recognized");
            if (cfg.Dry) { Log.Write("[dry] skip clipboard"); return; }

            HistoryItem it = new HistoryItem();
            DateTime received = receivedUtc.HasValue ? receivedUtc.Value.ToUniversalTime() : DateTime.UtcNow;
            if (received > DateTime.UtcNow) received = DateTime.UtcNow;
            it.ReceivedUtcTicks = received.Ticks;
            it.Time = received.ToLocalTime().ToString("MM-dd HH:mm:ss");
            it.Source = emailChannel ? (MailRules.SenderAddress(sender) ?? "未知发件人") : History.ExtractSender(text, sender);
            it.MessageKey = messageKey;
            it.MailboxKey = mailboxKey; it.MailUid = mailUid;
            it.CanBeLatest = updateLatest;
            it.Code = code;
            it.Channel = emailChannel ? "email" : "sms";
            string historyText = text ?? "";
            // 正文读取已限制为 20000 字符；额外保留主题，不再截成 1000 字符预览。
            if (emailChannel && !identity.EmailSaveBody) historyText = "";
            else if (emailChannel && historyText.Length > 24000) historyText = historyText.Substring(0, 24000) + "\n（内容过长，后续未保存）";
            it.Raw = historyText;
            if (!History.AddUnique(it)) return;
            if (updateLatest) SetLatestRecord(it);
            ApplyHistoryRetention();
            if (History.IsExpired(it, cfg.HistoryRetentionMinutes, DateTime.UtcNow)) return;
            Func<bool> canDeliver = delegate
            {
                lock (Config.SaveGate) return !History.IsExpired(it, cfg.HistoryRetentionMinutes, DateTime.UtcNow)
                    && (canCommit == null || canCommit());
            };

            if (mainWindow == null) return;

            try
            {
                mainWindow.Dispatcher.BeginInvoke(new Action(delegate
                {
                    // 委托体在 UI 线程执行, 必须自行兜底异常
                    try
                    {
                        lock (Config.SaveGate)
                        {
                        if (!canDeliver()) return;
                        mainWindow.OnNewRecord(it);
                        if (autoCopy)
                        {
                            // 剪贴板写入放后台 STA 线程重试（最长约 10 秒）：第三方程序
                            // （游戏加速器、剪贴板工具等）短期占用时不至于复制失败，也不阻塞界面。
                            StartClipboardWrite(code, text, notify, canDeliver, emailChannel);
                        }
                        else if (notify && (emailChannel || cfg.ShowTip) && tray != null)
                        {
                            tray.ShowBalloonTip(2500, "验证码已收到", ChannelNotificationText(code, text, emailChannel),
                                System.Windows.Forms.ToolTipIcon.Info);
                        }
                        }
                    }
                    catch (Exception e) { Log.Write("ui error: " + e.Message); }
                }));
            }
            catch (Exception e) { Log.Write("invoke error: " + e.Message); }
            }
        }

        // ---------- 邮箱接收 / QQ 弹窗联动 ----------

        static Thread emailThread, emailJunkThread, qqPopupThread;
        static string emailRuntimeSig = "";

        internal static bool EmailConfigured()
        {
            return cfg.EmailEnabled
                && cfg.EmailServer.Trim().Length > 0
                && cfg.EmailAccount.Trim().Length > 0
                && !cfg.EmailPasswordRequired && cfg.EmailAuth.Trim().Length > 0;
        }

        /// <summary>邮箱线程保存水位线用；与界面保存共用 Config 内部锁。</summary>
        internal static void SaveConfigQuiet()
        {
            try { cfg.Save(Path.Combine(appDir, "config.ini")); }
            catch (Exception e) { Log.Write("config save failed: " + e.Message); }
        }

        internal static void EmailStatusChanged()
        {
            if (mainWindow == null) return;
            try { mainWindow.Dispatcher.BeginInvoke(new Action(delegate { mainWindow.UpdateEmailStatus(); })); } catch { }
        }

        /// <summary>
        /// 设置变化后重设邮箱与弹窗监测线程；启用/停用/改配置都无需重启程序。
        /// 旧线程靠“生成号”自查退出（RequestStop 递增），不在 UI 线程等待，避免界面卡顿。
        /// </summary>
        internal static void RefreshEmailWatchers()
        {
            string sig = EmailConfigured() ? EmailSignature(cfg) : "";
            bool emailAlive = emailThread != null && emailThread.IsAlive;
            bool foldersAlive = emailJunkThread != null && emailJunkThread.IsAlive;
            if (sig != emailRuntimeSig || (sig.Length > 0 && (!emailAlive || !foldersAlive)))
            {
                EmailWatcher.RequestStop();
                EmailWatcher.RequestStopJunk();
                emailThread = null;
                emailJunkThread = null;
                emailRuntimeSig = sig;
                if (sig.Length > 0)
                {
                    int gen = EmailWatcher.BeginMainGeneration();
                    emailThread = StartWorker("email", delegate { EmailWatcher.MainLoop(gen); }, false);
                    int junkGen = EmailWatcher.BeginJunkGeneration();
                    emailJunkThread = StartWorker("email-folders", delegate { EmailWatcher.JunkLoop(junkGen); }, false);
                }
            }

            EmailStatusChanged();
            if (cfg.QqPopupEnabled)
            {
                if (qqPopupThread == null || !qqPopupThread.IsAlive)
                {
                    int gen = QqPopupWatcher.BeginGeneration();
                    qqPopupThread = StartWorker("qq-popup", delegate { QqPopupWatcher.Loop(gen); }, true);
                }
            }
            else
            {
                QqPopupWatcher.RequestStop();
                qqPopupThread = null;
            }
        }

        static Thread StartWorker(string name, ThreadStart body, bool sta)
        {
            return StartWorkerCore(name, body, sta);
        }

        internal static string EmailSignature(Config config)
        {
            return config.EmailServer + "\n" + config.EmailAccount + "\n" + config.EmailAuth
                + "\n" + config.EmailCheckJunk + "\n" + config.EmailPollSeconds + "\n" + config.EmailKeywords + "\n" + config.EmailSenders
                + "\n" + config.EmailAutoCopy + "\n" + config.EmailNotify + "\n" + config.EmailSaveBody
                + "\n" + config.MinLen + "\n" + config.MaxLen;
        }

        static Thread StartWorkerCore(string name, ThreadStart body, bool sta)
        {
            Thread t = new Thread(body);
            t.IsBackground = true;
            t.Name = name;
            if (sta)
            {
                try { t.SetApartmentState(ApartmentState.STA); } catch { }
            }
            t.Start();
            return t;
        }

        /// <summary>退出前停止监测线程，避免恰逢保存配置时进程退出截断文件。</summary>
        internal static void StopWatchersForExit()
        {
            EmailWatcher.RequestStop();
            EmailWatcher.RequestStopJunk();
            QqPopupWatcher.RequestStop();
            try { if (emailThread != null && emailThread.IsAlive) emailThread.Join(1500); } catch { }
            try { if (emailJunkThread != null && emailJunkThread.IsAlive) emailJunkThread.Join(1500); } catch { }
            try
            {
                TcpListener listener;
                lock (HttpGate) listener = httpListener;
                if (listener != null) listener.Stop();
                if (httpThread != null && httpThread.IsAlive && Thread.CurrentThread != httpThread) httpThread.Join(1500);
            }
            catch { }
            try
            {
                Thread qq = qqPopupThread;
                if (qq != null && qq.IsAlive && Thread.CurrentThread != qq) qq.Join(1500);
            }
            catch { }
        }

        static string Shorten(string s)
        {
            if (s == null) return "";
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.Length > 120 ? s.Substring(0, 120) + "..." : s;
        }

        static string NotificationText(string code, string raw)
        {
            return ChannelNotificationText(code, raw, false);
        }

        internal static string ChannelNotificationText(string code, string raw, bool emailChannel)
        {
            string body;
            int mode = emailChannel ? cfg.EmailPrivacyMode : cfg.PrivacyMode;
            if (mode == 2)
            {
                body = Shorten(raw);
                if (body.Length == 0) body = code;
            }
            else if (mode == 1)
            {
                // 在截断前掩码，避免通知末尾留下验证码的一部分。
                body = Shorten(MaskCode(raw, code));
                if (body.Length == 0) body = "收到一条新的验证码（内容已隐藏）";
            }
            else
            {
                body = "收到一条新的验证码";
            }
            return body;
        }

        static string MaskCode(string text, string code)
        {
            if (text == null || text.Length == 0) return "";
            // JSON 支持转义验证码，原始表示未必包含已解码的完整验证码。
            // 无法可靠定位时隐藏内容，而不是显示可能含部分验证码的原文。
            if (string.IsNullOrEmpty(code) || CodeExtractor.JsonGetString(text, "code") != null
                || text.IndexOf(code, StringComparison.Ordinal) < 0) return "";
            return text.Replace(code, new string('*', code.Length));
        }

        static void CopyLatestCode()
        {
            LatestReceipt receipt = GetLatestReceipt();
            string code = receipt == null ? null : receipt.Code;
            if (String.IsNullOrEmpty(code))
            {
                if (tray != null)
                    tray.ShowBalloonTip(1800, "codepass", "暂无可复制的验证码", System.Windows.Forms.ToolTipIcon.Info);
                return;
            }
            StartClipboardWrite(code, code, true, delegate
            {
                lock (Config.SaveGate) return latestReceipt == receipt
                    && (cfg.HistoryRetentionMinutes == 0 || receipt.ReceivedUtcTicks >= DateTime.UtcNow.AddMinutes(-cfg.HistoryRetentionMinutes).Ticks);
            }, receipt.EmailChannel);
        }

        static bool IsHttpsEndpoint(string value)
        {
            Uri uri;
            return Uri.TryCreate(value ?? "", UriKind.Absolute, out uri)
                && uri.Scheme == Uri.UriSchemeHttps && uri.Host.Length > 0
                && uri.UserInfo.Length == 0;
        }

        static void ScheduleClear(uint sequence, long request)
        {
            if (clearTimer != null) { clearTimer.Stop(); clearTimer = null; }
            if (cfg.AutoClearSeconds <= 0) return;

            // 局部捕获本实例, 避免旧 timer 的排队 Tick 误停新 timer
            DispatcherTimer t = new DispatcherTimer();
            clearTimer = t;
            t.Interval = TimeSpan.FromSeconds(cfg.AutoClearSeconds);
            t.Tick += delegate(object s, EventArgs e)
            {
                t.Stop();
                if (clearTimer == t) clearTimer = null;
                lock (Config.SaveGate)
                lock (ClipboardWriteGate)
                {
                    if (request != clipboardRequest || sequence == 0 || GetClipboardSequenceNumber() != sequence) return;
                    if (!OpenClipboard(new System.Windows.Interop.WindowInteropHelper(mainWindow).Handle)) return;
                    try { if (GetClipboardSequenceNumber() == sequence) EmptyClipboard(); }
                    finally { CloseClipboard(); }
                }
            };
            t.Start();
        }

        // ---------- 剪贴板写入 (Win32 直写 CF_UNICODETEXT, 不依赖延迟渲染) ----------

        [DllImport("user32.dll", SetLastError = true)] static extern bool OpenClipboard(IntPtr hWndNewOwner);
        [DllImport("user32.dll", SetLastError = true)] static extern bool EmptyClipboard();
        [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
        [DllImport("user32.dll", SetLastError = true)] static extern IntPtr GetClipboardData(uint uFormat);
        [DllImport("user32.dll", SetLastError = true)] static extern bool CloseClipboard();
        [DllImport("user32.dll")] static extern uint GetClipboardSequenceNumber();
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr GlobalLock(IntPtr hMem);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool GlobalUnlock(IntPtr hMem);
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr GlobalFree(IntPtr hMem);

        const uint CF_UNICODETEXT = 13;
        const uint GMEM_MOVEABLE = 0x0002;

        /// <summary>
        /// 写验证码到剪贴板：后台 STA 重试最长约 10 秒，以请求号与提交锁串行写入。
        /// 新请求或停止监测会使旧请求失效；清理仅作用于未被修改的本次写入。
        /// </summary>
        static void StartClipboardWrite(string code, string text, bool notify)
        {
            StartClipboardWrite(code, text, notify, null, false);
        }

        internal static bool CopyManually(string text, IntPtr owner)
        {
            lock (Config.SaveGate)
            lock (ClipboardWriteGate)
            {
                ++clipboardRequest;
                pendingClipboardCode = text;
                if (clearTimer != null) { clearTimer.Stop(); clearTimer = null; }
                uint sequence; string error;
                return SetClipboardTextWin32(text, owner, out sequence, out error);
            }
        }

        static void StartClipboardWrite(string code, string text, bool notify, Func<bool> canCommit, bool emailChannel)
        {
            long request;
            lock (Config.SaveGate)
            lock (ClipboardWriteGate) { request = ++clipboardRequest; pendingClipboardCode = code; }
            IntPtr owner = mainWindow == null ? IntPtr.Zero : new System.Windows.Interop.WindowInteropHelper(mainWindow).Handle;
            Thread worker = new Thread(delegate()
            {
                bool ok = false;
                uint sequence = 0;
                string lastError = null;
                int tries = 0;
                int deadline = unchecked(Environment.TickCount + 10000);
                while (!ok)
                {
                    if (request != Interlocked.Read(ref clipboardRequest) || (canCommit != null && !canCommit()))
                    {
                        Log.Write("clipboard: superseded, stop retry");
                        return;
                    }
                    tries++;
                    string error = null;
                    try
                    {
                        lock (Config.SaveGate)
                        lock (ClipboardWriteGate)
                        {
                            if (request != clipboardRequest || (canCommit != null && !canCommit())) return;
                            if (SetClipboardTextWin32(code, owner, out sequence, out error)) ok = true;
                        }
                        if (ok) break;
                    }
                    catch (Exception ex)
                    {
                        // P/Invoke 意外异常绝不能让后台线程崩溃；按失败重试处理。
                        error = ex.GetType().Name + ": " + ex.Message;
                    }
                    lastError = error;
                    int remaining = unchecked(deadline - Environment.TickCount);
                    if (remaining <= 0) break;
                    int delay = tries <= 2 ? 150 : (tries <= 10 ? 300 : 600);
                    if (delay > remaining) delay = (int)remaining;
                    Thread.Sleep(delay);
                }

                if (ok) Log.Write("clipboard write succeeded (tries=" + tries + ")");
                else Log.Write("clipboard error: " + (lastError == null ? "unknown" : lastError)
                    + " (tries=" + tries + ", window=10s)");

                // 期间已有更新的验证码：不再弹本次通知，避免旧提示干扰。
                if (request != Interlocked.Read(ref clipboardRequest))
                {
                    Log.Write("clipboard: superseded, skip notification");
                    return;
                }

                if (mainWindow == null) return;
                try
                {
                    mainWindow.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        try
                        {
                            lock (Config.SaveGate)
                            {
                            if (request != Interlocked.Read(ref clipboardRequest)) return;
                            CompleteClipboardClear(ok, sequence, request);
                            if (canCommit != null && !canCommit()) return;
                            if (notify && (emailChannel || cfg.ShowTip) && tray != null)
                            {
                                if (ok)
                                    tray.ShowBalloonTip(2500, "验证码已复制", ChannelNotificationText(code, text, emailChannel),
                                        System.Windows.Forms.ToolTipIcon.Info);
                                else
                                    tray.ShowBalloonTip(2500, "验证码已收到，复制失败",
                                        "未能写入剪贴板（可能被其他程序占用），请到记录页手动复制。",
                                        System.Windows.Forms.ToolTipIcon.Warning);
                            }
                            }
                        }
                        catch (Exception e) { Log.Write("ui error: " + e.Message); }
                    }));
                }
                catch (Exception e) { Log.Write("invoke error: " + e.Message); }
            });
            worker.IsBackground = true;
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
        }

        internal static void CompleteClipboardClear(bool succeeded, uint sequence, long request)
        {
            lock (Config.SaveGate)
            {
                if (succeeded && request == Interlocked.Read(ref clipboardRequest) && cfg.AutoClearSeconds > 0)
                    ScheduleClear(sequence, request);
            }
        }

        /// <summary>
        /// 用 Win32 直接写入 CF_UNICODETEXT：立即拷贝、不依赖延迟渲染。
        /// 先分配并填充内存再清空剪贴板：分配/加锁失败不会清空原内容；
        /// 写入环节失败会清空原内容，由上层重试补救。
        /// </summary>
        static bool SetClipboardTextWin32(string text, IntPtr owner, out uint sequence, out string error)
        {
            error = null;
            sequence = 0;
            if (owner == IntPtr.Zero || !OpenClipboard(owner))
            {
                error = "OpenClipboard failed, win32=" + Marshal.GetLastWin32Error();
                return false;
            }
            IntPtr hMem = IntPtr.Zero;
            try
            {
                byte[] payload = Encoding.Unicode.GetBytes(text + "\0");
                hMem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)payload.Length);
                if (hMem == IntPtr.Zero)
                {
                    error = "GlobalAlloc failed, win32=" + Marshal.GetLastWin32Error();
                    return false;
                }
                IntPtr target = GlobalLock(hMem);
                if (target == IntPtr.Zero)
                {
                    error = "GlobalLock failed, win32=" + Marshal.GetLastWin32Error();
                    return false;
                }
                try { Marshal.Copy(payload, 0, target, payload.Length); }
                finally { GlobalUnlock(hMem); }

                if (!EmptyClipboard())
                {
                    error = "EmptyClipboard failed, win32=" + Marshal.GetLastWin32Error();
                    return false;
                }
                if (SetClipboardData(CF_UNICODETEXT, hMem) == IntPtr.Zero)
                {
                    error = "SetClipboardData failed, win32=" + Marshal.GetLastWin32Error();
                    return false;
                }
                hMem = IntPtr.Zero;   // 所有权已交给系统
                sequence = GetClipboardSequenceNumber();
                return true;
            }
            finally
            {
                if (hMem != IntPtr.Zero) GlobalFree(hMem);
                CloseClipboard();
            }
        }

        /// <summary>校验剪贴板当前是否已经是目标验证码（写入报错后的兜底判断）。</summary>
        static bool ClipboardTextMatchesWin32(string expected)
        {
            if (!OpenClipboard(IntPtr.Zero)) return false;
            try
            {
                IntPtr handle = GetClipboardData(CF_UNICODETEXT);
                if (handle == IntPtr.Zero) return false;
                IntPtr ptr = GlobalLock(handle);
                if (ptr == IntPtr.Zero) return false;
                try
                {
                    string actual = Marshal.PtrToStringUni(ptr);
                    return string.Equals(actual, expected, StringComparison.Ordinal);
                }
                finally { GlobalUnlock(handle); }
            }
            catch { return false; }
            finally { CloseClipboard(); }
        }

        // ---------- 局域网 HTTP 服务 (TcpListener 手工解析, 免管理员/urlacl) ----------

        static void StartHttp()
        {
            Thread t = new Thread(HttpLoop);
            t.IsBackground = true;
            t.Name = "http";
            httpThread = t;
            t.Start();
        }

        static void HttpLoop()
        {
            TcpListener listener;
            IPAddress listenAddress = ResolveListenAddress();
            httpLanBound = !IPAddress.IsLoopback(listenAddress);
            try
            {
                listener = new TcpListener(listenAddress, cfg.Port);
                lock (HttpGate) httpListener = listener;
                listener.Start();
                Log.Write("HTTP listening on " + listenAddress + ":" + cfg.Port
                    + " token=" + (cfg.Token.Length == 0 ? "off" : "on"));
            }
            catch (Exception e)
            {
                Log.Write("HTTP listen FAILED: " + e.Message + "  (端口被占用?)");
                return;
            }

            while (running)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(new WaitCallback(HandleClient), client);
                }
                catch (Exception e)
                {
                    if (running) { Log.Write("accept error: " + e.Message); Thread.Sleep(300); }
                }
            }
            try { listener.Stop(); } catch { }
            lock (HttpGate) { if (httpListener == listener) httpListener = null; }
        }

        // 没有令牌时只绑定回环，避免把未鉴权接口暴露到局域网。
        // 有令牌时，空 PC_IP 绑定所有 IPv4 网卡，填写 PC_IP 则只绑定该地址。
        static IPAddress ResolveListenAddress()
        {
            IPAddress configured;
            if (cfg.Token.Length == 0)
            {
                return IPAddress.Loopback;
            }
            if (String.IsNullOrWhiteSpace(cfg.PcIp))
            {
                return IPAddress.Any;
            }
            if (!IPAddress.TryParse(cfg.PcIp.Trim(), out configured)
                || configured.AddressFamily != AddressFamily.InterNetwork
                || IPAddress.IsLoopback(configured)
                || configured.Equals(IPAddress.Any))
            {
                Log.Write("HTTP security: invalid LAN address; binding 127.0.0.1 instead");
                return IPAddress.Loopback;
            }
            return configured;
        }

        static void HandleClient(object state)
        {
            TcpClient client = (TcpClient)state;
            if (Interlocked.Increment(ref activeClients) > MaxClients)
            {
                Interlocked.Decrement(ref activeClients);
                try { client.Close(); } catch { }
                return;
            }
            try
            {
                client.ReceiveTimeout = 8000;
                client.SendTimeout = 8000;
                client.NoDelay = true;
                using (client)
                using (NetworkStream ns = client.GetStream())
                {
                    string head, body;
                    if (!ReadHttp(ns, out head, out body)) return;

                    string requiredToken = cfg.Token;
                    if (httpLanBound && requiredToken.Length == 0)
                    {
                        Log.Write("[http] 拒绝: 局域网监听未配置 token");
                        byte[] denied = Encoding.ASCII.GetBytes("HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                        ns.Write(denied, 0, denied.Length);
                        return;
                    }
                    if (requiredToken.Length > 0 && !TokenOk(head, requiredToken))
                    {
                        Log.Write("[http] 拒绝: token 不匹配");
                        byte[] denied = Encoding.UTF8.GetBytes("{\"ok\":false,\"err\":\"token\"}");
                        string dresp = "HTTP/1.1 403 Forbidden\r\n"
                                     + "Content-Type: application/json\r\n"
                                     + "Content-Length: " + denied.Length + "\r\n"
                                     + "Connection: close\r\n\r\n";
                        byte[] dh = Encoding.ASCII.GetBytes(dresp);
                        ns.Write(dh, 0, dh.Length);
                        ns.Write(denied, 0, denied.Length);
                        ns.Flush();
                        return;
                    }

                     if (IsHealthRequest(head))
                     {
                         byte[] health = Encoding.UTF8.GetBytes("{\"ok\":true}");
                         string healthResp = "HTTP/1.1 200 OK\r\n"
                                           + "Content-Type: application/json; charset=utf-8\r\n"
                                           + "Content-Length: " + health.Length + "\r\n"
                                           + "Connection: close\r\n\r\n";
                         byte[] healthHead = Encoding.ASCII.GetBytes(healthResp);
                         ns.Write(healthHead, 0, healthHead.Length);
                         ns.Write(health, 0, health.Length);
                         ns.Flush();
                         return;
                     }

                    string requestLine = head.Split('\n')[0].Trim();
                    string[] requestParts = requestLine.Split(' ');
                    if (requestParts.Length != 3 || requestParts[0] != "POST"
                        || requestParts[1].Split('?')[0] != "/sms")
                    {
                        byte[] notFound = Encoding.ASCII.GetBytes("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                        ns.Write(notFound, 0, notFound.Length);
                        return;
                    }

                       if (body != null && body.Trim().Length > 0)
                          HandleIncoming(body, "http", HeaderValueB64(head, "X-SMS-Sender-B64", HeaderValue(head, "X-SMS-Sender")));

                    byte[] payload = Encoding.UTF8.GetBytes("{\"ok\":true}");
                    string resp = "HTTP/1.1 200 OK\r\n"
                                + "Content-Type: application/json; charset=utf-8\r\n"
                                + "Content-Length: " + payload.Length + "\r\n"
                                + "Connection: close\r\n\r\n";
                    byte[] respHead = Encoding.ASCII.GetBytes(resp);
                    ns.Write(respHead, 0, respHead.Length);
                    ns.Write(payload, 0, payload.Length);
                    ns.Flush();
                }
            }
            catch (Exception e) { Log.Write("client error: " + e.Message); }
            finally { Interlocked.Decrement(ref activeClients); }
        }

        static bool TokenOk(string head, string requiredToken)
        {
            if (head == null) return false;
            string[] lines = head.Split('\n');

            // 头: X-Token: xxx
            for (int i = 0; i < lines.Length; i++)
            {
                string s = lines[i].Trim();
                int c = s.IndexOf(':');
                if (c <= 0) continue;
                if (s.Substring(0, c).Trim().Equals("X-Token", StringComparison.OrdinalIgnoreCase)
                    && s.Substring(c + 1).Trim() == requiredToken) return true;
            }

            // 查询串: ?token=xxx (按键值精确比较, 避免 xtoken= 之类误判)
            if (lines.Length > 0)
            {
                string first = lines[0];
                int q = first.IndexOf('?');
                if (q >= 0)
                {
                    int sp = first.IndexOf(' ', q);
                    string query = sp > q ? first.Substring(q + 1, sp - q - 1) : first.Substring(q + 1);
                    string[] kvs = query.Split('&');
                    for (int i = 0; i < kvs.Length; i++)
                    {
                        int eq = kvs[i].IndexOf('=');
                         if (eq > 0 && kvs[i].Substring(0, eq) == "token")
                         {
                             string supplied = kvs[i].Substring(eq + 1);
                             try { supplied = Uri.UnescapeDataString(supplied); }
                             catch { continue; }
                              if (supplied == requiredToken) return true;
                         }
                    }
                }
            }
            return false;
        }

        static bool IsHealthRequest(string head)
        {
            if (head == null) return false;
            int end = head.IndexOf('\n');
            string first = end >= 0 ? head.Substring(0, end).Trim() : head.Trim();
            string[] parts = first.Split(' ');
            return parts.Length == 3 && parts[0] == "GET"
                && parts[1].Split('?')[0] == "/health";
        }

        static bool ReadHttp(NetworkStream ns, out string head, out string body)
        {
            head = null; body = "";
            using (MemoryStream ms = new MemoryStream())
            {
                byte[] buf = new byte[4096];
                int headerEnd = -1;
                int cl = -1;
                int sep = 4;

                while (true)
                {
                    int n = ns.Read(buf, 0, buf.Length);
                    if (n <= 0) break;
                    ms.Write(buf, 0, n);
                    byte[] arr = ms.GetBuffer();
                    int len = (int)ms.Length;

                    if (headerEnd < 0)
                    {
                        headerEnd = IndexOfSeq(arr, len, CrlfCrlf);
                        sep = 4;
                        if (headerEnd < 0)
                        {
                            headerEnd = IndexOfSeq(arr, len, LfLf);   // 兼容只用 \n\n 的客户端
                            sep = 2;
                        }
                        if (headerEnd >= 0)
                        {
                            head = Encoding.ASCII.GetString(arr, 0, headerEnd);
                            cl = ParseContentLength(head);
                            if (cl < 0 && RequestHasNoBody(head)) cl = 0;
                            if (cl < 0 || cl > 1048576 || HeaderValue(head, "Transfer-Encoding").Length > 0) return false;
                        }
                    }
                    if (headerEnd >= 0 && cl >= 0 && len >= headerEnd + sep + cl) break;
                    if (len > 1048576) break;
                }

                if (headerEnd < 0) return false;
                int bodyStart = headerEnd + sep;
                int bodyLen = (int)ms.Length - bodyStart;
                if (cl < 0 || bodyLen < cl) return false;
                if (bodyLen < 0) bodyLen = 0;
                if (cl >= 0 && bodyLen > cl) bodyLen = cl;   // 无 Content-Length 时保留已读到的 body
                body = Encoding.UTF8.GetString(ms.GetBuffer(), bodyStart, bodyLen);
                return true;
            }
        }

        static int ParseContentLength(string head)
        {
            string[] lines = head.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string s = lines[i].Trim();
                int c = s.IndexOf(':');
                if (c <= 0) continue;
                if (s.Substring(0, c).Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    int v;
                    if (int.TryParse(s.Substring(c + 1).Trim(), out v)) return v;
                }
            }
            return -1;
        }

        static string HeaderValue(string head, string name)
        {
            if (head == null || name == null) return "";
            string[] lines = head.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string s = lines[i].Trim();
                int c = s.IndexOf(':');
                if (c <= 0) continue;
                if (s.Substring(0, c).Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                    return s.Substring(c + 1).Trim();
            }
            return "";
        }

        static string HeaderValueB64(string head, string name, string fallback)
        {
            string value = HeaderValue(head, name);
            if (value.Length == 0) return fallback ?? "";
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
            catch { return fallback ?? ""; }
        }

        static bool RequestHasNoBody(string head)
        {
            if (head == null) return false;
            int end = head.IndexOf('\n');
            string first = end >= 0 ? head.Substring(0, end).Trim() : head.Trim();
            return first.StartsWith("GET ", StringComparison.OrdinalIgnoreCase)
                || first.StartsWith("HEAD ", StringComparison.OrdinalIgnoreCase);
        }

        static int IndexOfSeq(byte[] hay, int len, byte[] needle)
        {
            int nl = needle.Length;
            for (int i = 0; i + nl <= len; i++)
            {
                bool ok = true;
                for (int j = 0; j < nl; j++)
                {
                    if (hay[i + j] != needle[j]) { ok = false; break; }
                }
                if (ok) return i;
            }
            return -1;
        }

        // ---------- ntfy 公网订阅 (NDJSON stream) ----------

        static void StartNtfy()
        {
            Thread t = new Thread(NtfyLoop);
            t.IsBackground = true;
            t.Name = "ntfy";
            t.Start();
        }

        static bool SeenBefore(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            lock (SeenGate)
            {
                if (SeenIds.Contains(id)) return true;
                SeenIds.Add(id);
                SeenOrder.Enqueue(id);
                while (SeenOrder.Count > 200)
                {
                    string old = SeenOrder.Dequeue();
                    SeenIds.Remove(old);
                }
                return false;
            }
        }

        static void NtfyLoop()
        {
            while (running)
            {
                try
                {
                    if (!IsHttpsEndpoint(cfg.NtfyServer))
                    {
                        Log.Write("ntfy stopped: server is not HTTPS");
                        return;
                    }
                    string url = cfg.NtfyServer.TrimEnd('/') + "/" + cfg.NtfyTopic + "/json";
                    Log.Write("ntfy subscribing");
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                    req.AllowAutoRedirect = false;
                    req.Timeout = 30000;
                    req.ReadWriteTimeout = 120000;
                    req.UserAgent = "codepass/1.0";
                    if (cfg.NtfyToken.Length > 0)
                        req.Headers["Authorization"] = "Bearer " + cfg.NtfyToken;

                    using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                    using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    {
                        if ((int)resp.StatusCode != 200) throw new IOException("ntfy response is not HTTP 200");
                        string line;
                        while (running && (line = sr.ReadLine()) != null)
                        {
                            if (line.Length == 0) continue;
                            string ev = CodeExtractor.JsonGetString(line, "event");
                            if (ev != "message") continue;
                            string mid = CodeExtractor.JsonGetString(line, "id");
                            if (SeenBefore(mid)) continue;   // 断线重连后不重放
                             string msg = CodeExtractor.JsonGetString(line, "message");
                             string sender = CodeExtractor.JsonGetString(line, "title");
                             if (msg != null && msg.Trim().Length > 0) HandleIncoming(msg, "ntfy", sender);
                        }
                    }
                }
                catch (Exception e)
                {
                    if (running) Log.Write("ntfy error: " + e.Message);
                }
                if (running) Thread.Sleep(5000);
            }
        }
    }
}
