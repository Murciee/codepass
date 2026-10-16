// 检查固定 GitHub 仓库的稳定版；下载校验完成后由独立辅助进程替换程序。
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Codepass
{
    static partial class Updater
    {
        // 当前程序版本：发布新版本时同步修改此处，并保证不小于 Release tag。
        internal const string AppVersion = "1.1.5";

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

        const long MaxPackageBytes = 64 * 1024 * 1024;
        const long MaxExeBytes = 32 * 1024 * 1024;

        [DataContract]
        internal sealed class ReleaseInfo
        {
            [DataMember(Name = "tag_name")] public string Tag { get; set; }
            [DataMember(Name = "draft")] public bool Draft { get; set; }
            [DataMember(Name = "prerelease")] public bool Prerelease { get; set; }
            [DataMember(Name = "assets")] public ReleaseAsset[] Assets { get; set; }
        }

        [DataContract]
        internal sealed class ReleaseAsset
        {
            [DataMember(Name = "name")] public string Name { get; set; }
            [DataMember(Name = "browser_download_url")] public string Url { get; set; }
            [DataMember(Name = "size")] public long Size { get; set; }
            [DataMember(Name = "digest")] public string Digest { get; set; }
        }

        [DataContract]
        sealed class PackageInfo
        {
            [DataMember(Name = "version")] public string Version { get; set; }
            [DataMember(Name = "exeSha256")] public string ExeSha256 { get; set; }
        }

        internal static void CheckForUpdate(Action<string> report, Action<ReleaseInfo> onAvailable, Action onFinished)
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
                    ReleaseInfo release = ParseRelease(HttpGet(api));
                    if (!IsNewer(release.Tag, AppVersion))
                    {
                        report("已是最新版本（v" + AppVersion + "）");
                        return;
                    }

                    SelectAsset(release);
                    report("发现 v" + TrimVersion(release.Tag) + "，点击“下载并更新”自动安装并重启。");
                    onAvailable(release);
                }
                catch (Exception ex)
                {
                    Log.Write("update check failed: " + ex.GetType().Name);
                    report("检查失败：" + ex.Message + "；可重试或从 GitHub 主页下载。");
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

        internal static void DownloadUpdate(ReleaseInfo release, Action<string> report, Action<InstallPlan> onReady, Action onFinished)
        {
            Thread worker = new Thread(delegate()
            {
                string stage = null;
                bool handedOff = false;
                try
                {
                    ReleaseAsset asset = SelectAsset(release);
                    string target = Assembly.GetExecutingAssembly().Location;
                    string parent = Path.Combine(Path.GetDirectoryName(target), ".codepass-update");
                    EnsurePlainDirectory(parent);
                    stage = Path.Combine(parent, Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(stage);
                    string package = Path.Combine(stage, "package.zip");
                    report("正在下载 v" + TrimVersion(release.Tag) + "…");
                    DownloadPackage(asset, package, report);
                    report("正在校验安装包…");
                    InstallPlan plan = PreparePackage(release, package, target, stage);
                    plan.ParentId = Process.GetCurrentProcess().Id;
                    plan.ParentStartedUtcTicks = Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks;
                    string helper = Path.Combine(stage, "update-helper.exe");
                    File.Copy(target, helper);
                    WriteJson(Path.Combine(stage, "plan.json"), plan);
                    onReady(plan);
                    handedOff = true;
                }
                catch (Exception ex)
                {
                    Log.Write("update download failed: " + ex.GetType().Name);
                    report("更新失败：" + ex.Message + "；当前程序可继续使用，点击按钮重试。");
                }
                finally
                {
                    if (!handedOff && stage != null) RecycleStage(stage);
                    if (onFinished != null) onFinished();
                }
            });
            worker.IsBackground = true;
            worker.Name = "codepass-update-download";
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
                char[] buffer = new char[4096];
                StringBuilder json = new StringBuilder();
                int count;
                while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (json.Length + count > 1024 * 1024) throw new IOException("版本响应过大");
                    json.Append(buffer, 0, count);
                }
                return json.ToString();
            }
        }

        static bool AllowedDownloadUrl(Uri uri)
        {
            return uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && uri.UserInfo.Length == 0 &&
                (uri.Host == "github.com" || uri.Host == "release-assets.githubusercontent.com" || uri.Host == "objects.githubusercontent.com");
        }

        static void DownloadPackage(ReleaseAsset asset, string destination, Action<string> report)
        {
            Uri uri = new Uri(asset.Url);
            for (int hop = 0; hop < 6; hop++)
            {
                if (!AllowedDownloadUrl(uri)) throw new IOException("安装包下载地址不受支持");
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
                request.UserAgent = "codepass/" + AppVersion;
                request.AllowAutoRedirect = false;
                request.Timeout = request.ReadWriteTimeout = 30000;
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    int code = (int)response.StatusCode;
                    if (code == 301 || code == 302 || code == 303 || code == 307 || code == 308)
                    {
                        string location = response.Headers["Location"];
                        if (string.IsNullOrEmpty(location)) throw new IOException("下载重定向缺少地址");
                        uri = new Uri(uri, location);
                        continue;
                    }
                    if (code != 200) throw new IOException("安装包响应不是 HTTP 200");
                    if (response.ContentLength > MaxPackageBytes || (response.ContentLength >= 0 && response.ContentLength != asset.Size))
                        throw new IOException("安装包大小不匹配");
                    using (Stream input = response.GetResponseStream())
                    using (FileStream output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        byte[] buffer = new byte[65536];
                        long total = 0;
                        int count, lastPercent = -1;
                        while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            total += count;
                            if (total > asset.Size || total > MaxPackageBytes) throw new IOException("安装包超过预期大小");
                            output.Write(buffer, 0, count);
                            int percent = (int)(total * 100 / asset.Size);
                            if (percent != lastPercent) { report("正在下载更新… " + percent + "%"); lastPercent = percent; }
                        }
                        if (total != asset.Size) throw new IOException("下载中断，安装包不完整");
                        output.Flush(true);
                    }
                    return;
                }
            }
            throw new IOException("下载重定向次数过多");
        }

        // ---------- 解析与校验 ----------

        static T ReadJson<T>(Stream stream)
        {
            // build.ps1 的 UTF-8 元数据带 BOM；先按文本解码再送入 JSON 解析器。
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                char[] buffer = new char[4096];
                StringBuilder text = new StringBuilder();
                int count;
                while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (text.Length + count > 1024 * 1024) throw new IOException("JSON 文件过大");
                    text.Append(buffer, 0, count);
                }
                using (MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(text.ToString())))
                    return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(input);
            }
        }

        static void WriteJson<T>(string path, T value)
        {
            using (FileStream output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(output, value);
                output.Flush(true);
            }
        }

        internal static ReleaseInfo ParseRelease(string json)
        {
            ReleaseInfo release;
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json))) release = ReadJson<ReleaseInfo>(stream);
            if (release == null || release.Draft || release.Prerelease) throw new IOException("未找到可用的稳定版本");
            ParseVersion(release.Tag);
            return release;
        }

        internal static ReleaseAsset SelectAsset(ReleaseInfo release)
        {
            ParseVersion(release.Tag);
            string expected = RepoUrl + "/releases/download/" + Uri.EscapeDataString(release.Tag) + "/codepass-windows.zip";
            ReleaseAsset found = null;
            foreach (ReleaseAsset asset in release.Assets ?? new ReleaseAsset[0])
            {
                if (asset.Name != "codepass-windows.zip") continue;
                if (found != null || asset.Url != expected || asset.Size <= 0 || asset.Size > MaxPackageBytes)
                    throw new IOException("Windows 安装包信息无效");
                if (!string.IsNullOrEmpty(asset.Digest) && !Regex.IsMatch(asset.Digest, "^sha256:[0-9a-fA-F]{64}$"))
                    throw new IOException("安装包校验信息无效");
                found = asset;
            }
            if (found == null) throw new IOException("该版本尚未提供 Windows 安装包");
            return found;
        }

        static string HashFile(string path)
        {
            using (SHA256 hash = SHA256.Create())
            using (Stream input = File.OpenRead(path)) return BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
        }

        internal static InstallPlan PreparePackage(ReleaseInfo release, string package, string target, string stage)
        {
            ReleaseAsset asset = SelectAsset(release);
            if (new FileInfo(package).Length != asset.Size) throw new IOException("安装包大小不匹配");
            if (!string.IsNullOrEmpty(asset.Digest) && !string.Equals(asset.Digest.Substring(7), HashFile(package), StringComparison.OrdinalIgnoreCase))
                throw new IOException("安装包 SHA256 校验失败");
            string staged = Path.Combine(stage, "codepass.exe");
            PackageInfo metadata;
            using (ZipArchive zip = ZipFile.OpenRead(package))
            {
                ZipArchiveEntry exe = null, info = null;
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    string name = entry.FullName.Replace('\\', '/');
                    if (name.StartsWith("/", StringComparison.Ordinal) || name.Contains(":") || Array.IndexOf(name.Split('/'), "..") >= 0)
                        throw new IOException("安装包包含无效路径");
                    if (string.Equals(name, "codepass.exe", StringComparison.OrdinalIgnoreCase))
                    { if (exe != null) throw new IOException("安装包包含重复程序"); exe = entry; }
                    if (string.Equals(name, "build-info.json", StringComparison.OrdinalIgnoreCase))
                    { if (info != null) throw new IOException("安装包包含重复版本信息"); info = entry; }
                }
                if (exe == null || info == null || exe.Length < 1024 || exe.Length > MaxExeBytes || info.Length > 65536)
                    throw new IOException("安装包缺少有效程序或版本信息");
                using (Stream input = info.Open()) metadata = ReadJson<PackageInfo>(input);
                if (metadata == null || metadata.Version != TrimVersion(release.Tag) || !Regex.IsMatch(metadata.ExeSha256 ?? "", "^[0-9a-fA-F]{64}$"))
                    throw new IOException("安装包版本信息不匹配");
                using (Stream input = exe.Open())
                using (FileStream output = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] buffer = new byte[65536];
                    long length = 0;
                    int count;
                    while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        length += count;
                        if (length > exe.Length || length > MaxExeBytes) throw new IOException("程序文件超过预期大小");
                        output.Write(buffer, 0, count);
                    }
                    if (length != exe.Length) throw new IOException("程序文件不完整");
                    output.Flush(true);
                }
            }
            ValidateExecutable(staged, metadata.Version, metadata.ExeSha256);
            return new InstallPlan { TargetPath = Path.GetFullPath(target), StageDirectory = Path.GetFullPath(stage), Version = metadata.Version,
                ExeSha256 = metadata.ExeSha256, OriginalSha256 = HashFile(target) };
        }

        static void ValidateExecutable(string path, string version, string digest)
        {
            if (!string.Equals(HashFile(path), digest, StringComparison.OrdinalIgnoreCase)) throw new IOException("程序 SHA256 校验失败");
            AssemblyName name = AssemblyName.GetAssemblyName(path);
            Version expected = ParseVersion(version);
            if (!string.Equals(name.Name, "codepass", StringComparison.OrdinalIgnoreCase) || name.Version.Major != expected.Major ||
                name.Version.Minor != expected.Minor || name.Version.Build != expected.Build)
                throw new IOException("程序文件与发布版本不匹配");
        }

        static string TrimVersion(string tag) { return (tag ?? "").TrimStart('v', 'V'); }
        static bool IsNewer(string tag, string local) { return ParseVersion(tag).CompareTo(ParseVersion(local)) > 0; }
        static Version ParseVersion(string text)
        {
            Version version;
            string value = TrimVersion(text);
            if (!Regex.IsMatch(value, "^\\d+\\.\\d+\\.\\d+$") || !Version.TryParse(value, out version)) throw new IOException("无法识别版本号");
            return version;
        }
    }
}
