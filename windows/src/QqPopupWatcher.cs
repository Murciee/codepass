// QQ 右下角新邮件提醒弹窗联动（可选）
// - 轻量监测屏幕右下角新出现的 QQ/TIM 小窗口；只识别“邮件提醒”类弹窗
// - 只读取窗口标题判断邮件提醒，作为 IMAP 检查信号；不直读验证码或聊天正文。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;

namespace Codepass
{
    static class QqPopupWatcher
    {
        static int stopGen;
        internal static string LastSignalText = "尚未检测到邮件提醒";
        static readonly int CurrentPid = Process.GetCurrentProcess().Id;
        static readonly ManualResetEventSlim stopSignal = new ManualResetEventSlim(false);

        public static void RequestStop()
        {
            Interlocked.Increment(ref stopGen);
            stopSignal.Set();
        }

        /// <summary>创建监测线程前取一个生成号，作为该线程的身份（见 EmailWatcher 同名机制）。</summary>
        public static int BeginGeneration()
        {
            stopSignal.Reset();
            return Interlocked.Increment(ref stopGen);
        }

        public static void Loop(int gen)
        {
            Log.Write("qq popup watcher started");
            Dictionary<IntPtr, string> seen = new Dictionary<IntPtr, string>();
            Dictionary<int, string> processNames = new Dictionary<int, string>();
            List<IntPtr> candidates = new List<IntPtr>();
            int loops = 0;
            while (gen == Volatile.Read(ref stopGen) && Program.Running)
            {
                try
                {
                    if (!Program.cfg.QqPopupEnabled)
                    {
                        if (stopSignal.Wait(1500)) break;
                        continue;
                    }
                    if (!HasQqProcess())
                    {
                        if (stopSignal.Wait(5000)) break;
                        continue;
                    }
                    if (stopSignal.Wait(700)) break;
                    if (gen != Volatile.Read(ref stopGen) || !Program.Running || !Program.cfg.QqPopupEnabled) break;
                    if (!HasQqProcess()) continue;
                    loops++;
                    if (loops % 300 == 0) processNames.Clear();

                    CollectCandidates(candidates, processNames);
                    CleanupSeen(seen);

                    foreach (IntPtr hwnd in candidates)
                    {
                        if (gen != Volatile.Read(ref stopGen) || !Program.cfg.QqPopupEnabled) break;
                        string text = ReadWindowTitle(hwnd);
                        string prev;
                        bool isNew = !seen.TryGetValue(hwnd, out prev);
                        if (!isNew && string.Equals(prev, text, StringComparison.Ordinal)) continue;
                        seen[hwnd] = text;
                        if (text.Length == 0) continue;
                        if (!LooksLikeMail(text))
                        {
                            if (isNew) Log.Write("qq popup: ignored (not a mail notice)");
                            continue;
                        }
                        if (gen == Volatile.Read(ref stopGen) && Program.cfg.QqPopupEnabled) HandleMailPopup(text);
                    }
                }
                catch (Exception ex)
                {
                    Log.Write("qq popup error: " + ex.Message);
                    if (stopSignal.Wait(3000)) break;
                }
            }
            Log.Write("qq popup watcher stopped");
        }

        static void HandleMailPopup(string text)
        {
            try
            {
                if (Program.EmailConfigured())
                {
                    LastSignalText = DateTime.Now.ToString("HH:mm:ss") + "（已唤醒收件箱与分类目录）";
                    Log.Write("qq popup: mail signal; waking both watchers");
                    EmailWatcher.Kick();
                    Program.EmailStatusChanged();
                }
            }
            catch (Exception ex)
            {
                Log.Write("qq popup handle error: " + ex.Message);
            }
        }

        static bool LooksLikeMail(string text)
        {
            return text.IndexOf("邮件", StringComparison.Ordinal) >= 0
                || text.IndexOf("邮箱", StringComparison.Ordinal) >= 0
                || text.IndexOf("mail", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ---------- 候选窗口收集 ----------

        static void CollectCandidates(List<IntPtr> result, Dictionary<int, string> processNames)
        {
            result.Clear();
            EnumWindowsProc callback = delegate(IntPtr hwnd, IntPtr lparam)
            {
                try
                {
                    if (!IsWindowVisible(hwnd)) return true;
                    RECT rect;
                    if (!GetWindowRect(hwnd, out rect)) return true;
                    int w = rect.Right - rect.Left;
                    int h = rect.Bottom - rect.Top;
                    // 只考虑右下角的小提示窗；主窗口一般远大于此。
                    if (w <= 0 || h <= 0 || w > 720 || h > 520) return true;
                    if (!IsNearBottomRight(rect)) return true;
                    int pid;
                    GetWindowThreadProcessId(hwnd, out pid);
                    if (pid == 0 || pid == CurrentPid) return true;
                    if (!IsQqProcess(pid, processNames)) return true;
                    result.Add(hwnd);
                }
                catch { }
                return true;
            };
            EnumWindows(callback, IntPtr.Zero);
        }

        static void CleanupSeen(Dictionary<IntPtr, string> seen)
        {
            if (seen.Count == 0) return;
            List<IntPtr> dead = null;
            foreach (IntPtr hwnd in seen.Keys)
            {
                if (!IsWindow(hwnd))
                {
                    if (dead == null) dead = new List<IntPtr>();
                    dead.Add(hwnd);
                }
            }
            if (dead != null)
                foreach (IntPtr hwnd in dead) seen.Remove(hwnd);
        }

        static bool IsNearBottomRight(RECT rect)
        {
            Rectangle window = new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
            Rectangle screen = Screen.FromRectangle(window).Bounds;
            return rect.Right >= screen.Right - 160 && rect.Bottom >= screen.Bottom - 160;
        }

        static bool HasQqProcess()
        {
            foreach (string name in new string[] { "QQ", "TIM", "QQNT" })
            {
                Process[] processes = null;
                try
                {
                    processes = Process.GetProcessesByName(name);
                    if (processes.Length > 0) return true;
                }
                catch { }
                finally
                {
                    if (processes != null)
                        foreach (Process process in processes) { try { process.Dispose(); } catch { } }
                }
            }
            return false;
        }

        static bool IsQqProcess(int pid, Dictionary<int, string> cache)
        {
            string name;
            if (!cache.TryGetValue(pid, out name))
            {
                name = "";
                try
                {
                    using (Process process = Process.GetProcessById(pid)) name = process.ProcessName;
                }
                catch { }
                if (name.Length > 0) cache[pid] = name;
            }
            if (name.Length == 0) return false;
            // 仅限 QQ / TIM 客户端进程（含新版 QQNT）；不用前缀匹配，避免 QQMusic、
            // QQBrowser 等其它腾讯进程的小窗口被误扫。
            return name.Equals("QQ", StringComparison.OrdinalIgnoreCase)
                || name.Equals("TIM", StringComparison.OrdinalIgnoreCase)
                || name.Equals("QQNT", StringComparison.OrdinalIgnoreCase);
        }

        // ---------- 弹窗文字 ----------

        static string ReadWindowText(IntPtr hwnd)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                AutomationElement root = AutomationElement.FromHandle(hwnd);
                if (root != null)
                {
                    AppendName(sb, root);
                    AutomationElementCollection all = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
                    int count = Math.Min(all.Count, 120);
                    for (int i = 0; i < count; i++) AppendName(sb, all[i]);
                }
            }
            catch { }
            if (sb.Length == 0)
            {
                try
                {
                    int len = GetWindowTextLength(hwnd);
                    if (len > 0)
                    {
                        StringBuilder title = new StringBuilder(len + 1);
                        GetWindowText(hwnd, title, title.Capacity);
                        sb.Append(title);
                    }
                }
                catch { }
            }
            return sb.ToString().Trim();
        }

        static string ReadWindowTitle(IntPtr hwnd)
        {
            int length = Math.Min(256, GetWindowTextLength(hwnd));
            if (length <= 0) return "";
            StringBuilder title = new StringBuilder(length + 1);
            GetWindowText(hwnd, title, title.Capacity);
            return title.ToString();
        }

        static void AppendName(StringBuilder sb, AutomationElement element)
        {
            try
            {
                string name = element.Current.Name;
                if (string.IsNullOrEmpty(name)) return;
                name = name.Trim();
                if (name.Length == 0) return;
                if (sb.Length > 0 && sb.ToString().IndexOf(name, StringComparison.Ordinal) >= 0) return;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(name);
            }
            catch { }
        }

        // ---------- Win32 ----------

        delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lparam);

        [StructLayout(LayoutKind.Sequential)]
        struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [DllImport("user32.dll")]
        static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lparam);
        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")]
        static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")]
        static extern int GetWindowThreadProcessId(IntPtr hwnd, out int pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
        [DllImport("user32.dll")]
        static extern int GetWindowTextLength(IntPtr hwnd);
    }
}
