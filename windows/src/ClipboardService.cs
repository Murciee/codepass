using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Codepass
{
    // 拥有消息窗口、请求号、后台 STA 重试和清理计时器；不依赖完整主窗口。
    sealed class ClipboardService : IDisposable
    {
        readonly Func<Config> configuration;
        readonly Func<Dispatcher> dispatcher;
        readonly Action statusChanged;
        readonly Action<bool, string, string, bool> completed;
        readonly object writeGate = new object();
        HwndSource clipboardHost;
        IntPtr clipboardOwner;
        DispatcherTimer clearTimer;
        long clipboardRequest;
        bool disposed;

        internal delegate bool ClipboardWriter(string text, IntPtr owner, out uint sequence, out string error);
        internal ClipboardWriter WriteClipboard = SetClipboardTextWin32;

        internal ClipboardService(Func<Config> configuration, Func<Dispatcher> dispatcher, Action statusChanged,
            Action<bool, string, string, bool> completed)
        { this.configuration = configuration; this.dispatcher = dispatcher; this.statusChanged = statusChanged; this.completed = completed; }

        internal void Initialize()
        {
            if (disposed) throw new ObjectDisposedException("ClipboardService");
            if (clipboardHost != null) return;
            HwndSourceParameters host = new HwndSourceParameters("codepass clipboard");
            host.Width = 0; host.Height = 0; host.WindowStyle = 0; host.ParentWindow = new IntPtr(-3);
            clipboardHost = new HwndSource(host); clipboardOwner = clipboardHost.Handle;
        }

        public void Dispose()
        {
            lock (Config.SaveGate)
            lock (writeGate)
            {
                if (disposed) return;
                disposed = true; ++clipboardRequest;
                if (clearTimer != null) { clearTimer.Stop(); clearTimer = null; }
                if (clipboardHost != null) { clipboardHost.Dispose(); clipboardHost = null; }
                clipboardOwner = IntPtr.Zero;
            }
        }

        internal bool CopyManually(string text, IntPtr owner)
        {
            lock (Config.SaveGate)
            lock (writeGate)
            {
                if (disposed) return false;
                ++clipboardRequest;
                if (clearTimer != null) { clearTimer.Stop(); clearTimer = null; }
                uint sequence; string error;
                bool success = WriteClipboard(text, owner, out sequence, out error);
                RuntimeStatus.Clipboard = success ? "已手动复制" : "复制失败，请重试";
                statusChanged(); return success;
            }
        }

        internal void StartClipboardWrite(string code, string text, bool notify, Func<bool> canCommit, bool emailChannel)
        {
            long request; IntPtr owner;
            lock (Config.SaveGate)
            lock (writeGate)
            {
                if (disposed) return;
                request = ++clipboardRequest; owner = clipboardOwner;
                RuntimeStatus.Clipboard = "正在复制"; statusChanged();
            }
            Thread worker = new Thread(delegate()
            {
                bool ok = false; uint sequence = 0; string lastError = null; int tries = 0;
                int deadline = unchecked(Environment.TickCount + 10000);
                while (!ok)
                {
                    if (request != Interlocked.Read(ref clipboardRequest) || (canCommit != null && !canCommit()))
                    { Log.Write("clipboard: superseded, stop retry"); CompleteClipboardCancellation(request); return; }
                    tries++;
                    string error = null;
                    try
                    {
                        lock (Config.SaveGate)
                        lock (writeGate)
                        {
                            if (request != clipboardRequest || (canCommit != null && !canCommit()))
                            { CompleteClipboardCancellation(request); return; }
                            if (WriteClipboard(code, owner, out sequence, out error)) ok = true;
                        }
                        if (ok) break;
                    }
                    catch (Exception ex) { error = ex.GetType().Name + ": " + ex.Message; }
                    lastError = error;
                    int remaining = unchecked(deadline - Environment.TickCount);
                    if (remaining <= 0) break;
                    int delay = tries <= 2 ? 150 : (tries <= 10 ? 300 : 600);
                    if (delay > remaining) delay = remaining;
                    Thread.Sleep(delay);
                }
                if (ok) Log.Write("clipboard write succeeded (tries=" + tries + ")");
                else Log.Write("clipboard error: " + (lastError ?? "unknown") + " (tries=" + tries + ", window=10s)");
                if (request != Interlocked.Read(ref clipboardRequest)) return;
                Dispatcher ui = dispatcher();
                if (ui == null) return;
                try
                {
                    ui.BeginInvoke(new Action(delegate
                    {
                        try
                        {
                            lock (Config.SaveGate)
                            {
                                if (request != Interlocked.Read(ref clipboardRequest)) return;
                                CompleteClipboardClear(ok, sequence, request);
                                RuntimeStatus.Clipboard = ok ? "已复制" : "复制失败，可手动重试";
                                statusChanged();
                                if (canCommit != null && !canCommit()) return;
                                if (notify) completed(ok, code, text, emailChannel);
                            }
                        }
                        catch (Exception e) { Log.Write("ui error: " + e.Message); }
                    }));
                }
                catch (Exception e) { Log.Write("invoke error: " + e.Message); }
            });
            worker.IsBackground = true; worker.SetApartmentState(ApartmentState.STA); worker.Start();
        }

        internal void CompleteClipboardCancellation(long request)
        {
            lock (Config.SaveGate)
            {
                if (request != Interlocked.Read(ref clipboardRequest)) return;
                RuntimeStatus.Clipboard = "复制已取消"; statusChanged();
            }
        }

        internal void CompleteClipboardClear(bool succeeded, uint sequence, long request)
        {
            lock (Config.SaveGate)
            {
                if (succeeded && request == Interlocked.Read(ref clipboardRequest) && configuration().AutoClearSeconds > 0)
                    ScheduleClear(sequence, request);
            }
        }

        void ScheduleClear(uint sequence, long request)
        {
            if (clearTimer != null) { clearTimer.Stop(); clearTimer = null; }
            if (configuration().AutoClearSeconds <= 0) return;
            DispatcherTimer timer = new DispatcherTimer(); clearTimer = timer;
            timer.Interval = TimeSpan.FromSeconds(configuration().AutoClearSeconds);
            timer.Tick += delegate
            {
                timer.Stop(); if (clearTimer == timer) clearTimer = null;
                lock (Config.SaveGate)
                lock (writeGate)
                {
                    if (request != clipboardRequest || sequence == 0 || GetClipboardSequenceNumber() != sequence) return;
                    if (!OpenClipboard(clipboardOwner)) return;
                    try { if (GetClipboardSequenceNumber() == sequence) EmptyClipboard(); }
                    finally { CloseClipboard(); }
                }
            };
            timer.Start();
        }

        [DllImport("user32.dll", SetLastError = true)] static extern bool OpenClipboard(IntPtr owner);
        [DllImport("user32.dll", SetLastError = true)] static extern bool EmptyClipboard();
        [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetClipboardData(uint format, IntPtr memory);
        [DllImport("user32.dll", SetLastError = true)] static extern bool CloseClipboard();
        [DllImport("user32.dll")] static extern uint GetClipboardSequenceNumber();
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr GlobalLock(IntPtr memory);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool GlobalUnlock(IntPtr memory);
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr GlobalFree(IntPtr memory);

        static bool SetClipboardTextWin32(string text, IntPtr owner, out uint sequence, out string error)
        {
            error = null; sequence = 0;
            if (owner == IntPtr.Zero || !OpenClipboard(owner))
            { error = "OpenClipboard failed, win32=" + Marshal.GetLastWin32Error(); return false; }
            IntPtr memory = IntPtr.Zero;
            try
            {
                byte[] payload = Encoding.Unicode.GetBytes(text + "\0");
                memory = GlobalAlloc(0x0002, (UIntPtr)payload.Length);
                if (memory == IntPtr.Zero) { error = "GlobalAlloc failed, win32=" + Marshal.GetLastWin32Error(); return false; }
                IntPtr target = GlobalLock(memory);
                if (target == IntPtr.Zero) { error = "GlobalLock failed, win32=" + Marshal.GetLastWin32Error(); return false; }
                try { Marshal.Copy(payload, 0, target, payload.Length); }
                finally { GlobalUnlock(memory); }
                if (!EmptyClipboard()) { error = "EmptyClipboard failed, win32=" + Marshal.GetLastWin32Error(); return false; }
                if (SetClipboardData(13, memory) == IntPtr.Zero)
                { error = "SetClipboardData failed, win32=" + Marshal.GetLastWin32Error(); return false; }
                memory = IntPtr.Zero; sequence = GetClipboardSequenceNumber(); return true;
            }
            finally { if (memory != IntPtr.Zero) GlobalFree(memory); CloseClipboard(); }
        }
    }
}
