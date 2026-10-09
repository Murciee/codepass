// 自动更新：检查 GitHub Release -> 下载 codepass.exe（或从 codepass-windows.zip 解出）
//          -> 替换自身 -> 自动重启。
// 约定：Release tag 形如 v1.0.0；附件提供 codepass.exe 或 codepass-windows.zip（二者任一）。
// 仓库地址用于“关于”中的 GitHub 主页与自动更新（见 Updater.RepoUrl）。
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace Codepass
{
    static class Updater
    {
        // 当前程序版本：发布新版本时同步修改此处，并保证不小于 Release tag。
        internal const string AppVersion = "1.0.0";

        // 实际仓库地址（同时用于“关于”中的 GitHub 主页与自动更新）。
        internal const string RepoUrl = "https://github.com/Murciee/codepass";

        internal static bool IsPlaceholderRepo
        {
            get { return RepoUrl.IndexOf("USERNAME", StringComparison.OrdinalIgnoreCase) >= 0; }
        }

        internal static string ApiUrl
        {
            get
            {
#if CODEPASS_TEST
                string test = Environment.GetEnvironmentVariable("CODEPASS_UPDATE_API");
                if (!string.IsNullOrEmpty(test)) return test;
#endif
                if (IsPlaceholderRepo) return "";
                return "https://api.github.com/repos/" + new Uri(RepoUrl).AbsolutePath.Trim('/') + "/releases/latest";
            }
        }

        // 上次更新替换下来的 codepass.exe.old，启动时清理（失败忽略）
        internal static void CleanupOldFile()
        {
            try
            {
                string old = Process.GetCurrentProcess().MainModule.FileName + ".old";
                if (File.Exists(old)) File.Delete(old);
            }
            catch { }
        }

        /// <summary>
        /// 全自动更新：检查 -> 下载 -> 替换 -> 重启，全部在后台线程完成。
        /// report    状态文案回调（后台线程调用，调用方自行切回 UI 线程）
        /// onRestart 替换完成、即将退出应用（调用方负责退出；随后由独立进程拉起新版本）
        /// onFinished 未更新或更新失败（调用方恢复界面，如重新启用按钮）
        /// </summary>
        internal static void RunFullyAutomatic(Action<string> report, Action onRestart, Action onFinished)
        {
            Thread worker = new Thread(delegate()
            {
                bool restarting = false;
                try
                {
                    string api = ApiUrl;
                    if (api.Length == 0)
                    {
                        report("尚未配置 GitHub 仓库地址（发布前请替换源码中的占位地址）");
                        return;
                    }

                    report("正在检查更新…");
                    string json = HttpGet(api);
                    string tag = FirstMatch(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                    if (tag.Length == 0 || !Regex.IsMatch(tag, "\\d"))
                        throw new Exception("无法识别最新版本号");

                    string remote = TrimVersion(tag);
                    if (!IsNewer(tag, AppVersion))
                    {
                        report("已是最新版本（v" + AppVersion + "）");
                        return;
                    }

                    string assetUrl = FindAssetUrl(json);
                    if (assetUrl.Length == 0)
                        throw new Exception("Release 中没有找到 codepass.exe 或 codepass-windows.zip");

                    string exe = Process.GetCurrentProcess().MainModule.FileName;
                    string dir = Path.GetDirectoryName(exe);
                    string staging = Path.Combine(dir, "codepass.exe.new");
                    try { if (File.Exists(staging)) File.Delete(staging); } catch { }

                    string label = "正在下载 v" + remote;
                    report(label + "…");
                    if (assetUrl.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        string tmpZip = Path.Combine(dir, "codepass-update.zip");
                        try { if (File.Exists(tmpZip)) File.Delete(tmpZip); } catch { }
                        try
                        {
                            Download(assetUrl, tmpZip, label, report);
                            ExtractExe(tmpZip, staging);
                        }
                        finally
                        {
                            try { if (File.Exists(tmpZip)) File.Delete(tmpZip); } catch { }
                        }
                    }
                    else
                    {
                        Download(assetUrl, staging, label, report);
                    }
                    VerifyExe(staging);

                    report("下载完成，正在安装…");
                    string old = exe + ".old";
                    try { if (File.Exists(old)) File.Delete(old); } catch { }
                    File.Move(exe, old);            // 运行中的 exe 允许改名（同一磁盘分区内）
                    try
                    {
                        File.Move(staging, exe);
                    }
                    catch
                    {
                        try { File.Move(old, exe); } catch { }
                        throw;
                    }

                    // 先退出旧进程，再由独立进程拉起新版本，避免单实例互斥把新实例挡掉。
                    int pid = Process.GetCurrentProcess().Id;
                    string psArgs = "-NoProfile -WindowStyle Hidden -Command \"Wait-Process -Id " + pid
                        + " -ErrorAction SilentlyContinue; Start-Sleep -Milliseconds 600; Start-Process -FilePath '"
                        + exe.Replace("'", "''") + "'\"";
                    Process.Start(new ProcessStartInfo("powershell.exe", psArgs)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    });

                    report("更新完成，正在重启…");
                    restarting = true;
                    if (onRestart != null) onRestart();
                }
                catch (Exception ex)
                {
                    Log.Write("update failed: " + ex.Message);
                    report("更新失败：" + ex.Message + "（可手动下载新版覆盖）");
                }
                finally
                {
                    if (!restarting && onFinished != null) onFinished();
                }
            });
            worker.IsBackground = true;
            worker.Name = "codepass-update";
            worker.Start();
        }

        // ---------- 网络 ----------

        static string HttpGet(string url)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "codepass/" + AppVersion;
            req.Accept = "application/vnd.github+json";
            req.Timeout = 20000;
            req.ReadWriteTimeout = 20000;
            using (WebResponse resp = req.GetResponse())
            using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                return reader.ReadToEnd();
        }

        static void Download(string url, string dest, string label, Action<string> report)
        {
            using (WebClient wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.UserAgent] = "codepass/" + AppVersion;
                wc.Proxy = WebRequest.DefaultWebProxy;   // 跟随系统代理
                int lastPct = -10;
                wc.DownloadProgressChanged += delegate(object s, DownloadProgressChangedEventArgs e)
                {
                    int pct = e.ProgressPercentage;
                    if (pct >= lastPct + 10 || pct >= 100)
                    {
                        lastPct = pct;
                        report(label + "… " + pct + "%");
                    }
                };
                ManualResetEvent done = new ManualResetEvent(false);
                Exception error = null;
                wc.DownloadFileCompleted += delegate(object s, AsyncCompletedEventArgs e)
                {
                    error = e.Error;
                    done.Set();
                };
                wc.DownloadFileAsync(new Uri(url), dest);
                done.WaitOne();
                if (error != null) throw error;
            }
        }

        // ---------- 解析与校验 ----------

        static string FindAssetUrl(string json)
        {
            string zip = "";
            foreach (Match m in Regex.Matches(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+)\""))
            {
                string u = m.Groups[1].Value;
                if (u.EndsWith("/codepass.exe", StringComparison.OrdinalIgnoreCase)) return u;
                if (zip.Length == 0 && u.EndsWith("/codepass-windows.zip", StringComparison.OrdinalIgnoreCase)) zip = u;
            }
            return zip;
        }

        static string FirstMatch(string text, string pattern)
        {
            Match m = Regex.Match(text ?? "", pattern);
            return m.Success ? m.Groups[1].Value : "";
        }

        static string TrimVersion(string tag)
        {
            return (tag ?? "").TrimStart('v', 'V', ' ', '\t');
        }

        static bool IsNewer(string tag, string local)
        {
            int[] remote = ParseVersion(tag);
            int[] current = ParseVersion(local);
            for (int i = 0; i < 3; i++)
                if (remote[i] != current[i]) return remote[i] > current[i];
            return false;
        }

        static int[] ParseVersion(string text)
        {
            int[] v = new int[3];
            Match m = Regex.Match(text ?? "", "(\\d+)(?:\\.(\\d+))?(?:\\.(\\d+))?");
            if (!m.Success) return v;
            v[0] = int.Parse(m.Groups[1].Value);
            if (m.Groups[2].Success) v[1] = int.Parse(m.Groups[2].Value);
            if (m.Groups[3].Success) v[2] = int.Parse(m.Groups[3].Value);
            return v;
        }

        static void ExtractExe(string zipPath, string dest)
        {
            using (ZipArchive zip = ZipFile.OpenRead(zipPath))
            {
                ZipArchiveEntry entry = null;
                foreach (ZipArchiveEntry e in zip.Entries)
                {
                    if (string.Equals(e.Name, "codepass.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        entry = e;
                        break;
                    }
                }
                if (entry == null) throw new Exception("更新包中没有 codepass.exe");
                using (Stream input = entry.Open())
                using (FileStream output = new FileStream(dest, FileMode.Create, FileAccess.Write))
                    input.CopyTo(output);
            }
        }

        static void VerifyExe(string path)
        {
            if (!File.Exists(path)) throw new Exception("下载的文件不存在");
            if (new FileInfo(path).Length < 64 * 1024) throw new Exception("下载的文件不完整");
            byte[] head = new byte[2];
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                if (fs.Read(head, 0, 2) != 2) throw new Exception("下载的文件不完整");
            }
            if (head[0] != (byte)'M' || head[1] != (byte)'Z') throw new Exception("下载的文件不是有效的程序");
        }
    }
}
