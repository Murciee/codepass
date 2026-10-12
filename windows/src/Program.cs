// codepass - .NET Framework 4.x 进程入口、托盘与应用级协调。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Microsoft.Win32;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
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
                    try { FileInfo fi = new FileInfo(_path); if (fi.Exists && fi.Length > MaxBytes) File.WriteAllText(_path, ""); } catch { }
                    File.AppendAllText(_path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine, new UTF8Encoding(true));
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
        static DispatcherTimer historyRetentionTimer;
        static int retentionQueued;
        static volatile bool running = true;
        internal static bool StartupSyncFailed { get; private set; }
        internal static bool Running { get { return running; } }

        // 配置/生命周期回调由入口注入；模块不读取 Program 的共享字段。
        internal static readonly ClipboardService Clipboard = new ClipboardService(
            delegate { return cfg; }, DeliveryDispatcher, EmailStatusChanged, ClipboardCompleted);
        internal static readonly IncomingDelivery Incoming = new IncomingDelivery(
            delegate { return cfg; }, delegate { return running; }, PromoteLatestRecord, ApplyHistoryRetention, EmailStatusChanged, PresentIncoming);
        internal static readonly HttpReceiver Http = new HttpReceiver(delegate { return cfg; }, Incoming, EmailStatusChanged);
        internal static readonly NtfyReceiver Ntfy = new NtfyReceiver(delegate { return cfg; }, delegate { return appDir; }, Incoming, EmailStatusChanged);

        internal sealed class LatestReceipt
        {
            public readonly string Code;
            public readonly bool EmailChannel;
            public readonly long ReceivedUtcTicks;
            internal readonly HistoryItem Record;
            internal LatestReceipt(string code, bool emailChannel, long receivedUtcTicks, HistoryItem record = null)
            { Code = code; EmailChannel = emailChannel; ReceivedUtcTicks = receivedUtcTicks; Record = record; }
        }
        static volatile LatestReceipt latestReceipt;
        internal static void SetLatestCode(string code, bool emailChannel)
        { latestReceipt = string.IsNullOrEmpty(code) ? null : new LatestReceipt(code, emailChannel, DateTime.UtcNow.Ticks); }
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
            lock (Config.SaveGate) SetLatestRecord(History.LatestUnexpiredRecord(cfg == null ? 0 : cfg.HistoryRetentionMinutes, DateTime.UtcNow));
        }
        internal static void SetLatestRecord(HistoryItem item)
        {
            latestReceipt = item == null || string.IsNullOrEmpty(item.Code) ? null
                : new LatestReceipt(item.Code, item.Channel == "email", item.ReceivedUtcTicks, item);
        }
        static void PromoteLatestRecord(HistoryItem item)
        {
            lock (Config.SaveGate)
            {
                LatestReceipt latest = GetLatestReceipt();
                if (latest == null || item.ReceivedUtcTicks >= latest.ReceivedUtcTicks) SetLatestRecord(item);
            }
        }
        static bool IsLatestRecord(HistoryItem item)
        {
            LatestReceipt latest = GetLatestReceipt();
            return latest != null && object.ReferenceEquals(latest.Record, item);
        }
        internal static void ForgetHistoryRecord(HistoryItem item)
        {
            lock (Config.SaveGate)
            {
                LatestReceipt latest = latestReceipt;
                if (latest != null && object.ReferenceEquals(latest.Record, item)) RestoreLatestFromHistory();
            }
        }
        internal static void ExpireLatestReceipt(int minutes, DateTime nowUtc)
        {
            lock (Config.SaveGate)
            {
                LatestReceipt latest = latestReceipt;
                if (minutes > 0 && latest != null && latest.ReceivedUtcTicks < nowUtc.AddMinutes(-minutes).Ticks) RestoreLatestFromHistory();
            }
        }

        [STAThread]
        static void Main(string[] args)
        {
            bool silent = IsSilentStartup(args), createdNew;
            singleInstance = new Mutex(true, "Codepass_SingleInstance", out createdNew);
            if (!createdNew)
            {
                if (!silent) MessageBox.Show(null, "codepass 已经在运行，请从系统托盘打开。", "codepass",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }
            try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
            appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Log.Init(Path.Combine(appDir, "app.log"));
            Config.RecycleStaleTemporaryFiles(appDir);
            cfg = Config.Load(Path.Combine(appDir, "config.ini"));
            if (cfg.Port < 1 || cfg.Port > 65535) { cfg.Port = 8787; Log.Write("invalid port in config; fallback to 8787"); }
            if (!NtfyReceiver.IsHttpsEndpoint(cfg.NtfyServer))
            { cfg.NtfyTopic = ""; cfg.NtfyToken = ""; Log.Write("ntfy disabled: server is not HTTPS"); }
            try { SetStartup(cfg.StartWithWindows); }
            catch (Exception ex) { StartupSyncFailed = true; Log.Write("startup setting sync failed: " + ex.Message); }
            if (args != null) foreach (string arg in args) if (string.Equals(arg, "--dry", StringComparison.OrdinalIgnoreCase)) cfg.Dry = true;
            History.Init(Path.Combine(appDir, "history.txt"));
            try { History.PruneExpired(cfg.HistoryRetentionMinutes, DateTime.UtcNow); }
            catch (IOException) { Log.Write("history expiry pending: storage failure"); }
            RestoreLatestFromHistory();
            Log.Write("========== start ==========");
            Log.Write("port=" + cfg.Port + " ntfy=" + (cfg.NtfyTopic.Length == 0 ? "(off)" : "(on)")
                + " token=" + (cfg.Token.Length == 0 ? "(off)" : "(on)") + " ntfy_token=" + (cfg.NtfyToken.Length == 0 ? "(off)" : "(on)")
                + " email=" + (cfg.EmailEnabled ? "(on)" : "(off)") + " qq_popup=" + (cfg.QqPopupEnabled ? "(on)" : "(off)") + " dry=" + cfg.Dry);
            try { System.Windows.Forms.Application.EnableVisualStyles(); } catch { }
            app = new System.Windows.Application(); app.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
            app.SessionEnding += delegate(object sender, System.Windows.SessionEndingCancelEventArgs e)
            {
                if (mainWindow != null && !mainWindow.ExitApp()) { e.Cancel = true; return; }
                lock (Config.SaveGate) running = false;
            };
            try { Clipboard.Initialize(); if (!silent) CreateMainWindow(); }
            catch (Exception ex)
            {
                Log.Write("UI init error: " + ex.ToString());
                MessageBox.Show(null, "codepass 界面初始化失败：\n" + ex.Message, "codepass",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                Clipboard.Dispose(); return;
            }
            BuildTray(); Http.Start(); if (cfg.NtfyTopic.Length > 0) Ntfy.Start(); RefreshEmailWatchers();
            Log.Write("started.");
            historyRetentionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            historyRetentionTimer.Tick += delegate
            {
                if (Interlocked.Exchange(ref retentionQueued, 1) != 0) return;
                ThreadPool.QueueUserWorkItem(delegate { try { ApplyHistoryRetention(); } finally { Interlocked.Exchange(ref retentionQueued, 0); } });
            };
            historyRetentionTimer.Start();
            if (silent) app.Run(); else RunMainWindow(app, mainWindow, false);
            lock (Config.SaveGate) running = false;
            Clipboard.Dispose(); StopWatchersForExit();
            try { if (historyRetentionTimer != null) { historyRetentionTimer.Stop(); historyRetentionTimer = null; } } catch { }
            try { if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; } } catch { }
            try { if (trayMenu != null) { trayMenu.Dispose(); trayMenu = null; } } catch { }
            try { if (singleInstance != null) singleInstance.ReleaseMutex(); } catch { }
            Log.Write("========== exit ==========");
        }

        internal static bool QuitApp()
        {
            if (mainWindow != null && !mainWindow.ExitApp()) return false;
            lock (Config.SaveGate) running = false;
            if (app != null) app.Shutdown(); return true;
        }
        static void CreateMainWindow()
        {
            if (mainWindow != null) return;
            mainWindow = new MainWindow(cfg, Path.Combine(appDir, "config.ini")); PrepareMainWindow(mainWindow, true);
            if (app != null) app.MainWindow = mainWindow;
        }
        static void ShowMainWindow()
        {
            try { CreateMainWindow(); mainWindow.ShowFromTray(); }
            catch (Exception ex) { Log.Write("window open failed: " + ex.GetType().Name); }
        }
        static Dispatcher DeliveryDispatcher()
        { return app != null ? app.Dispatcher : mainWindow == null ? null : mainWindow.Dispatcher; }
        internal static void SetStartup(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run"))
            {
                if (key == null) throw new InvalidOperationException("无法打开当前用户启动项");
                if (enabled)
                {
                    string exe = Assembly.GetExecutingAssembly().Location;
                    if (String.IsNullOrEmpty(exe)) throw new InvalidOperationException("无法确定程序路径");
                    key.SetValue("codepass", StartupCommand(exe), RegistryValueKind.String);
                }
                else key.DeleteValue("codepass", false);
            }
            StartupSyncFailed = false;
        }
        internal static string StartupCommand(string exe) { return "\"" + exe + "\" --startup"; }
        internal static bool IsSilentStartup(string[] args)
        {
            if (args != null) foreach (string arg in args) if (string.Equals(arg, "--startup", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
        internal static void RunMainWindow(System.Windows.Application application, MainWindow window, bool silent)
        {
            PrepareMainWindow(window, silent); application.MainWindow = window;
            if (silent) { window.ShowInTaskbar = false; application.Run(); } else application.Run(window);
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
            { if (cfg != null) ExpireLatestReceipt(cfg.HistoryRetentionMinutes, DateTime.UtcNow); EmailStatusChanged(); }
        }
        static void BuildTray()
        {
            tray = new System.Windows.Forms.NotifyIcon();
            try
            {
                Icon icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
                tray.Icon = icon == null ? (Icon)SystemIcons.Application.Clone() : (Icon)icon.Clone();
                if (icon != null) icon.Dispose();
            }
            catch { tray.Icon = (Icon)SystemIcons.Application.Clone(); }
            tray.Text = "codepass"; tray.Visible = true;
            System.Windows.Forms.ContextMenuStrip menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("复制最新验证码", null, delegate { CopyLatestCode(); });
            System.Windows.Forms.ToolStripMenuItem pause = new System.Windows.Forms.ToolStripMenuItem("暂停自动复制 15 分钟");
            pause.Click += delegate { RuntimeStatus.ToggleAutoCopyPause(); }; menu.Items.Add(pause);
            menu.Opening += delegate { pause.Text = RuntimeStatus.AutoCopyPaused ? "恢复自动复制" : "暂停自动复制 15 分钟"; };
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator()); menu.Items.Add("退出", null, delegate { QuitApp(); });
            tray.ContextMenuStrip = menu; trayMenu = menu; tray.DoubleClick += delegate { ShowMainWindow(); };
        }

        internal static List<string> DetectLocalIpList()
        {
            List<string> fallback = new List<string>(), preferred = new List<string>();
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(ip.Address)) continue;
                        string s = ip.Address.ToString();
                        if (s.StartsWith("169.254.") || fallback.Contains(s)) continue;
                        fallback.Add(s);
                        if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet || ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) preferred.Add(s);
                    }
                }
            }
            catch { }
            List<string> all = new List<string>(preferred);
            foreach (string s in fallback) if (!all.Contains(s)) all.Add(s);
            return all;
        }
        internal static Dictionary<string, string> LocalIpLabels()
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            try
            {
                foreach (NetworkInterface network in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (network.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (UnicastIPAddressInformation address in network.GetIPProperties().UnicastAddresses)
                        if (address.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address.Address))
                            result[address.Address.ToString()] = network.Name + " · " + network.NetworkInterfaceType;
                }
            }
            catch { }
            return result;
        }
        internal static string DetectLocalIp()
        { List<string> addresses = DetectLocalIpList(); return addresses.Count == 0 ? "127.0.0.1" : addresses[0]; }

        static void PresentIncoming(HistoryItem item, string text, bool autoCopy, bool notify, Func<bool> canDeliver)
        {
            Dispatcher dispatcher = DeliveryDispatcher(); if (dispatcher == null) return;
            try
            {
                dispatcher.BeginInvoke(new Action(delegate
                {
                    try
                    {
                        lock (Config.SaveGate)
                        {
                            if (!canDeliver()) return;
                            bool email = item.Channel == "email";
                            if (autoCopy && !RuntimeStatus.AutoCopyPaused && IsLatestRecord(item))
                                Clipboard.StartClipboardWrite(item.Code, text, notify, delegate { return !RuntimeStatus.AutoCopyPaused && canDeliver() && IsLatestRecord(item); }, email);
                            else if (notify && IsLatestRecord(item) && (email || cfg.ShowTip) && tray != null)
                                tray.ShowBalloonTip(2500, "验证码已收到", ChannelNotificationText(item.Code, text, email), System.Windows.Forms.ToolTipIcon.Info);
                            if (mainWindow != null) mainWindow.OnNewRecord(item);
                        }
                    }
                    catch (Exception e) { Log.Write("ui error: " + e.Message); }
                }));
            }
            catch (Exception e) { Log.Write("invoke error: " + e.Message); }
        }
        static void ClipboardCompleted(bool ok, string code, string text, bool email)
        {
            if (!(email || cfg.ShowTip) || tray == null) return;
            if (ok) tray.ShowBalloonTip(2500, "验证码已复制", ChannelNotificationText(code, text, email), System.Windows.Forms.ToolTipIcon.Info);
            else tray.ShowBalloonTip(2500, "验证码已收到，复制失败", "未能写入剪贴板（可能被其他程序占用），请到记录页手动复制。", System.Windows.Forms.ToolTipIcon.Warning);
        }

        static Thread emailThread, emailJunkThread, qqPopupThread;
        static string emailRuntimeSig = "";
        internal static bool EmailConfigured()
        {
            return cfg.EmailEnabled && cfg.EmailServer.Trim().Length > 0 && cfg.EmailAccount.Trim().Length > 0
                && !cfg.EmailPasswordRequired && cfg.EmailAuth.Trim().Length > 0;
        }
        internal static void SaveConfigQuiet()
        {
            try { cfg.Save(Path.Combine(appDir, "config.ini")); }
            catch (Exception e) { Log.Write("config save failed: " + e.Message); }
        }
        static int statusUpdateQueued;
        internal static void EmailStatusChanged()
        {
            if (mainWindow == null || Interlocked.Exchange(ref statusUpdateQueued, 1) != 0) return;
            try { mainWindow.Dispatcher.BeginInvoke(new Action(delegate
            { Interlocked.Exchange(ref statusUpdateQueued, 0); if (mainWindow != null) mainWindow.UpdateEmailStatus(); })); }
            catch { Interlocked.Exchange(ref statusUpdateQueued, 0); }
        }
        internal static void RefreshEmailWatchers()
        {
            string sig = EmailConfigured() ? EmailSignature(cfg) : "";
            bool emailAlive = emailThread != null && emailThread.IsAlive, foldersAlive = emailJunkThread != null && emailJunkThread.IsAlive;
            if (sig != emailRuntimeSig || (sig.Length > 0 && (!emailAlive || !foldersAlive)))
            {
                EmailWatcher.RequestStop(); EmailWatcher.RequestStopJunk(); emailThread = null; emailJunkThread = null; emailRuntimeSig = sig;
                if (sig.Length > 0)
                {
                    int gen = EmailWatcher.BeginMainGeneration(); emailThread = StartWorker("email", delegate { EmailWatcher.MainLoop(gen); }, false);
                    int junkGen = EmailWatcher.BeginJunkGeneration(); emailJunkThread = StartWorker("email-folders", delegate { EmailWatcher.JunkLoop(junkGen); }, false);
                }
            }
            EmailStatusChanged();
            if (cfg.QqPopupEnabled)
            {
                if (qqPopupThread == null || !qqPopupThread.IsAlive)
                { int gen = QqPopupWatcher.BeginGeneration(); qqPopupThread = StartWorker("qq-popup", delegate { QqPopupWatcher.Loop(gen); }, true); }
            }
            else { QqPopupWatcher.RequestStop(); qqPopupThread = null; }
        }
        internal static string EmailSignature(Config config)
        {
            return config.EmailServer + "\n" + config.EmailAccount + "\n" + config.EmailAuth
                + "\n" + config.EmailCheckJunk + "\n" + config.EmailPollSeconds + "\n" + config.EmailKeywords + "\n" + config.EmailSenders
                + "\n" + config.EmailAutoCopy + "\n" + config.EmailNotify + "\n" + config.EmailSaveBody + "\n" + config.MinLen + "\n" + config.MaxLen;
        }
        static Thread StartWorker(string name, ThreadStart body, bool sta)
        {
            Thread t = new Thread(body); t.IsBackground = true; t.Name = name;
            if (sta) { try { t.SetApartmentState(ApartmentState.STA); } catch { } }
            t.Start(); return t;
        }
        internal static void StopWatchersForExit()
        {
            Ntfy.Stop(); Http.Stop(); EmailWatcher.RequestStop(); EmailWatcher.RequestStopJunk(); QqPopupWatcher.RequestStop();
            foreach (Thread worker in new Thread[] { emailThread, emailJunkThread, qqPopupThread })
            { try { if (worker != null && worker.IsAlive && Thread.CurrentThread != worker) worker.Join(1500); } catch { } }
        }

        static string Shorten(string s)
        {
            if (s == null) return "";
            s = s.Replace("\r", " ").Replace("\n", " "); return s.Length > 120 ? s.Substring(0, 120) + "..." : s;
        }
        internal static string ChannelNotificationText(string code, string raw, bool emailChannel)
        {
            string body; int mode = emailChannel ? cfg.EmailPrivacyMode : cfg.PrivacyMode;
            if (mode == 2) { body = Shorten(raw); if (body.Length == 0) body = code; }
            else if (mode == 1) { body = Shorten(MaskCode(raw, code)); if (body.Length == 0) body = "收到一条新的验证码（内容已隐藏）"; }
            else body = "收到一条新的验证码";
            return body;
        }
        static string MaskCode(string text, string code)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (string.IsNullOrEmpty(code) || CodeExtractor.JsonGetString(text, "code") != null || text.IndexOf(code, StringComparison.Ordinal) < 0) return "";
            return text.Replace(code, new string('*', code.Length));
        }
        internal static void CopyLatestCode()
        {
            LatestReceipt receipt = GetLatestReceipt(); string code = receipt == null ? null : receipt.Code;
            if (String.IsNullOrEmpty(code))
            { if (tray != null) tray.ShowBalloonTip(1800, "codepass", "暂无可复制的验证码", System.Windows.Forms.ToolTipIcon.Info); return; }
            Clipboard.StartClipboardWrite(code, code, true, delegate
            {
                lock (Config.SaveGate) return running && latestReceipt == receipt
                    && (cfg.HistoryRetentionMinutes == 0 || receipt.ReceivedUtcTicks >= DateTime.UtcNow.AddMinutes(-cfg.HistoryRetentionMinutes).Ticks);
            }, receipt.EmailChannel);
        }
    }
}
