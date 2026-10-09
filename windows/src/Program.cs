// codepass - SMS verification code -> Windows clipboard
// 目标框架: .NET Framework 4.x (Win10/11 系统自带, 无需安装运行时)
// 编译: 见同目录 build.ps1
//
// 通道:
//   1) 局域网 HTTP:  手机 POST http://<本机IP>:<port>/sms
//                    可选鉴权: 头 X-Token 或 query ?token=  (config.ini 里 token 非空时启用)
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
        static volatile string pendingClipboardCode;
        static volatile string latestCode;
        static volatile bool running = true;
        internal static bool StartupSyncFailed { get; private set; }

        static int activeClients;
        const int MaxClients = 32;

        static readonly byte[] CrlfCrlf = new byte[] { 13, 10, 13, 10 };
        static readonly byte[] LfLf = new byte[] { 10, 10 };

        static readonly HashSet<string> SeenIds = new HashSet<string>();
        static readonly Queue<string> SeenOrder = new Queue<string>();
        static readonly object SeenGate = new object();

        [STAThread]
        static void Main(string[] args)
        {
            // 单实例, 避免重复启动导致端口冲突
            bool createdNew;
            singleInstance = new Mutex(true, "Codepass_SingleInstance", out createdNew);
            if (!createdNew)
            {
                 MessageBox.Show(null, "codepass 已经在运行，请从系统托盘打开。", "codepass",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            // 老版 .NET Framework 默认只协商到 TLS 1.0, 访问 ntfy.sh 等站点会 SSL 失败
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12
                    | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            }
            catch { }

            appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Log.Init(Path.Combine(appDir, "app.log"));

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
            List<HistoryItem> savedHistory = History.Snapshot();
            if (savedHistory.Count > 0) latestCode = savedHistory[0].Code;
            Updater.CleanupOldFile();

            Log.Write("========== start ==========");
            Log.Write("port=" + cfg.Port
                + " ntfy=" + (cfg.NtfyTopic.Length == 0 ? "(off)" : "(on)")
                + " token=" + (cfg.Token.Length == 0 ? "(off)" : "(on)")
                + " ntfy_token=" + (cfg.NtfyToken.Length == 0 ? "(off)" : "(on)")
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

            Log.Write("started.");
            app.Run(mainWindow);

            running = false;
            try { if (clearTimer != null) { clearTimer.Stop(); clearTimer = null; } } catch { }
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
                    key.SetValue("codepass", "\"" + exe + "\"", RegistryValueKind.String);
                }
                else
                {
                    key.DeleteValue("codepass", false);
                }
            }
            StartupSyncFailed = false;
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
            if (CodeExtractor.IsFiltered(text, cfg.FilterKeywords, cfg.FilterRegex))
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
            Log.Write("[" + src + "] code recognized");
            if (cfg.Dry) { Log.Write("[dry] skip clipboard"); return; }
            latestCode = code;

            HistoryItem it = new HistoryItem();
            it.Time = DateTime.Now.ToString("MM-dd HH:mm:ss");
            it.Source = History.ExtractSender(text, sender);
            it.Code = code;
            it.Raw = text ?? "";
            History.Add(it);

            if (mainWindow == null) return;

            try
            {
                mainWindow.Dispatcher.BeginInvoke(new Action(delegate
                {
                    // 委托体在 UI 线程执行, 必须自行兜底异常
                    try
                    {
                        mainWindow.OnNewRecord(it);
                        // 剪贴板写入放后台 STA 线程重试（最长约 10 秒）：第三方程序
                        // （游戏加速器、剪贴板工具等）短期占用时不至于复制失败，也不阻塞界面。
                        StartClipboardWrite(code, text);
                    }
                    catch (Exception e) { Log.Write("ui error: " + e.Message); }
                }));
            }
            catch (Exception e) { Log.Write("invoke error: " + e.Message); }
        }

        static string Shorten(string s)
        {
            if (s == null) return "";
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.Length > 120 ? s.Substring(0, 120) + "..." : s;
        }

        static string NotificationText(string code, string raw)
        {
            string body;
            if (cfg.PrivacyMode == 2)
            {
                body = Shorten(raw);
                if (body.Length == 0) body = code;
            }
            else if (cfg.PrivacyMode == 1)
            {
                body = MaskCode(raw, code);
                if (body.Length == 0) body = MaskCode(code, code);
                body = Shorten(body);
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
            if (code == null || code.Length == 0) return text;
            return text.Replace(code, new string('*', code.Length));
        }

        static void CopyLatestCode()
        {
            string code = latestCode;
            if (String.IsNullOrEmpty(code))
            {
                if (tray != null)
                    tray.ShowBalloonTip(1800, "codepass", "暂无可复制的验证码", System.Windows.Forms.ToolTipIcon.Info);
                return;
            }
            StartClipboardWrite(code, code);
        }

        static bool IsHttpsEndpoint(string value)
        {
            Uri uri;
            return Uri.TryCreate(value ?? "", UriKind.Absolute, out uri)
                && uri.Scheme == Uri.UriSchemeHttps;
        }

        static void ScheduleClear()
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
                try { System.Windows.Clipboard.Clear(); Log.Write("clipboard cleared"); } catch { }
            };
            t.Start();
        }

        // ---------- 剪贴板写入 (Win32 直写 CF_UNICODETEXT, 不依赖延迟渲染) ----------

        [DllImport("user32.dll", SetLastError = true)] static extern bool OpenClipboard(IntPtr hWndNewOwner);
        [DllImport("user32.dll", SetLastError = true)] static extern bool EmptyClipboard();
        [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
        [DllImport("user32.dll", SetLastError = true)] static extern IntPtr GetClipboardData(uint uFormat);
        [DllImport("user32.dll", SetLastError = true)] static extern bool CloseClipboard();
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr GlobalLock(IntPtr hMem);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool GlobalUnlock(IntPtr hMem);
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr GlobalFree(IntPtr hMem);

        const uint CF_UNICODETEXT = 13;
        const uint GMEM_MOVEABLE = 0x0002;

        /// <summary>
        /// 写验证码到剪贴板：后台 STA 线程重试最长约 10 秒；每次失败后校验
        /// 剪贴板内容是否已是目标验证码；最终结果回到 UI 线程弹通知。
        /// 期间若有更新的验证码到来，旧线程直接退出，避免把旧验证码写回去。
        /// </summary>
        static void StartClipboardWrite(string code, string text)
        {
            pendingClipboardCode = code;
            Thread worker = new Thread(delegate()
            {
                bool ok = false;
                string lastError = null;
                int tries = 0;
                int deadline = unchecked(Environment.TickCount + 10000);
                while (!ok)
                {
                    if (!string.Equals(pendingClipboardCode, code, StringComparison.Ordinal))
                    {
                        Log.Write("clipboard: superseded, stop retry");
                        return;
                    }
                    tries++;
                    string error = null;
                    try
                    {
                        if (SetClipboardTextWin32(code, out error)) { ok = true; break; }
                    }
                    catch (Exception ex)
                    {
                        // P/Invoke 意外异常绝不能让后台线程崩溃；按失败重试处理。
                        error = ex.GetType().Name + ": " + ex.Message;
                    }
                    lastError = error;
                    // 写入报错不代表一定没写进去：校验剪贴板内容是否已是目标验证码。
                    if (ClipboardTextMatchesWin32(code)) { ok = true; break; }
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
                if (!string.Equals(pendingClipboardCode, code, StringComparison.Ordinal))
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
                            if (cfg.ShowTip && tray != null)
                            {
                                if (ok)
                                    tray.ShowBalloonTip(2500, "验证码已复制", NotificationText(code, text),
                                        System.Windows.Forms.ToolTipIcon.Info);
                                else
                                    tray.ShowBalloonTip(2500, "验证码已收到，复制失败",
                                        "未能写入剪贴板（可能被其他程序占用），请到记录页手动复制。",
                                        System.Windows.Forms.ToolTipIcon.Warning);
                            }
                            if (ok && cfg.AutoClearSeconds > 0) ScheduleClear();
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

        /// <summary>
        /// 用 Win32 直接写入 CF_UNICODETEXT：立即拷贝、不依赖延迟渲染。
        /// 先分配并填充内存再清空剪贴板：分配/加锁失败不会清空原内容；
        /// 写入环节失败会清空原内容，由上层重试补救。
        /// </summary>
        static bool SetClipboardTextWin32(string text, out string error)
        {
            error = null;
            if (!OpenClipboard(IntPtr.Zero))
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
            t.Start();
        }

        static void HttpLoop()
        {
            TcpListener listener;
            try
            {
                listener = new TcpListener(IPAddress.Any, cfg.Port);
                listener.Start();
                Log.Write("HTTP listening on 0.0.0.0:" + cfg.Port);
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

                    if (cfg.Token.Length > 0 && !TokenOk(head))
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

        static bool TokenOk(string head)
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
                    && s.Substring(c + 1).Trim() == cfg.Token) return true;
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
                             if (supplied == cfg.Token) return true;
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
            return first.StartsWith("GET /health ", StringComparison.OrdinalIgnoreCase)
                || first.Equals("GET /health", StringComparison.OrdinalIgnoreCase);
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
                            if (cl > 1048576) cl = 1048576;   // 防整数溢出 / 防巨量 body
                        }
                    }
                    if (headerEnd >= 0 && cl >= 0 && len >= headerEnd + sep + cl) break;
                    if (len > 1048576) break;
                }

                if (headerEnd < 0) return false;
                int bodyStart = headerEnd + sep;
                int bodyLen = (int)ms.Length - bodyStart;
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
                    req.Timeout = 30000;
                    req.ReadWriteTimeout = 120000;
                    req.UserAgent = "codepass/1.0";
                    if (cfg.NtfyToken.Length > 0)
                        req.Headers["Authorization"] = "Bearer " + cfg.NtfyToken;

                    using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                    using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    {
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
