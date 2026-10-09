using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Codepass
{
    sealed class HistoryItem
    {
        public string Time;
        public string Source;
        public string Code;
        public string Raw;
    }

    static class History
    {
        public const int Max = 100;
        static string _path;
        static readonly List<HistoryItem> Items = new List<HistoryItem>();
        static readonly object Gate = new object();

        public static void Init(string path)
        {
            _path = path;
            lock (Gate)
            {
                Items.Clear();
                try
                {
                    if (!File.Exists(path)) return;
                    string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                    foreach (string ln in lines)
                    {
                        if (ln.Length == 0) continue;
                        string[] p = ln.Split('\t');
                        HistoryItem it = new HistoryItem();
                        if (p.Length > 0 && p[0] == "v2" && p.Length >= 5)
                        {
                            it.Time = Decode(p[1]);
                            it.Source = Decode(p[2]);
                            it.Code = Decode(p[3]);
                            it.Raw = Decode(p[4]);
                        }
                        else
                        {
                            // 兼容旧版的普通 TSV 历史文件。
                            it.Time = p.Length > 0 ? p[0] : "";
                            it.Source = p.Length > 1 ? p[1] : "";
                            it.Code = p.Length > 2 ? p[2] : "";
                            it.Raw = p.Length > 3 ? p[3] : "";
                        }
                        if (IsTransportSource(it.Source)) it.Source = ExtractSender(it.Raw);
                        Items.Add(it);
                        if (Items.Count >= Max) break;
                    }
                }
                catch { }
            }
        }

        public static List<HistoryItem> Snapshot()
        {
            lock (Gate) return new List<HistoryItem>(Items);
        }

        public static void Add(HistoryItem it)
        {
            lock (Gate)
            {
                Items.Insert(0, it);
                while (Items.Count > Max) Items.RemoveAt(Items.Count - 1);
                Save();
            }
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
            lock (Gate) { Items.Clear(); Save(); }
        }

        static void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                foreach (HistoryItem it in Items)
                    sb.Append("v2\t").Append(Encode(it.Time)).Append('\t')
                      .Append(Encode(it.Source)).Append('\t')
                      .Append(Encode(it.Code)).Append('\t')
                      .Append(Encode(it.Raw)).Append('\r').Append('\n');
                File.WriteAllText(_path, sb.ToString(), new UTF8Encoding(true));
            }
            catch { }
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
