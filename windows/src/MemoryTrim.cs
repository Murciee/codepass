using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Codepass
{
    /// <summary>
    /// 空闲时把工作集交还给系统。窗口隐藏且一段时间没有消息处理后，进程只剩
    /// 后台线程在触碰少量页面，但历史缓存、WPF 视觉树等仍留在工作集里；Windows
    /// 只在内存紧张时才回收，任务管理器会一直显示几十 MB。做法与 WinShun 相同：
    /// 清空工作集——什么都不释放，页进备用列表并计入可用内存，下次使用靠软缺页
    /// 换回；之后定期复查，被后台工作带回来的页再清一次。
    /// </summary>
    static class MemoryTrim
    {
        const int TickMs = 5000;                // 复查间隔
        const int IdleBeforeTrimMs = 20000;     // 隐藏后先等这么久，避免马上又打开时白清
        const int SettleAfterActivityMs = 5000; // 最近处理过消息，等静默了再清
        const int MinTrimIntervalMs = 30000;    // 两次清空之间至少隔这么久
        const long TrimAboveBytes = 32L * 1024 * 1024; // 工作集低于此值就没有可交还的页

        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetProcessWorkingSetSizeEx(IntPtr process, IntPtr minimum, IntPtr maximum, uint flags);

        static Timer timer;
        static int started;
        static long hiddenSinceTicks;   // 0 = 窗口可见或未启动
        static long lastActivityTicks;  // 最近一次收到消息
        static long lastTrimTicks;
        static int trimming;            // 清空期间（约 10ms/100MB）不叠另一次
        static int trims;               // 实际执行清空的次数，供测试与日志
        static int trimLogged;          // 每段隐藏期只写一条日志，避免刷 app.log

        internal static int TrimCount { get { return Interlocked.CompareExchange(ref trims, 0, 0); } }

        internal static void EnsureStarted()
        {
            if (Interlocked.CompareExchange(ref started, 1, 0) != 0) return;
            timer = new Timer(delegate { Tick(); }, null, TickMs, TickMs);
        }

        /// <summary>主窗口可见性变化：隐藏即开始计时；可见时撤销（不清正在使用的窗口）。</summary>
        internal static void SetWindowVisible(bool visible)
        {
            Interlocked.Exchange(ref hiddenSinceTicks, visible ? 0L : DateTime.UtcNow.Ticks);
            if (visible) Interlocked.Exchange(ref trimLogged, 0);
        }

        /// <summary>识别出验证码的消息开始入库/投递时调用：静默 SettleAfterActivityMs 之后才允许清空。</summary>
        internal static void PokeActivity()
        {
            Interlocked.Exchange(ref lastActivityTicks, DateTime.UtcNow.Ticks);
        }

        static void Tick()
        {
            try
            {
                long hidden = Interlocked.Read(ref hiddenSinceTicks);
                if (hidden == 0) return;
                long now = DateTime.UtcNow.Ticks;
                if (now - hidden < IdleBeforeTrimMs * TimeSpan.TicksPerMillisecond) return;
                if (now - Interlocked.Read(ref lastActivityTicks) < SettleAfterActivityMs * TimeSpan.TicksPerMillisecond) return;
                if (now - Interlocked.Read(ref lastTrimTicks) < MinTrimIntervalMs * TimeSpan.TicksPerMillisecond) return;
                long workingSet = Environment.WorkingSet;
                if (workingSet <= TrimAboveBytes) return;
                if (Interlocked.CompareExchange(ref trimming, 1, 0) != 0) return;
                try
                {
                    bool ok = SetProcessWorkingSetSizeEx(GetCurrentProcess(), new IntPtr(-1), new IntPtr(-1), 0);
                    Interlocked.Increment(ref trims);
                    if (Interlocked.Exchange(ref trimLogged, 1) == 0)
                        Log.Write("idle trim: working set " + (workingSet / (1024 * 1024)) + " MB"
                            + (ok ? " released to standby" : " release failed, error " + Marshal.GetLastWin32Error()));
                }
                finally
                {
                    Interlocked.Exchange(ref lastTrimTicks, DateTime.UtcNow.Ticks);
                    Interlocked.Exchange(ref trimming, 0);
                }
            }
            catch { }
        }
    }
}
