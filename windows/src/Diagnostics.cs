using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace Codepass
{
    static class HistorySearch
    {
        internal static bool Matches(HistoryItem item, string channel, string query)
        {
            if (item == null) return false;
            bool email = item.Channel == "email";
            if (channel == "sms" && email || channel == "email" && !email) return false;
            foreach (string term in (query ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
                if (!Contains(item.Source, term) && !Contains(item.Code, term) && !Contains(item.Raw, term)
                    && !Contains(item.Time, term)) return false;
            return true;
        }

        static bool Contains(string text, string term)
        {
            return (text ?? "").IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    // 从已知字段重新生成报告；不读取 config.ini / history.txt / app.log，也不拼接自由文本。
    static class Diagnostics
    {
        static readonly object Gate = new object();
        static readonly Queue<string> Events = new Queue<string>();
        const int MaxEvents = 40;

        internal static void ObserveLog(string message)
        {
            string category = EventCategory(message ?? "");
            if (category == null) return;
            lock (Gate)
            {
                Events.Enqueue(DateTime.UtcNow.ToString("HH:mm:ss") + " UTC · " + category);
                while (Events.Count > MaxEvents) Events.Dequeue();
            }
        }

        static string EventCategory(string message)
        {
            if (message.StartsWith("email error category=", StringComparison.Ordinal)
                || message.StartsWith("email folders error category=", StringComparison.Ordinal))
            {
                foreach (string category in new string[] { "content", "tls", "protocol_or_auth", "storage", "network", "unexpected" })
                    if (message.EndsWith("=" + category, StringComparison.Ordinal)) return "邮件错误：" + category;
                return "邮件错误：未分类";
            }
            if (message == "history save failed; original retained") return "历史保存失败";
            if (message.StartsWith("config save failed:", StringComparison.Ordinal)) return "配置保存失败";
            if (message.StartsWith("HTTP listen FAILED:", StringComparison.Ordinal)) return "HTTP 监听失败";
            if (message.StartsWith("HTTP listening on ", StringComparison.Ordinal)) return "HTTP 监听已启动";
            if (message.StartsWith("ntfy error:", StringComparison.Ordinal)) return "ntfy 连接失败";
            if (message == "ntfy subscribing") return "ntfy 已开始订阅";
            if (message.StartsWith("clipboard error:", StringComparison.Ordinal)) return "剪贴板写入失败";
            if (message.StartsWith("clipboard write succeeded (tries=", StringComparison.Ordinal)) return "剪贴板写入成功";
            if (message == "[http] SMS skipped: length limit exceeded" || message == "[ntfy] SMS skipped: length limit exceeded") return "短信超过长度上限";
            if (message == "[http] message filtered" || message == "[ntfy] message filtered") return "短信命中过滤规则";
            if (message == "[http] SMS skipped: acceptance keywords not matched" || message == "[ntfy] SMS skipped: acceptance keywords not matched") return "短信未命中接收关键字";
            return null;
        }

        internal static string StatusCategory(string value)
        {
            string text = value ?? "";
            if (text.Contains("失败") || text.Contains("重试")) return "异常 / 重试中";
            if (text.Contains("已连接") && text.Contains("IDLE 不可用")) return "已连接 · 轮询";
            if (text.Contains("已连接") && text.Contains("IDLE")) return "已连接 · 实时 IDLE";
            if (text.Contains("已连接")) return "已连接 · 轮询";
            if (text.Contains("正在连接")) return "连接中";
            if (text.Contains("正在检查")) return "检查中";
            if (text.Contains("没有额外接收目录")) return "无额外接收目录";
            if (text.Contains("目录检查成功")) return "检查成功";
            if (text.Contains("完成")) return "检查完成";
            return "待检查 / 未连接";
        }

        internal static string CreateReport(Config config)
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("codepass 脱敏诊断报告 · 格式 1");
            report.AppendLine("生成时间（UTC）=" + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            report.AppendLine("包含：版本、系统版本、功能开关、数量、归类状态及本次运行最近 40 条已归类事件。");
            report.AppendLine("不包含：验证码、正文、发件人、账号、服务器/IP/路径、主题、规则文本、授权码/令牌、密码锁校验值或原始日志。");
            report.AppendLine();
            report.AppendLine("[运行环境]");
            report.AppendLine("程序版本=" + Updater.AppVersion);
            AssemblyInformationalVersionAttribute stamp = Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute;
            // 构建戳由本仓库构建脚本生成；只接受其限定字符，拒绝外部构建的自由文本。
            string build = stamp == null ? "" : stamp.InformationalVersion;
            report.AppendLine("构建标识=" + (System.Text.RegularExpressions.Regex.IsMatch(build, @"\A[0-9A-Za-z.+-]{1,160}\z") ? build : "不可用"));
            report.AppendLine("Windows 版本=" + Environment.OSVersion.Version);
            report.AppendLine("CLR 版本=" + Environment.Version);
            report.AppendLine("进程位数=" + (IntPtr.Size * 8));
            lock (Config.SaveGate)
            {
                report.AppendLine(); report.AppendLine("[功能与设置]");
                Add(report, "局域网令牌已配置", !string.IsNullOrEmpty(config.Token));
                Add(report, "监听地址已指定", !string.IsNullOrEmpty(config.PcIp));
                report.AppendLine("监听端口=" + config.Port);
                Add(report, "ntfy 已配置", !string.IsNullOrEmpty(config.NtfyTopic));
                Add(report, "ntfy 令牌已配置", !string.IsNullOrEmpty(config.NtfyToken));
                report.AppendLine("验证码长度=" + config.MinLen + "～" + config.MaxLen);
                report.AppendLine("短信长度上限=" + config.SmsMaxLength);
                report.AppendLine("短信关键词条数=" + LineCount(config.FilterKeywords));
                report.AppendLine("短信接收关键字条数=" + LineCount(config.SmsAcceptKeywords));
                report.AppendLine("短信正则条数=" + LineCount(config.FilterRegex));
                report.AppendLine("剪贴板清理秒数=" + config.AutoClearSeconds);
                report.AppendLine("记录保留分钟数=" + config.HistoryRetentionMinutes);
                Add(report, "短信通知开启", config.ShowTip);
                report.AppendLine("短信通知隐私档位=" + config.PrivacyMode);
                report.AppendLine("邮件通知隐私档位=" + config.EmailPrivacyMode);
                Add(report, "邮箱接收开启", config.EmailEnabled);
                Add(report, "邮箱账号已填写", !string.IsNullOrEmpty(config.EmailAccount));
                Add(report, "邮箱授权已填写", !string.IsNullOrEmpty(config.EmailAuth));
                Add(report, "邮箱授权无法解密", config.EmailAuthUndecryptable);
                Add(report, "邮箱需重新授权", config.EmailPasswordRequired);
                report.AppendLine("邮箱检查间隔秒数=" + config.EmailPollSeconds);
                report.AppendLine("邮件主题关键词条数=" + LineCount(config.EmailKeywords));
                report.AppendLine("邮件白名单条数=" + LineCount(config.EmailSenders));
                Add(report, "检查垃圾箱", config.EmailCheckJunk);
                Add(report, "邮件到达提示", config.EmailNotify);
                Add(report, "邮件自动复制", config.EmailAutoCopy);
                Add(report, "保存邮件正文", config.EmailSaveBody);
                Add(report, "QQ 联动开启", config.QqPopupEnabled);
                Add(report, "密码锁开启", config.LockEnabled);
                Add(report, "自动锁定开启", config.AutoLockEnabled);
                report.AppendLine(); report.AppendLine("[运行状态]");
                report.AppendLine("收件箱状态=" + (config.EmailEnabled ? StatusCategory(EmailWatcher.Status) : "已关闭"));
                report.AppendLine("分类目录状态=" + (config.EmailEnabled ? StatusCategory(EmailWatcher.FolderStatus) : "已关闭"));
                Add(report, "已有收件箱检查", EmailWatcher.LastCheckUtc != DateTime.MinValue);
                Add(report, "已有分类目录检查", EmailWatcher.LastFolderCheckUtc != DateTime.MinValue);
                Add(report, "近 30 分钟复查等待中", EmailWatcher.RecentCheckPending);
                Add(report, "配置保存有错误", !string.IsNullOrEmpty(Config.LastSaveError));
                Add(report, "历史保存有错误", !string.IsNullOrEmpty(History.LastSaveError));
                int sms = 0, email = 0;
                foreach (HistoryItem item in History.Snapshot()) { if (item.Channel == "email") email++; else sms++; }
                report.AppendLine("短信记录数=" + sms); report.AppendLine("邮件记录数=" + email);
                report.AppendLine("记录总上限=" + History.Max);
            }
            report.AppendLine(); report.AppendLine("[本次运行已归类事件]");
            lock (Gate)
            {
                if (Events.Count == 0) report.AppendLine("暂无已归类事件；不会读取磁盘上的原始日志。");
                foreach (string entry in Events) report.AppendLine(entry);
            }
            return report.ToString();
        }

        static void Add(StringBuilder report, string label, bool value) { report.AppendLine(label + "=" + (value ? "是" : "否")); }
        static int LineCount(string text) { return (text ?? "").Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length; }

        internal static void SavePreview(string path, string preview, string configPath)
        {
            string name = Path.GetFileName(path);
            if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(configPath), StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "history.txt", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "app.log", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "config.ini", StringComparison.OrdinalIgnoreCase))
                throw new IOException("不能覆盖运行数据");
            if (!string.Equals(Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase))
                throw new IOException("请选择文本报告文件");
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("不能写入文件链接");
            // 替换目录项而非截断目标：即使用户选中了运行文件的硬链接，也不改写原数据。
            Config.WriteAtomically(path, preview);
        }
    }
}
