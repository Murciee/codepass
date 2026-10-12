using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace Codepass
{
    enum DeliveryOutcome { Received, Ignored, Duplicate }

    [DataContract]
    sealed class SmsEnvelope
    {
        [DataMember(Name = "codepass")] public int Format { get; set; }
        [DataMember(Name = "id")] public string Id { get; set; }
        [DataMember(Name = "body_b64")] public string Body { get; set; }
        [DataMember(Name = "sender_b64")] public string Sender { get; set; }
        [DataMember(Name = "time")] public long Time { get; set; }
    }

    [DataContract]
    sealed class NtfyMessage
    {
        [DataMember(Name = "event")] public string Event { get; set; }
        [DataMember(Name = "id")] public string Id { get; set; }
        [DataMember(Name = "time")] public long Time { get; set; }
        [DataMember(Name = "message")] public string Message { get; set; }
        [DataMember(Name = "title")] public string Title { get; set; }
    }

    static class MessageIdentity
    {
        internal const int SmsMaxAgeMinutes = 10;
        static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        internal static long UnixTime(DateTime value) { return (long)(value.ToUniversalTime() - Epoch).TotalSeconds; }
        internal static DateTime FromUnix(long value)
        {
            if (value < 0 || value > 253402300799L) throw new FormatException("消息时间无效");
            return Epoch.AddSeconds(value);
        }
        internal static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? "")));
        }
        internal static T ReadJson<T>(string json)
        {
            if (json == null || json.Length > 1048576) throw new FormatException("消息超出读取上限");
            using (MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(input);
        }
        internal static string SmsKey(string body, string id)
        {
            return string.IsNullOrEmpty(id) ? "sms-text:" + Hash((body ?? "").Replace("\r\n", "\n").Trim())
                : "sms-id:" + Hash(id);
        }
        internal static void Unwrap(ref string body, ref string sender, ref string id, ref DateTime receivedUtc)
        {
            string text = (body ?? "").Trim();
            if (!text.StartsWith("{", StringComparison.Ordinal) || text.IndexOf("\"codepass\"", StringComparison.Ordinal) < 0) return;
            SmsEnvelope envelope = ReadJson<SmsEnvelope>(text);
            if (envelope.Format != 1) return;
            if (string.IsNullOrWhiteSpace(envelope.Id) || envelope.Id.Length > 256 || envelope.Body == null)
                throw new FormatException("短信消息身份无效");
            body = Encoding.UTF8.GetString(Convert.FromBase64String(envelope.Body));
            sender = Encoding.UTF8.GetString(Convert.FromBase64String(envelope.Sender ?? ""));
            id = envelope.Id;
            receivedUtc = FromUnix(envelope.Time);
        }
    }

    // 只保存服务器/主题的散列和成功处理的时间，不保存消息或凭据。
    sealed class NtfyCheckpoint
    {
        readonly string path, identity;
        long acknowledged;
        readonly Dictionary<string, long> events = new Dictionary<string, long>();
        internal NtfyCheckpoint(string path, string server, string topic)
        {
            this.path = path; identity = MessageIdentity.Hash(server.TrimEnd('/') + "\n" + topic);
            try
            {
                FileInfo file = new FileInfo(path);
                if (!file.Exists || file.Length > 524288) return;
                string[] parts = File.ReadAllText(path, Encoding.UTF8).Split('\n');
                long value;
                long now = MessageIdentity.UnixTime(DateTime.UtcNow);
                if (parts.Length >= 2 && parts[0] == identity && long.TryParse(parts[1], out value) && value >= 0 && value <= now)
                {
                    acknowledged = value;
                    for (int i = 2; i < parts.Length && i < 4098; i++)
                    {
                        string[] entry = parts[i].Split('\t');
                        if (entry.Length == 2 && entry[0].Length == 44 && long.TryParse(entry[1], out value)
                            && value >= now - 660 && value <= now) events[entry[0]] = value;
                    }
                }
            }
            catch { }
        }
        internal long Since(DateTime nowUtc)
        {
            long now = MessageIdentity.UnixTime(nowUtc), floor = now - MessageIdentity.SmsMaxAgeMinutes * 60;
            return acknowledged <= 0 ? floor : Math.Max(floor, Math.Min(acknowledged - 1, now));
        }
        internal bool Contains(string id) { return !string.IsNullOrEmpty(id) && events.ContainsKey(MessageIdentity.Hash(id)); }
        internal void Acknowledge(long time, string id)
        {
            long now = MessageIdentity.UnixTime(DateTime.UtcNow);
            if (time <= 0 || time > now + 300) return;
            long value = time > now ? acknowledged : Math.Max(acknowledged, time);
            Dictionary<string, long> next = new Dictionary<string, long>();
            foreach (KeyValuePair<string, long> entry in events) if (entry.Value >= now - 660) next[entry.Key] = entry.Value;
            if (!string.IsNullOrEmpty(id)) next[MessageIdentity.Hash(id)] = Math.Min(time, now);
            if (next.Count > 4096) throw new IOException("ntfy 待确认事件达到上限");
            StringBuilder text = new StringBuilder(identity + "\n" + value);
            foreach (KeyValuePair<string, long> entry in next) text.Append('\n').Append(entry.Key).Append('\t').Append(entry.Value);
            Config.WriteAtomically(path, text.ToString());
            acknowledged = value;
            events.Clear(); foreach (KeyValuePair<string, long> entry in next) events[entry.Key] = entry.Value;
        }
    }
}
