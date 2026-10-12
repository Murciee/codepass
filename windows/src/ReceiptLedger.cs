using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Codepass
{
    // 只管理接收凭据；不持有文件路径、不独立落盘。调用方在 History.Gate 内操作并统一原子提交。
    sealed class ReceiptLedger
    {
        internal sealed class PendingReceipt { internal string Mailbox; internal long Uid; internal string ProtectedLine; }
        internal sealed class RecentReceipt { internal long Expires; internal string ProtectedLine; }
        readonly Dictionary<string, PendingReceipt> PendingReceipts = new Dictionary<string, PendingReceipt>();
        readonly Dictionary<string, RecentReceipt> RecentMessages = new Dictionary<string, RecentReceipt>();
        const int MaxReceipts = 4096;

        internal sealed class Snapshot
        {
            internal readonly Dictionary<string, PendingReceipt> Pending;
            internal readonly Dictionary<string, RecentReceipt> Recent;
            internal Snapshot(Dictionary<string, PendingReceipt> pending, Dictionary<string, RecentReceipt> recent)
            { Pending = new Dictionary<string, PendingReceipt>(pending); Recent = new Dictionary<string, RecentReceipt>(recent); }
        }
        internal Snapshot Capture() { return new Snapshot(PendingReceipts, RecentMessages); }
        internal void Restore(Snapshot snapshot)
        {
            Reset();
            foreach (KeyValuePair<string, PendingReceipt> entry in snapshot.Pending) PendingReceipts.Add(entry.Key, entry.Value);
            foreach (KeyValuePair<string, RecentReceipt> entry in snapshot.Recent) RecentMessages.Add(entry.Key, entry.Value);
        }
        internal void Reset() { PendingReceipts.Clear(); RecentMessages.Clear(); }

        internal bool LoadLine(string storedLine)
        {
            bool pending = storedLine.StartsWith("r3\t", StringComparison.Ordinal);
            bool recent = storedLine.StartsWith("d3\t", StringComparison.Ordinal);
            if (!pending && !recent) return false;
            string protectedLine = storedLine.Substring(3);
            string[] parts = Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(protectedLine), null, DataProtectionScope.CurrentUser)).Split('\t');
            long value;
            if (pending)
            {
                if (parts.Length != 4 || parts[0] != "r1" || !long.TryParse(parts[3], out value) || value <= 0) throw new IOException("未提交邮件凭据损坏");
                PendingReceipts[Decode(parts[1])] = new PendingReceipt { Mailbox = Decode(parts[2]), Uid = value, ProtectedLine = protectedLine };
            }
            else
            {
                if (parts.Length != 3 || parts[0] != "d1" || !long.TryParse(parts[2], out value)) throw new IOException("近期消息去重凭据损坏");
                if (value > DateTime.UtcNow.Ticks && value <= DateTime.UtcNow.AddMinutes(36).Ticks)
                    RecentMessages[Decode(parts[1])] = new RecentReceipt { Expires = value, ProtectedLine = protectedLine };
            }
            return true;
        }

        internal void SeedLegacy(IEnumerable<HistoryItem> records)
        {
            foreach (HistoryItem item in records)
                if (item.Channel == "email" && !string.IsNullOrEmpty(item.MessageKey) && !RecentMessages.ContainsKey(item.MessageKey))
                    RecentMessages[item.MessageKey] = new RecentReceipt { Expires = DateTime.UtcNow.AddMinutes(35).Ticks };
        }
        internal void PruneExpired(DateTime nowUtc)
        {
            List<string> expired = new List<string>();
            foreach (KeyValuePair<string, RecentReceipt> item in RecentMessages) if (item.Value.Expires <= nowUtc.Ticks) expired.Add(item.Key);
            foreach (string key in expired) RecentMessages.Remove(key);
        }
        internal bool IsDuplicate(HistoryItem item, IEnumerable<HistoryItem> records)
        {
            if (!string.IsNullOrEmpty(item.TransportKey) && RecentMessages.ContainsKey(item.TransportKey)) return true;
            if (string.IsNullOrEmpty(item.MessageKey)) return false;
            if (RecentMessages.ContainsKey(item.MessageKey) || PendingReceipts.ContainsKey(item.MessageKey)) return true;
            foreach (HistoryItem existing in records)
                if (existing.MessageKey == item.MessageKey && (!item.MessageKey.StartsWith("sms-text:", StringComparison.Ordinal)
                    || existing.ReceivedUtcTicks >= DateTime.UtcNow.AddMinutes(-2).Ticks)) return true;
            return false;
        }
        internal void Track(HistoryItem item)
        {
            bool message = !string.IsNullOrEmpty(item.MessageKey);
            bool pending = message && !string.IsNullOrEmpty(item.MailboxKey) && item.MailUid > 0;
            if (pending && PendingReceipts.Count >= MaxReceipts) throw new IOException("未提交邮件凭据达到上限，请修复配置保存问题后重试");
            int required = message ? 1 : 0;
            bool transport = !string.IsNullOrEmpty(item.TransportKey) && item.TransportKey != item.MessageKey;
            if (transport) required++;
            if (RecentMessages.Count + required > MaxReceipts) throw new IOException("近期消息去重凭据达到上限，请稍后重试");
            if (pending) PendingReceipts.Add(item.MessageKey, new PendingReceipt { Mailbox = item.MailboxKey, Uid = item.MailUid });
            if (message) RecentMessages.Add(item.MessageKey, new RecentReceipt { Expires = DateTime.UtcNow.AddMinutes(
                item.MessageKey.StartsWith("sms-text:", StringComparison.Ordinal) ? 2 : 35).Ticks });
            if (transport) RecentMessages.Add(item.TransportKey, new RecentReceipt { Expires = DateTime.UtcNow.AddMinutes(35).Ticks });
        }
        internal bool TrackDuplicateTransport(HistoryItem item)
        {
            if (string.IsNullOrEmpty(item.TransportKey) || RecentMessages.ContainsKey(item.TransportKey)) return false;
            if (RecentMessages.Count >= MaxReceipts) throw new IOException("消息去重凭据达到上限");
            RecentMessages.Add(item.TransportKey, new RecentReceipt { Expires = DateTime.UtcNow.AddMinutes(35).Ticks });
            return true;
        }
        internal bool AcknowledgeMailbox(string mailbox, long water)
        {
            List<string> acknowledged = new List<string>();
            foreach (KeyValuePair<string, PendingReceipt> item in PendingReceipts)
                if (item.Value.Mailbox == mailbox && item.Value.Uid <= water) acknowledged.Add(item.Key);
            foreach (string key in acknowledged) PendingReceipts.Remove(key);
            return acknowledged.Count > 0;
        }
        internal void AppendProtectedLines(StringBuilder text)
        {
            foreach (KeyValuePair<string, PendingReceipt> receipt in PendingReceipts)
            {
                if (receipt.Value.ProtectedLine == null)
                    receipt.Value.ProtectedLine = Protect("r1\t" + Encode(receipt.Key) + "\t" + Encode(receipt.Value.Mailbox) + "\t" + receipt.Value.Uid.ToString(CultureInfo.InvariantCulture));
                text.Append("r3\t").Append(receipt.Value.ProtectedLine).AppendLine();
            }
            long now = DateTime.UtcNow.Ticks;
            foreach (KeyValuePair<string, RecentReceipt> receipt in RecentMessages)
            {
                if (receipt.Value.Expires <= now) continue;
                if (receipt.Value.ProtectedLine == null)
                    receipt.Value.ProtectedLine = Protect("d1\t" + Encode(receipt.Key) + "\t" + receipt.Value.Expires.ToString(CultureInfo.InvariantCulture));
                text.Append("d3\t").Append(receipt.Value.ProtectedLine).AppendLine();
            }
        }
        static string Protect(string text)
        { return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(text), null, DataProtectionScope.CurrentUser)); }
        static string Encode(string text) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(text ?? "")); }
        static string Decode(string text) { return Encoding.UTF8.GetString(Convert.FromBase64String(text)); }
    }
}
