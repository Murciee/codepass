using System;

namespace Codepass
{
    enum ConnectionState { Off, Connecting, Ready, Retrying, Failed }
    enum OperationState { Success, Failed, Cancelled, Stale }
    sealed class OperationResult
    {
        internal readonly OperationState State;
        internal readonly string Message;
        internal OperationResult(OperationState state, string message) { State = state; Message = message; }
    }
    static class RuntimeStatus
    {
        internal static volatile ConnectionState Http = ConnectionState.Off, Ntfy = ConnectionState.Off;
        internal static volatile string SmsResult = "等待手机发送消息", Clipboard = "尚未复制";
        internal static DateTime LastSmsUtc;
        static long pauseUntil;
        internal static bool AutoCopyPaused { get { return System.Threading.Interlocked.Read(ref pauseUntil) > DateTime.UtcNow.Ticks; } }
        internal static void ToggleAutoCopyPause()
        {
            System.Threading.Interlocked.Exchange(ref pauseUntil, AutoCopyPaused ? 0 : DateTime.UtcNow.AddMinutes(15).Ticks);
            Program.EmailStatusChanged();
        }
        internal static string ConnectionText(ConnectionState value)
        {
            switch (value)
            {
                case ConnectionState.Ready: return "正常";
                case ConnectionState.Connecting: return "正在连接";
                case ConnectionState.Retrying: return "断开，正在重试";
                case ConnectionState.Failed: return "连接失败";
                default: return "未启用";
            }
        }
    }
}
