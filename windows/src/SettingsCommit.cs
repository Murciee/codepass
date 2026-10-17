using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;

namespace Codepass
{
    [Flags]
    enum SettingsSection { Sms = 1, SmsRules = 2, Ntfy = 4, Email = 8, EmailRules = 16, Privacy = 32, Retention = 64, Window = 128, All = 63 }

    sealed class SettingsCommitException : IOException
    {
        internal readonly bool RecoveryFailed;
        internal SettingsCommitException(Exception cause, bool recoveryFailed)
            : base("设置保存失败", cause) { RecoveryFailed = recoveryFailed; }
    }

    static class SettingsCommit
    {
        internal static void Merge(Config target, Config draft, SettingsSection section)
        {
            if ((section & SettingsSection.Sms) != 0)
                target.Token = draft.Token;
            if ((section & SettingsSection.Ntfy) != 0)
            { target.NtfyTopic = draft.NtfyTopic; target.NtfyToken = draft.NtfyToken; }
            if ((section & SettingsSection.Email) != 0)
            {
                bool changed = !string.Equals(target.EmailServer, draft.EmailServer, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(target.EmailAccount, draft.EmailAccount, StringComparison.OrdinalIgnoreCase);
                if (changed)
                {
                    target.EmailMailboxState = "";
                    target.EmailLastUid = target.EmailLastUidJunk = target.EmailUidValidity = target.EmailUidValidityJunk = 0;
                    target.EmailAuthEncRaw = ""; target.EmailAuthUndecryptable = false;
                }
                target.EmailAuth = draft.EmailAuth;
                if (draft.EmailAuth.Length > 0)
                { target.EmailPasswordRequired = false; target.EmailAuthEncRaw = ""; target.EmailAuthUndecryptable = false; }
            }
            ConfigSettings.Merge(target, draft, section);
            if ((section & SettingsSection.Privacy) != 0)
                target.AutoLockEnabled = target.LockEnabled && draft.AutoLockEnabled;
            if ((section & SettingsSection.Window) != 0)
            {
                target.WindowWidth = draft.WindowWidth; target.WindowHeight = draft.WindowHeight;
                // 只有真正测到位置时才覆盖，避免其它路径的窗口草稿把记忆位置清成 (0,0)。
                if (draft.WindowPositionIncluded)
                {
                    target.WindowLeft = draft.WindowLeft; target.WindowTop = draft.WindowTop;
                    target.WindowPositionIncluded = true;
                }
            }
        }

        // 提交时从最新运行配置合并，避免旧草稿覆盖后台刚推进的邮件水位。
        internal static void Commit(Config current, Config draft, SettingsSection section, string path)
        {
            lock (Config.SaveGate)
            {
                Config previous = new Config(); previous.CopyFrom(current);
                Config next = new Config(); next.CopyFrom(current); Merge(next, draft, section);
                if (Equivalent(previous, next) && !Program.StartupSyncFailed && Config.LastSaveError == null) return;
                bool startup = previous.StartWithWindows != next.StartWithWindows || Program.StartupSyncFailed;
                bool saved = false;
                try
                {
                    next.Save(path); saved = true;
                    if (startup) Program.SetStartup(next.StartWithWindows);
                    current.CopyFrom(next);
                }
                catch (Exception failure)
                {
                    bool recoveryFailed = false;
                    try { if (saved) previous.Save(path); } catch { recoveryFailed = true; }
                    try { if (startup && saved) Program.SetStartup(previous.StartWithWindows); } catch { recoveryFailed = true; }
                    throw new SettingsCommitException(failure, recoveryFailed);
                }
            }
        }
        internal static bool Equivalent(Config a, Config b)
        {
            // 只比较配置值；水位等字段仍来自同一份最新快照，不比较属性或密文输出。
            foreach (System.Reflection.FieldInfo field in typeof(Config).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
                if (!object.Equals(field.GetValue(a), field.GetValue(b))) return false;
            return true;
        }
    }

    // 单一消费者只在后台写盘；Drain 用于退出/导入等必须完成已有事务的边界。
    sealed class SettingsCommitQueue
    {
        sealed class Work { internal Action Save; internal Action<Exception> Complete; }
        readonly object gate = new object();
        readonly Queue<Work> pending = new Queue<Work>();
        bool running;
        internal void Enqueue(Action save, Action<Exception> complete)
        {
            lock (gate)
            {
                pending.Enqueue(new Work { Save = save, Complete = complete });
                if (running) return;
                running = true; ThreadPool.QueueUserWorkItem(delegate { Run(); });
            }
        }
        void Run()
        {
            while (true)
            {
                Work work;
                lock (gate)
                {
                    if (pending.Count == 0) { running = false; Monitor.PulseAll(gate); return; }
                    work = pending.Dequeue();
                }
                Exception error = null;
                try { work.Save(); } catch (Exception ex) { error = ex; }
                try { if (work.Complete != null) work.Complete(error); } catch { }
            }
        }
        internal void Drain() { lock (gate) while (running) Monitor.Wait(gate); }
    }
}
