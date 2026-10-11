using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Globalization;

namespace Codepass
{
    sealed class HistoryItem
    {
        public string Time;
        public long ReceivedUtcTicks;
        public string Source;
        public string Code;
        public string Raw;
        // sms / email；旧版历史没有该字段，读取时按 sms 兼容。
        public string Channel;
        public string MessageKey;
        public string MailboxKey;
        public long MailUid;
        public bool CanBeLatest = true;
        internal string CachedPlainLine, CachedProtectedLine;
    }

    static class History
    {
        public const int Max = 100;
        static string _path;
        static readonly List<HistoryItem> Items = new List<HistoryItem>();
        static readonly object Gate = new object();
        static bool unreadableProtectedData;
        sealed class Receipt { public string Mailbox; public long Uid; public string ProtectedLine; }
        static readonly Dictionary<string, Receipt> PendingReceipts = new Dictionary<string, Receipt>();
        const int MaxPendingReceipts = 4096;
        sealed class RecentReceipt { public long Expires; public string ProtectedLine; }
        static readonly Dictionary<string, RecentReceipt> RecentMessages = new Dictionary<string, RecentReceipt>();
        public static string LastSaveError { get; private set; }

        public static void Init(string path)
        {
            _path = path;
            lock (Gate)
            {
                Items.Clear();
                PendingReceipts.Clear();
                RecentMessages.Clear();
                unreadableProtectedData = false;
                LastSaveError = null;
                try
                {
                    if (!File.Exists(path)) return;
                    string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                    foreach (string storedLine in lines)
                    {
                        string ln = storedLine;
                        string protectedLine = null;
                        if (ln.Length == 0) continue;
                        bool receipt = ln.StartsWith("r3\t", StringComparison.Ordinal);
                        bool recent = ln.StartsWith("d3\t", StringComparison.Ordinal);
                        if (receipt || recent || ln.StartsWith("v3\t", StringComparison.Ordinal))
                        {
                            try { protectedLine = ln.Substring(3); ln = Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(protectedLine), null, DataProtectionScope.CurrentUser)); }
                            catch { unreadableProtectedData = true; LastSaveError = "历史记录无法在当前 Windows 用户下解密，原文件已保留"; continue; }
                        }
                        string[] p = ln.Split('\t');
                        if (recent)
                        {
                            long expires;
                            if (p.Length == 3 && p[0] == "d1" && long.TryParse(p[2], out expires))
                            {
                                if (expires > DateTime.UtcNow.Ticks && expires <= DateTime.UtcNow.AddMinutes(36).Ticks)
                                    RecentMessages[Decode(p[1])] = new RecentReceipt { Expires = expires, ProtectedLine = protectedLine };
                            }
                            else { unreadableProtectedData = true; LastSaveError = "近期邮件去重凭据损坏，原历史文件已保留"; }
                            continue;
                        }
                        if (receipt)
                        {
                            long uid;
                            if (p.Length == 4 && p[0] == "r1" && long.TryParse(p[3], out uid) && uid > 0)
                                PendingReceipts[Decode(p[1])] = new Receipt { Mailbox = Decode(p[2]), Uid = uid, ProtectedLine = protectedLine };
                            else { unreadableProtectedData = true; LastSaveError = "未提交邮件凭据损坏，原历史文件已保留"; }
                            continue;
                        }
                        HistoryItem it = new HistoryItem();
                        if (protectedLine != null) { it.CachedPlainLine = ln; it.CachedProtectedLine = protectedLine; }
                        if (p.Length > 0 && p[0] == "v2" && p.Length >= 5)
                        {
                            it.Time = Decode(p[1]);
                            it.Source = Decode(p[2]);
                            it.Code = Decode(p[3]);
                            it.Raw = Decode(p[4]);
                            it.Channel = p.Length > 5 ? Decode(p[5]) : "sms";
                            it.MessageKey = p.Length > 6 ? Decode(p[6]) : "";
                            it.CanBeLatest = p.Length <= 7 || p[7] == "1";
                            long ticks;
                            if (p.Length > 8 && long.TryParse(p[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks)
                                && ticks > 0 && ticks <= DateTime.MaxValue.Ticks) it.ReceivedUtcTicks = ticks;
                        }
                        else
                        {
                            // 兼容旧版的普通 TSV 历史文件。
                            it.Time = p.Length > 0 ? p[0] : "";
                            it.Source = p.Length > 1 ? p[1] : "";
                            it.Code = p.Length > 2 ? p[2] : "";
                            it.Raw = p.Length > 3 ? p[3] : "";
                            it.Channel = "sms";
                        }
                        if (it.Channel != "email") it.Channel = "sms";
                        if (it.ReceivedUtcTicks == 0) it.ReceivedUtcTicks = LegacyReceivedUtc(it.Time, DateTime.UtcNow).Ticks;
                        if (IsTransportSource(it.Source)) it.Source = ExtractSender(it.Raw);
                        if (Items.Count < Max) Items.Add(it);
                    }
                    // 升级前的可见邮件也加入近期去重，避免首次复查过程中被淘汰后重投递。
                    foreach (HistoryItem item in Items)
                        if (item.Channel == "email" && !string.IsNullOrEmpty(item.MessageKey) && !RecentMessages.ContainsKey(item.MessageKey))
                            RecentMessages[item.MessageKey] = new RecentReceipt { Expires = DateTime.UtcNow.AddMinutes(35).Ticks };
                }
                catch { LastSaveError = "历史读取失败，原文件已保留"; unreadableProtectedData = true; }
            }
        }

        public static List<HistoryItem> Snapshot()
        {
            lock (Gate) return new List<HistoryItem>(Items);
        }

        internal static void RetrySave()
        {
            lock (Gate) Save();
        }

        internal static DateTime LegacyReceivedUtc(string time, DateTime nowUtc)
        {
            DateTime local;
            if (!DateTime.TryParseExact("2000-" + time, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out local)) return nowUtc;
            DateTime nowLocal = nowUtc.ToLocalTime();
            for (int year = nowLocal.Year; year >= Math.Max(1, nowLocal.Year - 8); year--)
            {
                if (local.Day > DateTime.DaysInMonth(year, local.Month)) continue;
                DateTime candidate = new DateTime(year, local.Month, local.Day, local.Hour, local.Minute, local.Second, DateTimeKind.Local);
                if (candidate <= nowLocal.AddMinutes(5)) return candidate.ToUniversalTime();
            }
            return nowUtc;
        }

        internal static bool IsExpired(HistoryItem item, int minutes, DateTime nowUtc)
        {
            return minutes > 0 && item.ReceivedUtcTicks > 0
                && item.ReceivedUtcTicks < nowUtc.AddMinutes(-minutes).Ticks;
        }

        internal static int PruneExpired(int minutes, DateTime nowUtc)
        {
            if (minutes <= 0 || minutes > Config.MaximumHistoryRetentionMinutes) return 0;
            lock (Config.SaveGate)
            lock (Gate)
            {
                List<HistoryItem> previous = new List<HistoryItem>(Items);
                int removed = Items.RemoveAll(delegate(HistoryItem item) { return IsExpired(item, minutes, nowUtc); });
                if (removed == 0) { Program.ExpireLatestReceipt(minutes, nowUtc); return 0; }
                try { Save(); }
                catch { Items.Clear(); Items.AddRange(previous); throw; }
                // 清理后不再允许从托盘复制已过期的最新验证码；保留邮件去重/水位凭据。
                Program.ExpireLatestReceipt(minutes, nowUtc);
                return removed;
            }
        }

        internal static string LatestCode()
        {
            HistoryItem item = LatestRecord();
            return item == null ? null : item.Code;
        }

        internal static HistoryItem LatestRecord()
        {
            lock (Gate)
            {
                foreach (HistoryItem item in Items)
                    if (item.CanBeLatest && !string.IsNullOrEmpty(item.Code)) return item;
                return null;
            }
        }

        internal static HistoryItem LatestUnexpiredRecord(int minutes, DateTime nowUtc)
        {
            lock (Gate)
            {
                foreach (HistoryItem item in Items)
                    if (item.CanBeLatest && !string.IsNullOrEmpty(item.Code) && !IsExpired(item, minutes, nowUtc)) return item;
                return null;
            }
        }

        public static void Add(HistoryItem it)
        {
            AddUnique(it);
        }

        public static bool AddUnique(HistoryItem it)
        {
            lock (Gate)
            {
                if (it.ReceivedUtcTicks <= 0) it.ReceivedUtcTicks = DateTime.UtcNow.Ticks;
                PruneRecentMessages();
                if (!string.IsNullOrEmpty(it.MessageKey))
                {
                    if (RecentMessages.ContainsKey(it.MessageKey)) return false;
                    if (PendingReceipts.ContainsKey(it.MessageKey)) return false;
                    foreach (HistoryItem existing in Items)
                        if (existing.MessageKey == it.MessageKey) return false;
                }
                bool pending = !string.IsNullOrEmpty(it.MessageKey) && !string.IsNullOrEmpty(it.MailboxKey) && it.MailUid > 0;
                if (pending && PendingReceipts.Count >= MaxPendingReceipts)
                { LastSaveError = "未提交邮件凭据达到上限，请修复配置保存问题后重试"; Program.EmailStatusChanged(); throw new IOException(LastSaveError); }
                bool recent = it.Channel == "email" && !string.IsNullOrEmpty(it.MessageKey);
                if (recent && RecentMessages.Count >= MaxPendingReceipts)
                { LastSaveError = "近期邮件去重凭据达到上限，请稍后重试"; Program.EmailStatusChanged(); throw new IOException(LastSaveError); }
                List<HistoryItem> previous = new List<HistoryItem>(Items);
                Items.Insert(0, it);
                if (pending) PendingReceipts.Add(it.MessageKey, new Receipt { Mailbox = it.MailboxKey, Uid = it.MailUid });
                if (recent) RecentMessages.Add(it.MessageKey, new RecentReceipt { Expires = DateTime.UtcNow.AddMinutes(35).Ticks });
                while (Items.Count > Max) Items.RemoveAt(Items.Count - 1);
                try { Save(); }
                catch { Items.Clear(); Items.AddRange(previous); if (pending) PendingReceipts.Remove(it.MessageKey); if (recent) RecentMessages.Remove(it.MessageKey); throw; }
                return true;
            }
        }

        internal static void AcknowledgeMailbox(string mailbox, long water)
        {
            lock (Gate)
            {
                Dictionary<string, Receipt> acknowledged = new Dictionary<string, Receipt>();
                foreach (KeyValuePair<string, Receipt> item in PendingReceipts)
                    if (item.Value.Mailbox == mailbox && item.Value.Uid <= water) acknowledged.Add(item.Key, item.Value);
                if (acknowledged.Count == 0) return;
                foreach (string key in acknowledged.Keys) PendingReceipts.Remove(key);
                try { Save(); }
                catch { foreach (KeyValuePair<string, Receipt> item in acknowledged) PendingReceipts[item.Key] = item.Value; }
            }
        }

        static void PruneRecentMessages()
        {
            List<string> expired = new List<string>();
            foreach (KeyValuePair<string, RecentReceipt> item in RecentMessages)
                if (item.Value.Expires <= DateTime.UtcNow.Ticks) expired.Add(item.Key);
            foreach (string key in expired) RecentMessages.Remove(key);
        }

        public static string ExtractSender(string text)
        {
            return ExtractSender(text, "");
        }

        public static string ExtractSender(string text, string fallback)
        {
            if (!String.IsNullOrEmpty(text))
            {
                int start = text.IndexOf('【');
                while (start >= 0)
                {
                    int end = text.IndexOf('】', start + 1);
                    if (end > start + 1)
                    {
                        string sender = text.Substring(start + 1, end - start - 1).Trim();
                        if (sender.Length > 0)
                            return sender.Length > 64 ? sender.Substring(0, 64) : sender;
                    }
                    start = text.IndexOf('【', start + 1);
                }
            }
            if (!String.IsNullOrEmpty(fallback))
            {
                string sender = fallback.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ").Trim();
                if (sender.Length > 0)
                    return sender.Length > 64 ? sender.Substring(0, 64) : sender;
            }
            return "未知发件人";
        }

        static bool IsTransportSource(string source)
        {
            return source == "http" || source == "ntfy" || source == "test" || String.IsNullOrEmpty(source);
        }

        public static void Clear()
        {
            lock (Config.SaveGate)
            lock (Gate)
            {
                List<HistoryItem> previous = new List<HistoryItem>(Items);
                Items.Clear();
                try { Save(); } catch { Items.AddRange(previous); throw; }
                Program.SetLatestCode(null, false);
            }
        }

        static void Save()
        {
            try
            {
                if (unreadableProtectedData) throw new IOException("历史记录无法解密，禁止覆盖原文件");
                StringBuilder sb = new StringBuilder();
                foreach (HistoryItem it in Items)
                {
                    string line = "v2\t" + Encode(it.Time) + "\t" + Encode(it.Source) + "\t" + Encode(it.Code)
                        + "\t" + Encode(it.Raw) + "\t" + Encode(it.Channel == "email" ? "email" : "sms") + "\t" + Encode(it.MessageKey)
                        + "\t" + (it.CanBeLatest ? "1" : "0") + "\t" + it.ReceivedUtcTicks.ToString(CultureInfo.InvariantCulture);
                    if (it.CachedPlainLine != line || it.CachedProtectedLine == null)
                    {
                        it.CachedProtectedLine = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(line), null, DataProtectionScope.CurrentUser));
                        it.CachedPlainLine = line;
                    }
                    sb.Append("v3\t").Append(it.CachedProtectedLine).AppendLine();
                }
                foreach (KeyValuePair<string, Receipt> receipt in PendingReceipts)
                {
                    string line = "r1\t" + Encode(receipt.Key) + "\t" + Encode(receipt.Value.Mailbox) + "\t" + receipt.Value.Uid;
                    if (receipt.Value.ProtectedLine == null)
                        receipt.Value.ProtectedLine = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(line), null, DataProtectionScope.CurrentUser));
                    sb.Append("r3\t").Append(receipt.Value.ProtectedLine).AppendLine();
                }
                PruneRecentMessages();
                foreach (KeyValuePair<string, RecentReceipt> receipt in RecentMessages)
                {
                    if (receipt.Value.ProtectedLine == null)
                    {
                        string line = "d1\t" + Encode(receipt.Key) + "\t" + receipt.Value.Expires;
                        receipt.Value.ProtectedLine = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(line), null, DataProtectionScope.CurrentUser));
                    }
                    sb.Append("d3\t").Append(receipt.Value.ProtectedLine).AppendLine();
                }
                Config.WriteAtomically(_path, sb.ToString());
                LastSaveError = null;
            }
            catch
            {
                LastSaveError = unreadableProtectedData ? "历史无法在当前 Windows 用户解密，原文件已保留；请先恢复原用户或移走原文件"
                    : "历史保存失败，未推进邮件水位，请检查文件访问权限";
                Log.Write("history save failed; original retained");
                Program.EmailStatusChanged();
                throw new IOException(LastSaveError);
            }
        }

        static string Encode(string s)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(s ?? ""));
        }

        static string Decode(string s)
        {
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(s ?? "")); }
            catch { return ""; }
        }
    }
}
