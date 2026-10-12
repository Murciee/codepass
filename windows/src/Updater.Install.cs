using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Threading;

namespace Codepass
{
    static partial class Updater
    {
        [DataContract]
        internal sealed class InstallPlan
        {
            [DataMember] public string TargetPath;
            [DataMember] public string StageDirectory;
            [DataMember] public string Version;
            [DataMember] public string ExeSha256;
            [DataMember] public string OriginalSha256;
            [DataMember] public int ParentId;
            [DataMember] public long ParentStartedUtcTicks;
            internal string BackupPath;
        }

        static void EnsurePlainDirectory(string directory)
        {
            for (string current = Path.GetFullPath(directory); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("更新目录不能经过链接，请从普通本地目录运行");
            Directory.CreateDirectory(directory);
        }

        static void ValidatePlan(InstallPlan plan)
        {
            if (plan == null) throw new IOException("更新计划无效");
            string target = Path.GetFullPath(plan.TargetPath);
            string stage = Path.GetFullPath(plan.StageDirectory);
            string root = Path.Combine(Path.GetDirectoryName(target), ".codepass-update");
            if (!string.Equals(Path.GetDirectoryName(stage), root, StringComparison.OrdinalIgnoreCase) ||
                !System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(stage), "^[a-f0-9]{32}$") ||
                !string.Equals(Path.GetFileName(target), "codepass.exe", StringComparison.OrdinalIgnoreCase))
                throw new IOException("更新目标路径无效");
            EnsurePlainDirectory(stage);
            foreach (string file in new string[] { target, Path.Combine(stage, "codepass.exe"), Path.Combine(stage, "plan.json") })
                if (File.Exists(file) && (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException("更新文件不能是链接");
            ValidateExecutable(Path.Combine(stage, "codepass.exe"), plan.Version, plan.ExeSha256);
        }

        internal static void StartInstaller(InstallPlan plan)
        {
            ValidatePlan(plan);
            string helper = Path.Combine(plan.StageDirectory, "update-helper.exe");
            Process process = Process.Start(new ProcessStartInfo(helper, "--apply-update")
            { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = plan.StageDirectory });
            if (process == null) throw new IOException("无法启动更新助手");
            process.Dispose();
        }

        internal static void CancelInstall(InstallPlan plan)
        {
            // 助手等待当前进程退出；取消文件让它退出，不覆盖正在使用的程序。
            File.WriteAllText(Path.Combine(plan.StageDirectory, "cancel"), "cancelled");
            ScheduleStageRecycle(plan.StageDirectory, "cancelled");
        }

        internal static bool TryRunInstaller(string[] args)
        {
            if (args == null || args.Length != 1 || args[0] != "--apply-update") return false;
            string stage = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            InstallPlan plan = null;
            bool parentExited = false;
            try
            {
                using (Stream input = File.OpenRead(Path.Combine(stage, "plan.json"))) plan = ReadJson<InstallPlan>(input);
                if (!string.Equals(stage, plan.StageDirectory, StringComparison.OrdinalIgnoreCase)) throw new IOException("更新助手目录不匹配");
                ValidatePlan(plan);
                using (Mutex gate = new Mutex(false, "Codepass_UpdateInstall"))
                {
                    bool owns;
                    try { owns = gate.WaitOne(0); } catch (AbandonedMutexException) { owns = true; }
                    if (!owns) throw new IOException("另一个更新正在进行");
                    try
                    {
                        WaitForParent(plan);
                        if (File.Exists(Path.Combine(stage, "cancel")))
                        {
                            File.WriteAllText(Path.Combine(stage, "cancelled"), "cancelled");
                            return true;
                        }
                        parentExited = true;
                        InstallFiles(plan);
                        Restart(plan.TargetPath, stage, true);
                        File.WriteAllText(Path.Combine(stage, "complete"), "complete");
                    }
                    finally { gate.ReleaseMutex(); }
                }
            }
            catch (Exception ex)
            {
                bool recovered = false;
                if (parentExited && plan != null)
                {
                    try
                    {
                        RestoreOriginal(plan);
                        if (File.Exists(plan.TargetPath) && string.Equals(HashFile(plan.TargetPath), plan.OriginalSha256, StringComparison.OrdinalIgnoreCase))
                        {
                            Restart(plan.TargetPath, stage, false);
                            recovered = true;
                        }
                    }
                    catch { }
                }
                try { File.WriteAllText(Path.Combine(stage, "install-error.txt"), ex.GetType().Name + ": " + ex.Message); } catch { }
                System.Windows.MessageBox.Show("更新未完成：" + ex.Message + (recovered ? "\n已恢复并启动旧版本。" : "\n请重新打开原目录中的 codepass.exe。") +
                    "\n安装日志保存在更新目录中。", "codepass 更新", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            return true;
        }

        static void WaitForParent(InstallPlan plan)
        {
            Process parent;
            try { parent = Process.GetProcessById(plan.ParentId); }
            catch (ArgumentException) { return; }
            using (parent)
            {
                try { if (parent.StartTime.ToUniversalTime().Ticks != plan.ParentStartedUtcTicks) return; }
                catch (InvalidOperationException) { return; }
                Stopwatch watch = Stopwatch.StartNew();
                while (!parent.WaitForExit(200))
                {
                    if (File.Exists(Path.Combine(plan.StageDirectory, "cancel"))) return;
                    if (watch.Elapsed > TimeSpan.FromMinutes(2)) throw new IOException("等待程序退出超时，未替换文件");
                }
            }
        }

        internal static string InstallFiles(InstallPlan plan)
        {
            ValidatePlan(plan);
            if (!string.Equals(HashFile(plan.TargetPath), plan.OriginalSha256, StringComparison.OrdinalIgnoreCase))
                throw new IOException("原程序已发生变化，请重新检查更新");
            string backupRoot = Path.Combine(Path.GetDirectoryName(plan.TargetPath), "backups");
            EnsurePlainDirectory(backupRoot);
            string backup = Path.Combine(backupRoot, "codepass-before-update-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".exe");
            plan.BackupPath = backup;
            string candidate = Path.Combine(plan.StageDirectory, "codepass.exe");
            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                while (true)
                {
                    try { File.Replace(candidate, plan.TargetPath, backup); break; }
                    catch (IOException)
                    {
                        if (!File.Exists(candidate) || File.Exists(backup) || watch.ElapsedMilliseconds > 10000) throw;
                        Thread.Sleep(200);
                    }
                }
                ValidateExecutable(plan.TargetPath, plan.Version, plan.ExeSha256);
            }
            catch
            {
                // ReplaceFile 部分失败时旧文件可能已移到备份，目标路径可能不存在。
                RestoreOriginal(plan);
                throw;
            }
            return backup;
        }

        internal static void RestoreOriginal(InstallPlan plan)
        {
            if (string.IsNullOrEmpty(plan.BackupPath) || !File.Exists(plan.BackupPath)) return;
            if (!string.Equals(HashFile(plan.BackupPath), plan.OriginalSha256, StringComparison.OrdinalIgnoreCase)) throw new IOException("旧程序备份校验失败");
            if (File.Exists(plan.TargetPath) && string.Equals(HashFile(plan.TargetPath), plan.OriginalSha256, StringComparison.OrdinalIgnoreCase)) return;
            string restore = Path.Combine(plan.StageDirectory, "restore-" + Guid.NewGuid().ToString("N") + ".exe");
            File.Copy(plan.BackupPath, restore);
            if (File.Exists(plan.TargetPath)) File.Replace(restore, plan.TargetPath, Path.Combine(plan.StageDirectory, "failed-" + Guid.NewGuid().ToString("N") + ".exe"));
            else File.Move(restore, plan.TargetPath);
        }

        static void Restart(string target, string stage, bool confirmReady)
        {
            Process process = Process.Start(new ProcessStartInfo(target, confirmReady ? "--updated " + Path.GetFileName(stage) : "")
            { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(target) });
            if (process == null) throw new IOException("无法启动更新后的程序");
            using (process)
            {
                if (!confirmReady) return;
                string ready = Path.Combine(stage, "ready-" + process.Id);
                Stopwatch watch = Stopwatch.StartNew();
                while (watch.Elapsed < TimeSpan.FromSeconds(30))
                {
                    if (process.HasExited) throw new IOException("新版启动失败");
                    if (File.Exists(ready)) return;
                    process.WaitForExit(100);
                }
                try
                {
                    process.CloseMainWindow();
                    if (!process.WaitForExit(5000)) throw new IOException("新版未响应关闭请求");
                }
                catch { }
                throw new IOException("新版未完成启动确认");
            }
        }

        internal static void CleanupCompletedUpdate(string[] args)
        {
            if (args == null || args.Length != 2 || args[0] != "--updated" ||
                !System.Text.RegularExpressions.Regex.IsMatch(args[1], "^[a-f0-9]{32}$")) return;
            string stage = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), ".codepass-update", args[1]);
            if (!Directory.Exists(stage) || (File.GetAttributes(stage) & FileAttributes.ReparsePoint) != 0) return;
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(delegate
            {
                try { File.WriteAllText(Path.Combine(stage, "ready-" + Process.GetCurrentProcess().Id), AppVersion); }
                catch { Log.Write("update: startup confirmation unavailable"); }
            }));
            ScheduleStageRecycle(stage, "complete");
        }

        static void ScheduleStageRecycle(string stage, string marker)
        {
            System.Windows.Threading.DispatcherTimer timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(5);
            int attempts = 0;
            timer.Tick += delegate
            {
                if (++attempts >= 12) timer.Stop();
                if (!File.Exists(Path.Combine(stage, marker))) return;
                try
                {
                    using (FileStream probe = File.Open(Path.Combine(stage, "update-helper.exe"), FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                }
                catch { return; }
                timer.Stop();
                RecycleStage(stage);
            };
            timer.Start();
        }

        static void RecycleStage(string stage)
        {
            object shell = null, bin = null;
            try
            {
                if (!Directory.Exists(stage) || (File.GetAttributes(stage) & FileAttributes.ReparsePoint) != 0) return;
                if (!System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(stage), "^[a-f0-9]{32}$") ||
                    Path.GetFileName(Path.GetDirectoryName(stage)) != ".codepass-update") return;
                Type type = Type.GetTypeFromProgID("Shell.Application");
                shell = Activator.CreateInstance(type);
                bin = type.InvokeMember("NameSpace", BindingFlags.InvokeMethod, null, shell, new object[] { 10 });
                bin.GetType().InvokeMember("MoveHere", BindingFlags.InvokeMethod, null, bin, new object[] { stage, 1044 });
            }
            catch { Log.Write("update: temporary package retained"); }
            finally
            {
                if (bin != null) Marshal.ReleaseComObject(bin);
                if (shell != null) Marshal.ReleaseComObject(shell);
            }
        }
    }
}
