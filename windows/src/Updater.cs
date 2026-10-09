// 检查 GitHub Release，有新版本时打开固定仓库下载页，由用户手动更新。
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace Codepass
{
    static class Updater
    {
        // 当前程序版本：发布新版本时同步修改此处，并保证不小于 Release tag。
        internal const string AppVersion = "1.0.3";

        // 实际仓库地址（用于 GitHub 主页与版本检查）。
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

        internal static void CheckForUpdate(Action<string> report, Action onFinished)
        {
            Thread worker = new Thread(delegate()
            {
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

                    Process.Start(new ProcessStartInfo(RepoUrl + "/releases/latest") { UseShellExecute = true });
                    report("发现 v" + remote + "，已打开下载页；退出程序后手动覆盖 codepass.exe，保留配置和记录。");
                }
                catch (Exception ex)
                {
                    Log.Write("update failed: " + ex.Message);
                    report("更新失败：" + ex.Message + "（可手动下载新版覆盖）");
                }
                finally
                {
                    if (onFinished != null) onFinished();
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
            req.AllowAutoRedirect = false;
            req.Timeout = 20000;
            req.ReadWriteTimeout = 20000;
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
            {
                if ((int)resp.StatusCode != 200) throw new IOException("版本检查响应不是 HTTP 200");
                return reader.ReadToEnd();
            }
        }

        // ---------- 解析与校验 ----------

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

    }
}
