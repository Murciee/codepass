// 极简 IMAP 客户端与 MIME 解析（仅本机使用，不引第三方库）
// - Imap：TLS 连接、命令/响应、literal 读取、UID SEARCH/FETCH、IDLE
// - Mime：MIME 编码词、quoted-printable、base64、HTML 转文本、修正版 UTF-7 文件名
// 说明：这是为“只读检查收件箱”裁剪的最小实现，不做 IMAP 全集。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Codepass
{
    sealed class ImapException : Exception
    {
        public bool FetchBad;
        public ImapException(string message) : base(message) { }
    }

    sealed class MailContentException : Exception
    {
        public readonly string ReasonCode;
        public MailContentException(string reason, string message) : base(message) { ReasonCode = reason; }
    }

    sealed class Mailbox
    {
        public string Name = "";
        public int Exists;
        public long UidNext;
        public long UidValidity;
    }

    enum IdleWait
    {
        Data,
        Timeout
    }

    sealed class ImapFolder
    {
        public string Name;
        public string Delimiter;
        public bool Junk, Selectable;
        public bool ExcludedFromReceive;
    }

    sealed class MailHeader
    {
        public byte[] Data;
        public DateTimeOffset? Received;
        public bool Found;
    }

    sealed class TextSection
    {
        public string Section, Charset, Transfer;
        public bool Html;
    }

    sealed class Imap
    {
        TcpClient tcp;
        volatile Stream stream;
        readonly byte[] buf = new byte[16384];
        int bufPos, bufLen;
        int tagSeq;
        const int DefaultReadTimeoutMs = 30000;
        const int IdleCompletionTimeoutMs = 5000;
        readonly List<string> capabilities = new List<string>();
        bool idleActive;
        string idleTag;
        IAsyncResult pendingRead;
        Stream pendingReadStream;

        // ---------- 连接 ----------

        public void Connect(string host, int port, int timeoutMs)
        {
            tcp = new TcpClient();
            IAsyncResult ar = tcp.BeginConnect(host, port, null, null);
            if (!ar.AsyncWaitHandle.WaitOne(timeoutMs))
            {
                try { tcp.Close(); } catch { }
                throw new IOException("连接 " + host + " 超时");
            }
            tcp.EndConnect(ar);
            SslStream ssl = new SslStream(tcp.GetStream(), false);
            ssl.ReadTimeout = timeoutMs;
            ssl.WriteTimeout = timeoutMs;
            try
            {
                ssl.AuthenticateAsClient(host, null, SslProtocols.Tls12, false);
            }
            catch
            {
                try { ssl.Dispose(); } catch { }
                try { tcp.Close(); } catch { }
                throw;
            }
            ssl.ReadTimeout = DefaultReadTimeoutMs;
            stream = ssl;
            string greeting = ReadLine();
            if (greeting == null || !greeting.StartsWith("* OK", StringComparison.Ordinal))
                throw new ImapException("服务器响应异常：" + Clip(greeting));
        }

        /// <summary>测试入口：直接挂到任意流上（无 TLS）。</summary>
        public void Attach(Stream s)
        {
            stream = s;
        }

        public void PrepareAndLogin(string user, string auth)
        {
            try { ParseCapabilities(Cmd("CAPABILITY")); } catch (ImapException) { }
            if (HasCapability("ID"))
            {
                // 网易邮箱（163/126）要求第三方客户端发送 ID 才放行 IMAP 登录，
                // 否则返回 "Unsafe Login"；这里使用网易认可的兼容客户端标识。
                try { Cmd("ID (\"name\" \"Foxmail\" \"version\" \"7.2.25\")"); } catch (ImapException) { }
            }
            Cmd("LOGIN " + Quote(user) + " " + Quote(auth));
            try { ParseCapabilities(Cmd("CAPABILITY")); } catch (ImapException) { }
        }

        public bool HasCapability(string name)
        {
            foreach (string c in capabilities)
                if (string.Equals(c, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        void ParseCapabilities(string response)
        {
            capabilities.Clear();
            foreach (string line in response.Split('\n'))
            {
                string t = line.Trim();
                if (!t.StartsWith("* CAPABILITY", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (string c in t.Substring(12).Split(' '))
                    if (c.Trim().Length > 0) capabilities.Add(c.Trim());
            }
            // IDLE 支持也常出现在 OK [CAPABILITY ...] 里，一并扫描。
            foreach (Match m in Regex.Matches(response, @"\[CAPABILITY ([^\]]+)\]"))
                foreach (string c in m.Groups[1].Value.Split(' '))
                    if (c.Trim().Length > 0 && !capabilities.Contains(c.Trim())) capabilities.Add(c.Trim());
        }

        // ---------- 常用命令 ----------

        public Mailbox Examine(string mailbox)
        {
            string tag = NextTag();
            Send(tag + " EXAMINE " + Quote(mailbox) + "\r\n");
            Mailbox mb = new Mailbox { Name = mailbox };
            ReadUntilTag(tag, delegate(Line ln)
            {
                string t = ln.Text;
                if (t.IndexOf("UIDNEXT", StringComparison.OrdinalIgnoreCase) >= 0)
                    mb.UidNext = NumberAfter(t, "UIDNEXT");
                if (t.IndexOf("UIDVALIDITY", StringComparison.OrdinalIgnoreCase) >= 0)
                    mb.UidValidity = NumberAfter(t, "UIDVALIDITY");
                if (t.EndsWith(" EXISTS", StringComparison.OrdinalIgnoreCase))
                    mb.Exists = (int)NumberBefore(t, "EXISTS");
            });
            return mb;
        }

        public List<string> ListFolders()
        {
            List<string> names = new List<string>();
            foreach (ImapFolder folder in ListFolderDetails()) names.Add(folder.Name);
            return names;
        }

        public List<ImapFolder> ListFolderDetails()
        {
            List<ImapFolder> folders = new List<ImapFolder>();
            string tag = NextTag();
            Send(tag + " LIST \"\" \"*\"\r\n");
            ReadUntilTag(tag, delegate(Line ln)
            {
                Match m = Regex.Match(ln.Text, "^\\* LIST \\((?<attrs>[^)]*)\\) (?<delim>NIL|\"(?:[^\"\\\\]|\\\\.)*\") (?<name>.+)$", RegexOptions.IgnoreCase);
                if (!m.Success) return;
                string name = m.Groups["name"].Value.Trim();
                if (ln.Literal != null) name = Encoding.UTF8.GetString(ln.Literal);
                else name = UnquoteMailbox(name);
                string attrs = m.Groups["attrs"].Value;
                string delimiter = m.Groups["delim"].Value;
                folders.Add(new ImapFolder { Name = name, Delimiter = delimiter.Equals("NIL", StringComparison.OrdinalIgnoreCase) ? null : UnquoteMailbox(delimiter),
                    Selectable = attrs.IndexOf("\\Noselect", StringComparison.OrdinalIgnoreCase) < 0,
                    Junk = attrs.IndexOf("\\Junk", StringComparison.OrdinalIgnoreCase) >= 0,
                    ExcludedFromReceive = Regex.IsMatch(attrs, @"(?:^|\s)\\(?:Sent|Drafts|Trash|All)(?:\s|$)", RegexOptions.IgnoreCase) });
            });
            return folders;
        }

        public List<long> SearchUidAbove(long water, int cap)
        {
            string resp = Cmd("UID SEARCH UID " + (water + 1).ToString(CultureInfo.InvariantCulture) + ":*");
            SortedSet<long> result = new SortedSet<long>();
            foreach (string line in resp.Split('\n'))
            {
                string t = line.Trim();
                if (!t.StartsWith("* SEARCH", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (string p in t.Substring(8).Split(' '))
                {
                    long uid;
                    if (long.TryParse(p.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out uid) && uid > water)
                        result.Add(uid);
                }
            }
            List<long> list = new List<long>(result);
            if (list.Count > cap) list.RemoveRange(cap, list.Count - cap);
            return list;
        }

        public byte[] FetchHeader(long uid)
        {
            return FetchLiteral(uid, "BODY.PEEK[HEADER.FIELDS (SUBJECT FROM DATE)]");
        }

        // SINCE 按日期粗筛；调用方再用 INTERNALDATE 严格筛选近 30 分钟。
        public List<long> SearchRecentUids(DateTimeOffset since, int cap)
        {
            string response = Cmd("UID SEARCH SINCE " + since.AddDays(-1).UtcDateTime.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture));
            SortedSet<long> matches = new SortedSet<long>();
            foreach (string line in response.Split('\n'))
            {
                string value = line.Trim();
                if (!value.StartsWith("* SEARCH", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (string number in value.Substring(8).Split(' '))
                {
                    long uid;
                    if (long.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out uid) && uid > 0) matches.Add(uid);
                }
            }
            List<long> uids = new List<long>(matches);
            LastRecentCandidateCount = uids.Count;
            if (uids.Count > cap) uids.RemoveRange(0, uids.Count - cap);
            return uids;
        }

        public int LastRecentCandidateCount { get; private set; }

        public MailHeader FetchHeaderDetails(long uid)
        {
            string tag = NextTag();
            Send(tag + " UID FETCH " + uid.ToString(CultureInfo.InvariantCulture)
                + " (UID INTERNALDATE BODY.PEEK[HEADER.FIELDS (SUBJECT FROM DATE MESSAGE-ID)])\r\n");
            MailHeader header = new MailHeader();
            StringBuilder fetch = new StringBuilder();
            byte[] candidate = null;
            bool inFetch = false;
            Action finish = delegate
            {
                long responseUid = NumberAfter(fetch.ToString(), "UID");
                if (responseUid != uid) return;
                header.Found = true;
                if (candidate != null) header.Data = candidate;
                Match date = Regex.Match(fetch.ToString(), "INTERNALDATE\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
                DateTimeOffset parsed;
                if (date.Success && TryInternalDate(date.Groups[1].Value, out parsed)) header.Received = parsed;
            };
            ReadUntilTag(tag, delegate(Line line)
            {
                if (Regex.IsMatch(line.Text, @"^\* [0-9]+ FETCH ", RegexOptions.IgnoreCase))
                {
                    if (inFetch) finish();
                    fetch.Length = 0; candidate = null; inFetch = true;
                }
                else if (line.Text.StartsWith("* ", StringComparison.Ordinal) || line.Text.StartsWith(tag + " ", StringComparison.Ordinal))
                {
                    if (inFetch) finish();
                    inFetch = false;
                }
                if (!inFetch) return;
                fetch.Append(line.Text).Append(' ');
                if (line.Literal != null && line.Text.IndexOf("HEADER.FIELDS", StringComparison.OrdinalIgnoreCase) >= 0) candidate = line.Literal;
            });
            if (inFetch) finish();
            return header;
        }

        internal static bool TryInternalDate(string value, out DateTimeOffset date)
        {
            string normalized = Regex.Replace(value.Trim(), "([+-][0-9]{2})([0-9]{2})$", "$1:$2");
            return DateTimeOffset.TryParseExact(normalized, new string[] { "d-MMM-yyyy HH:mm:ss zzz", "dd-MMM-yyyy HH:mm:ss zzz" },
                CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date);
        }

        public string FetchText(long uid)
        {
            return FetchTextCore(uid, null);
        }

        public bool LastTextTruncated { get; private set; }

        public string FetchTextForCode(long uid, int min, int max)
        {
            return FetchTextCore(uid, delegate(string text) { return CodeExtractor.ExtractExplicit(text, min, max) != null; });
        }

        string FetchTextCore(long uid, Func<string, bool> containsCode)
        {
            LastTextTruncated = false;
            string response;
            try { response = Cmd("UID FETCH " + uid.ToString(CultureInfo.InvariantCulture) + " (BODYSTRUCTURE)"); }
            catch (ImapException ex)
            {
                if (ex.FetchBad) throw new MailContentException("fetch_bad", "邮件正文读取被服务器拒绝");
                throw;
            }
            List<TextSection> sections = ParseTextSections(response);
            string firstText = "";
            bool firstTruncated = false;
            int count = 0;
            foreach (TextSection section in sections)
            {
                if (++count > 8) break;
                byte[] data;
                try { data = FetchLiteral(uid, "BODY.PEEK[" + section.Section + "]<0.512000>"); }
                catch (ImapException ex)
                {
                    if (ex.FetchBad) throw new MailContentException("fetch_bad", "邮件正文读取被服务器拒绝");
                    throw;
                }
                if (data == null) continue;
                string text = Mime.DecodeTextSection(data, section.Transfer, section.Charset, section.Html, data.Length >= 512000);
                bool truncated = data.Length >= 512000 || text.Length > 20000;
                text = LimitTextForExtraction(text, 20000);
                if (string.IsNullOrWhiteSpace(text)) continue;
                if (firstText.Length == 0) { firstText = text; firstTruncated = truncated; }
                if (containsCode == null || containsCode(text)) { LastTextTruncated = truncated; return text; }
            }
            LastTextTruncated = firstTruncated;
            return firstText;
        }

        internal static string LimitTextForExtraction(string text, int limit)
        {
            if (text.Length <= limit) return text;
            int lastLine = text.LastIndexOf('\n', limit - 1, limit);
            return lastLine >= 0 ? text.Substring(0, lastLine) : "";
        }

        internal static List<TextSection> ParseTextSections(string response)
        {
            int at = response.IndexOf("BODYSTRUCTURE", StringComparison.OrdinalIgnoreCase);
            List<TextSection> sections = new List<TextSection>();
            if (at < 0) throw new MailContentException("structure_missing", "邮件正文结构缺失，请稍后重试");
            int position = response.IndexOf('(', at + 13);
            int nodes = 0;
            object tree = ParseStructure(response, ref position, 0, ref nodes);
            SelectTextSections(tree as List<object>, "", sections);
            sections.Sort(delegate(TextSection a, TextSection b) { return a.Html.CompareTo(b.Html); });
            return sections;
        }

        static object ParseStructure(string text, ref int position, int depth, ref int nodes)
        {
            if (position < 0 || depth > 16 || ++nodes > 2048) throw new MailContentException("structure_limit", "邮件结构超出限制");
            while (position < text.Length && char.IsWhiteSpace(text[position])) position++;
            if (position >= text.Length) throw new MailContentException("structure_invalid", "邮件结构不完整");
            if (text[position] == '(')
            {
                position++;
                List<object> list = new List<object>();
                while (position < text.Length)
                {
                    while (position < text.Length && char.IsWhiteSpace(text[position])) position++;
                    if (position < text.Length && text[position] == ')') { position++; return list; }
                    list.Add(ParseStructure(text, ref position, depth + 1, ref nodes));
                }
                throw new MailContentException("structure_invalid", "邮件结构未闭合");
            }
            StringBuilder value = new StringBuilder();
            if (text[position] == '"')
            {
                position++;
                while (position < text.Length)
                {
                    char c = text[position++];
                    if (c == '"') return value.ToString();
                    if (c == '\\' && position < text.Length) c = text[position++];
                    value.Append(c);
                }
                throw new MailContentException("structure_invalid", "邮件结构字符串未闭合");
            }
            while (position < text.Length && !char.IsWhiteSpace(text[position]) && text[position] != ')' && text[position] != '(')
                value.Append(text[position++]);
            if (value.Length == 0) throw new MailContentException("structure_invalid", "邮件结构无效");
            return value.ToString();
        }

        static string StructureString(List<object> part, int index)
        {
            return part != null && index < part.Count ? part[index] as string ?? "" : "";
        }

        static bool IsAttachmentStructure(List<object> part)
        {
            int paramIndex = 2, dispositionIndex = StructureString(part, 0).Equals("TEXT", StringComparison.OrdinalIgnoreCase) ? 9 : 8;
            if (part.Count > 0 && part[0] is List<object>)
            {
                int children = 0;
                while (children < part.Count && part[children] is List<object>) children++;
                paramIndex = children + 1; dispositionIndex = children + 2;
            }
            List<object> parameters = paramIndex < part.Count ? part[paramIndex] as List<object> : null;
            List<object> disposition = dispositionIndex < part.Count ? part[dispositionIndex] as List<object> : null;
            if (disposition != null && StructureString(disposition, 0).Equals("ATTACHMENT", StringComparison.OrdinalIgnoreCase)) return true;
            if (ContainsFilename(parameters)) return true;
            if (disposition != null && disposition.Count > 1 && ContainsFilename(disposition[1] as List<object>)) return true;
            return false;
        }

        static bool ContainsFilename(List<object> parameters)
        {
            if (parameters == null) return false;
            for (int i = 0; i + 1 < parameters.Count; i += 2)
                if (StructureString(parameters, i).Equals("NAME", StringComparison.OrdinalIgnoreCase)
                    || StructureString(parameters, i).Equals("FILENAME", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static void SelectTextSections(List<object> part, string prefix, List<TextSection> result)
        {
            if (part == null || part.Count == 0 || IsAttachmentStructure(part)) return;
            if (part[0] is List<object>)
            {
                for (int i = 0; i < part.Count && part[i] is List<object>; i++)
                    SelectTextSections((List<object>)part[i], prefix.Length == 0 ? (i + 1).ToString() : prefix + "." + (i + 1), result);
                return;
            }
            if (!StructureString(part, 0).Equals("TEXT", StringComparison.OrdinalIgnoreCase)) return;
            string subtype = StructureString(part, 1);
            if (!subtype.Equals("PLAIN", StringComparison.OrdinalIgnoreCase) && !subtype.Equals("HTML", StringComparison.OrdinalIgnoreCase)) return;
            string charset = "UTF-8";
            List<object> parameters = part.Count > 2 ? part[2] as List<object> : null;
            if (parameters != null)
                for (int i = 0; i + 1 < parameters.Count; i += 2)
                    if (StructureString(parameters, i).Equals("CHARSET", StringComparison.OrdinalIgnoreCase)) charset = StructureString(parameters, i + 1);
            result.Add(new TextSection { Section = prefix.Length == 0 ? "1" : prefix, Charset = charset,
                Transfer = StructureString(part, 5), Html = subtype.Equals("HTML", StringComparison.OrdinalIgnoreCase) });
        }

        public byte[] FetchFull(long uid)
        {
            // 只取前 512KB，防止超大附件邮件拖慢解析；验证码邮件正文远小于此。
            return FetchLiteral(uid, "BODY.PEEK[]<0.512000>");
        }

        byte[] FetchLiteral(long uid, string spec)
        {
            string tag = NextTag();
            Send(tag + " UID FETCH " + uid.ToString(CultureInfo.InvariantCulture) + " (" + spec + ")\r\n");
            byte[] found = null;
            List<byte[]> literals = new List<byte[]>();
            StringBuilder response = new StringBuilder();
            bool inFetch = false;
            Action finish = delegate
            {
                if (NumberAfter(response.ToString(), "UID") != uid || literals.Count == 0) return;
                if (found == null) found = literals[0];
            };
            ReadUntilTag(tag, delegate(Line ln)
            {
                if (Regex.IsMatch(ln.Text, @"^\* [0-9]+ FETCH ", RegexOptions.IgnoreCase))
                {
                    if (inFetch) finish();
                    response.Length = 0; literals.Clear(); inFetch = true;
                }
                else if (ln.Text.StartsWith("* ", StringComparison.Ordinal) || ln.Text.StartsWith(tag + " ", StringComparison.Ordinal))
                {
                    if (inFetch) finish();
                    inFetch = false;
                }
                if (!inFetch) return;
                response.Append(ln.Text).Append(' ');
                if (ln.Literal != null) literals.Add(ln.Literal);
            });
            if (inFetch) finish();
            return found;
        }

        public void Noop()
        {
            Cmd("NOOP");
        }

        // ---------- IDLE ----------

        public void StartIdle()
        {
            idleTag = NextTag();
            Send(idleTag + " IDLE\r\n");
            for (int count = 0; count < 1000; count++)
            {
                string line = ReadLine();
                if (line == null || line.StartsWith("* BYE", StringComparison.OrdinalIgnoreCase)) throw new IOException("连接已断开");
                if (line.StartsWith("+", StringComparison.Ordinal)) { idleActive = true; return; }
                if (line.StartsWith(idleTag + " ", StringComparison.Ordinal)) throw new ImapException("IDLE 被服务器拒绝");
                if (!line.StartsWith("* ", StringComparison.Ordinal)) throw new IOException("IDLE 协议响应无效");
            }
            throw new IOException("IDLE 响应超出限制");
        }

        /// <summary>等待 IDLE 期间的数据（EXISTS/RECENT 等）；超时返回 Timeout。</summary>
        public IdleWait WaitForIdleData(int sliceMs)
        {
            // 一个持续的异步读取跨切片保留，不用 Socket.Poll 猜测 SslStream 的内部缓冲。
            if (bufPos >= bufLen)
            {
                if (pendingRead == null)
                {
                    pendingReadStream = stream;
                    if (pendingReadStream == null) throw new IOException("连接已断开");
                    pendingRead = pendingReadStream.BeginRead(buf, 0, buf.Length, null, null);
                }
                if (!pendingRead.AsyncWaitHandle.WaitOne(sliceMs)) return IdleWait.Timeout;
                CompletePendingRead();
            }
            try
            {
                string line = ReadLine();
                if (line == null) throw new IOException("连接已断开");
                if (line.StartsWith("+", StringComparison.Ordinal)) return IdleWait.Timeout;
                return IdleWait.Data;
            }
            catch (IOException ex)
            {
                if (IsTimeout(ex)) return IdleWait.Timeout;
                throw;
            }
            finally
            {
                SetReadTimeout(DefaultReadTimeoutMs);
            }
        }

        public void EndIdle()
        {
            if (!idleActive) return;
            idleActive = false;
            Send("DONE\r\n");
            ReadUntilTag(idleTag, null);
        }

        public void Close()
        {
            // 停止时直接从其他线程调用：只断开连接，不再发 DONE（写可能阻塞）。
            // 正常运行路径的 IDLE 已由 EndIdle 正常结束。
            idleActive = false;
            Stream s = stream;
            stream = null;
            try { if (s != null) s.Dispose(); } catch { }
            try { if (tcp != null) tcp.Close(); } catch { }
        }

        // ---------- 协议底层 ----------

        sealed class Line
        {
            public string Text = "";
            public byte[] Literal;
        }

        string Cmd(string command)
        {
            string tag = NextTag();
            Send(tag + " " + command + "\r\n");
            List<Line> lines = ReadUntilTag(tag, null);
            StringBuilder sb = new StringBuilder();
            foreach (Line ln in lines)
            {
                string line = ln.Text;
                if (ln.Literal != null)
                {
                    int marker = line.LastIndexOf('{');
                    if (marker >= 0) line = line.Substring(0, marker) + Quote(Encoding.UTF8.GetString(ln.Literal));
                }
                sb.Append(line).Append('\n');
            }
            return sb.ToString();
        }

        List<Line> ReadUntilTag(string tag, Action<Line> onLine)
        {
            List<Line> lines = new List<Line>();
            long totalBytes = 0;
            while (true)
            {
                string text = ReadLine();
                if (text == null) throw new IOException("连接已断开");
                Line ln = new Line { Text = text };
                int literalLen = LiteralLength(text);
                if (literalLen >= 0) ln.Literal = ReadExact(literalLen);
                totalBytes += text.Length + (ln.Literal == null ? 0 : ln.Literal.Length);
                if (lines.Count >= 10000 || totalBytes > 4 * 1024 * 1024) throw new IOException("IMAP 响应超出安全限制");
                lines.Add(ln);
                if (onLine != null) onLine(ln);
                if (text.StartsWith(tag + " ", StringComparison.Ordinal))
                {
                    string status = text.Substring(tag.Length + 1).Trim();
                    if (status.StartsWith("OK", StringComparison.OrdinalIgnoreCase)) return lines;
                    throw new ImapException("IMAP 命令被拒绝（" + (status.StartsWith("NO", StringComparison.OrdinalIgnoreCase) ? "NO" : "BAD") + "），请检查服务状态和授权")
                        { FetchBad = status.StartsWith("BAD", StringComparison.OrdinalIgnoreCase) };
                }
            }
        }

        static int LiteralLength(string line)
        {
            Match m = Regex.Match(line, "\\{(\\d+)\\}$");
            if (!m.Success) return -1;
            int len;
            if (!int.TryParse(m.Groups[1].Value, out len) || len < 0 || len > 2 * 1024 * 1024) throw new IOException("IMAP 数据块超出安全限制");
            return len;
        }

        void Send(string command)
        {
            Stream s = stream;
            if (s == null) throw new IOException("连接未建立");
            byte[] bytes = Encoding.UTF8.GetBytes(command);
            s.Write(bytes, 0, bytes.Length);
            s.Flush();
        }

        string ReadLine()
        {
            List<byte> line = new List<byte>(128);
            while (true)
            {
                int b = ReadByte();
                if (b < 0)
                {
                    if (line.Count == 0) return null;
                    break;
                }
                if (b == '\n')
                {
                    if (line.Count > 0 && line[line.Count - 1] == (byte)'\r')
                        line.RemoveAt(line.Count - 1);
                    break;
                }
                line.Add((byte)b);
                if (line.Count > 256 * 1024) throw new IOException("IMAP 行超出安全限制");
            }
            return Encoding.UTF8.GetString(line.ToArray());
        }

        int ReadByte()
        {
            if (bufPos >= bufLen)
            {
                if (pendingRead != null) { CompletePendingRead(); if (bufLen <= 0) return -1; return buf[bufPos++]; }
                Stream s = stream;
                if (s == null) return -1;
                bufLen = s.Read(buf, 0, buf.Length);
                bufPos = 0;
                if (bufLen <= 0) { bufLen = 0; return -1; }
            }
            return buf[bufPos++];
        }

        void CompletePendingRead()
        {
            IAsyncResult read = pendingRead;
            Stream source = pendingReadStream;
            pendingRead = null; pendingReadStream = null;
            if (!read.AsyncWaitHandle.WaitOne(IdleCompletionTimeoutMs))
            {
                Close();
                read.AsyncWaitHandle.Close();
                throw new IOException("IDLE 收尾读取超时，重新连接");
            }
            try { bufLen = source.EndRead(read); bufPos = 0; }
            finally { read.AsyncWaitHandle.Close(); }
            if (bufLen <= 0) throw new IOException("连接已断开");
        }

        byte[] ReadExact(int count)
        {
            byte[] result = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                if (bufPos >= bufLen)
                {
                    Stream s = stream;
                    if (s == null) throw new IOException("连接已断开");
                    bufLen = s.Read(buf, 0, buf.Length);
                    bufPos = 0;
                    if (bufLen <= 0) throw new IOException("连接已断开");
                }
                int n = Math.Min(count - offset, bufLen - bufPos);
                Array.Copy(buf, bufPos, result, offset, n);
                bufPos += n;
                offset += n;
            }
            return result;
        }

        void SetReadTimeout(int ms)
        {
            Stream s = stream;
            try { if (s != null) s.ReadTimeout = ms; } catch { }
        }

        string NextTag()
        {
            tagSeq++;
            return "C" + tagSeq.ToString(CultureInfo.InvariantCulture);
        }

        static string Quote(string value)
        {
            // 去掉 CR/LF 防止配置内容注入额外 IMAP 命令。
            return "\"" + (value ?? "").Replace("\r", "").Replace("\n", "")
                .Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        static string UnquoteMailbox(string name)
        {
            if (name.Length >= 2 && name[0] == '"' && name[name.Length - 1] == '"')
            {
                string inner = name.Substring(1, name.Length - 2);
                return inner.Replace("\\\\", "\\").Replace("\\\"", "\"");
            }
            return name;
        }

        static long NumberAfter(string text, string marker)
        {
            Match m = Regex.Match(text, Regex.Escape(marker) + "\\s+(\\d+)", RegexOptions.IgnoreCase);
            long v;
            if (m.Success && long.TryParse(m.Groups[1].Value, out v)) return v;
            return 0;
        }

        static long NumberBefore(string text, string marker)
        {
            Match m = Regex.Match(text, "(\\d+)\\s+" + Regex.Escape(marker), RegexOptions.IgnoreCase);
            long v;
            if (m.Success && long.TryParse(m.Groups[1].Value, out v)) return v;
            return 0;
        }

        static bool IsTimeout(Exception ex)
        {
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                if (e is TimeoutException) return true;
                SocketException se = e as SocketException;
                if (se != null && se.SocketErrorCode == SocketError.TimedOut) return true;
            }
            return false;
        }

        static string Clip(string value)
        {
            string s = (value ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            return s.Length > 160 ? s.Substring(0, 160) : s;
        }
    }

    // ---------- MIME 解析（纯函数） ----------

    static class Mime
    {
        static readonly Encoding Latin1Encoding = Encoding.GetEncoding(28591);

        public static string DecodeTextSection(byte[] data, string transfer, string charset, bool html, bool partial)
        {
            if (data == null || data.Length > 512000) throw new ImapException("邮件正文段超出限制");
            byte[] decoded = DecodeTransfer(data, transfer, partial);
            string text = CharsetEncoding(charset).GetString(decoded);
            if (partial)
            {
                // 丢弃最后可能未完整接收的一行，避免把截断长数字当成短验证码。
                int lastLine = text.LastIndexOf('\n');
                text = lastLine >= 0 ? text.Substring(0, lastLine) : "";
            }
            return html ? StripHtml(text) : text;
        }

        public static string Latin1(byte[] data)
        {
            return Latin1Encoding.GetString(data ?? new byte[0]);
        }

        public static byte[] Latin1Bytes(string s)
        {
            return Latin1Encoding.GetBytes(s ?? "");
        }

        /// <summary>取头字段值（含折行合并）。找不到返回 null。</summary>
        public static string HeaderValue(string rawHeaders, string name)
        {
            if (string.IsNullOrEmpty(rawHeaders)) return null;
            string[] lines = rawHeaders.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            StringBuilder cur = null;
            string result = null;
            foreach (string line in lines)
            {
                if (line.Length == 0) continue;
                if (line[0] == ' ' || line[0] == '\t')
                {
                    if (cur != null) cur.Append(' ').Append(line.Trim());
                    continue;
                }
                if (cur != null) { result = cur.ToString(); break; }
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                if (!line.Substring(0, colon).Trim().Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
                cur = new StringBuilder(line.Substring(colon + 1).Trim());
            }
            if (result == null && cur != null) result = cur.ToString();
            return result;
        }

        /// <summary>解码 MIME 编码词（=?UTF-8?B?...?= / =?...?Q?...?=）。</summary>
        public static string DecodeWords(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf("=?", StringComparison.Ordinal) < 0) return value ?? "";
            MatchCollection matches = Regex.Matches(value, "=\\?([^?\\s]+)\\?([bBqQ])\\?(.*?)\\?=", RegexOptions.Singleline);
            if (matches.Count == 0) return value;
            StringBuilder sb = new StringBuilder();
            int last = 0;
            bool hasPrev = false;
            foreach (Match m in matches)
            {
                string gap = value.Substring(last, m.Index - last);
                if (!(hasPrev && gap.Trim().Length == 0)) sb.Append(gap);
                string charset = m.Groups[1].Value;
                string kind = m.Groups[2].Value;
                string data = m.Groups[3].Value;
                Encoding enc = CharsetEncoding(charset);
                try
                {
                    if (kind.Equals("B", StringComparison.OrdinalIgnoreCase))
                        sb.Append(enc.GetString(Convert.FromBase64String(data)));
                    else
                        sb.Append(enc.GetString(QpDecode(Latin1Bytes(data.Replace('_', ' ')))));
                }
                catch { sb.Append(data); }
                last = m.Index + m.Length;
                hasPrev = true;
            }
            sb.Append(value.Substring(last));
            return sb.ToString();
        }

        /// <summary>把整封邮件正文转成纯文本（优先 text/plain，其次 text/html 去标签）。</summary>
        public static string ExtractText(byte[] raw)
        {
            try
            {
                if (raw == null || raw.Length == 0) return "";
                string all = Latin1(raw);
                int split = all.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                int skip = 4;
                if (split < 0)
                {
                    split = all.IndexOf("\n\n", StringComparison.Ordinal);
                    skip = 2;
                }
                if (split < 0) return "";
                string headers = all.Substring(0, split);
                string body = all.Substring(split + skip);
                string text = TextFromPart(headers, Latin1Bytes(body), 0);
                return text ?? "";
            }
            catch { return ""; }
        }

        static string TextFromPart(string headers, byte[] body, int depth)
        {
            if (depth > 8 || body.Length > 1024 * 1024) return null;
            string disposition = HeaderValue(headers, "Content-Disposition") ?? "";
            if (disposition.IndexOf("attachment", StringComparison.OrdinalIgnoreCase) >= 0
                || CTypeParam(disposition, "filename") != null) return null;
            string ctype = HeaderValue(headers, "Content-Type") ?? "";
            string subtype = SubtypeOf(ctype);
            string boundary = CTypeParam(ctype, "boundary");
            string charset = CTypeParam(ctype, "charset");
            if (CTypeParam(ctype, "name") != null || subtype.Equals("message/rfc822", StringComparison.OrdinalIgnoreCase)) return null;

            if (subtype.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(boundary))
            {
                string plain = null, html = null;
                foreach (Part part in SplitParts(body, boundary))
                {
                    string ps = SubtypeOf(HeaderValue(part.Headers, "Content-Type") ?? "");
                    string pd = HeaderValue(part.Headers, "Content-Disposition") ?? "";
                    if (pd.IndexOf("attachment", StringComparison.OrdinalIgnoreCase) >= 0
                        || CTypeParam(pd, "filename") != null || CTypeParam(HeaderValue(part.Headers, "Content-Type"), "name") != null
                        || ps.Equals("message/rfc822", StringComparison.OrdinalIgnoreCase)) continue;
                    if (ps.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase))
                    {
                        string nested = TextFromPart(part.Headers, part.Body, depth + 1);
                        if (nested != null && nested.Length > 0)
                        {
                            if (plain == null) plain = nested;
                        }
                        continue;
                    }
                    byte[] cooked = DecodeTransfer(part.Body, HeaderValue(part.Headers, "Content-Transfer-Encoding"));
                    string text = CharsetEncoding(CTypeParam(HeaderValue(part.Headers, "Content-Type") ?? "", "charset")).GetString(cooked);
                    if (ps.Length == 0 || ps.Equals("text/plain", StringComparison.OrdinalIgnoreCase))
                    {
                        if (plain == null) plain = text;
                    }
                    else if (ps.Equals("text/html", StringComparison.OrdinalIgnoreCase))
                    {
                        if (html == null) html = StripHtml(text);
                    }
                }
                if (plain != null && plain.Trim().Length > 0) return plain;
                if (html != null && html.Trim().Length > 0) return html;
                return plain ?? html ?? "";
            }

            byte[] bodyCooked = DecodeTransfer(body, HeaderValue(headers, "Content-Transfer-Encoding"));
            string raw = CharsetEncoding(charset).GetString(bodyCooked);
            if (subtype.Equals("text/html", StringComparison.OrdinalIgnoreCase)) return StripHtml(raw);
            if (subtype.Length == 0 || subtype.StartsWith("text/", StringComparison.OrdinalIgnoreCase)) return raw;
            return "";
        }

        sealed class Part
        {
            public string Headers = "";
            public byte[] Body = new byte[0];
        }

        static List<Part> SplitParts(byte[] body, string boundary)
        {
            List<Part> parts = new List<Part>();
            if (boundary.Length > 256) return parts;
            string s = Latin1(body);
            string marker = "--" + boundary;
            int pos = s.IndexOf(marker, StringComparison.Ordinal);
            while (pos >= 0)
            {
                int start = pos + marker.Length;
                if (start + 1 < s.Length && s[start] == '-' && s[start + 1] == '-') break;
                if (start < s.Length && s[start] == '\r') start++;
                if (start < s.Length && s[start] == '\n') start++;
                int next = s.IndexOf(marker, start, StringComparison.Ordinal);
                int end = next < 0 ? s.Length : next;
                string chunk = s.Substring(start, end - start);
                chunk = chunk.TrimEnd('\r', '\n');
                int idx = chunk.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                int skip = 4;
                if (idx < 0)
                {
                    idx = chunk.IndexOf("\n\n", StringComparison.Ordinal);
                    skip = 2;
                }
                Part part = new Part();
                if (idx < 0) part.Headers = chunk;
                else
                {
                    part.Headers = chunk.Substring(0, idx);
                    part.Body = Latin1Bytes(chunk.Substring(idx + skip));
                }
                parts.Add(part);
                if (parts.Count >= 128) break;
                pos = next;
            }
            return parts;
        }

        static byte[] DecodeTransfer(byte[] body, string cte)
        {
            return DecodeTransfer(body, cte, false);
        }

        static byte[] DecodeTransfer(byte[] body, string cte, bool partial)
        {
            string mode = (cte ?? "").Trim().ToLowerInvariant();
            if (mode.Contains("base64"))
            {
                string s = Latin1(body);
                StringBuilder b64 = new StringBuilder(s.Length);
                foreach (char c in s)
                    if (!char.IsWhiteSpace(c)) b64.Append(c);
                if (partial) b64.Length -= b64.Length % 4;
                try { return Convert.FromBase64String(b64.ToString()); }
                catch (FormatException) { throw new MailContentException("base64_invalid", "邮件正文 Base64 编码无效"); }
            }
            if (mode.Contains("quoted-printable")) return QpDecode(body);
            return body;
        }

        static byte[] QpDecode(byte[] data)
        {
            MemoryStream ms = new MemoryStream();
            for (int i = 0; i < data.Length; i++)
            {
                byte b = data[i];
                if (b != (byte)'=')
                {
                    ms.WriteByte(b);
                    continue;
                }
                if (i + 2 < data.Length && IsHex(data[i + 1]) && IsHex(data[i + 2]))
                {
                    ms.WriteByte((byte)((HexVal(data[i + 1]) << 4) | HexVal(data[i + 2])));
                    i += 2;
                }
                else if (i + 1 < data.Length && data[i + 1] == (byte)'\r'
                         && i + 2 < data.Length && data[i + 2] == (byte)'\n')
                {
                    i += 2;   // 软换行
                }
                else if (i + 1 < data.Length && data[i + 1] == (byte)'\n')
                {
                    i += 1;   // 软换行（LF）
                }
                else
                {
                    ms.WriteByte(b);
                }
            }
            return ms.ToArray();
        }

        static bool IsHex(byte b)
        {
            return (b >= (byte)'0' && b <= (byte)'9') || (b >= (byte)'A' && b <= (byte)'F') || (b >= (byte)'a' && b <= (byte)'f');
        }

        static int HexVal(byte b)
        {
            if (b >= (byte)'0' && b <= (byte)'9') return b - (byte)'0';
            if (b >= (byte)'A' && b <= (byte)'F') return b - (byte)'A' + 10;
            return b - (byte)'a' + 10;
        }

        static string SubtypeOf(string ctype)
        {
            if (string.IsNullOrEmpty(ctype)) return "";
            int semi = ctype.IndexOf(';');
            string head = (semi < 0 ? ctype : ctype.Substring(0, semi)).Trim();
            return head.ToLowerInvariant();
        }

        static string CTypeParam(string ctype, string name)
        {
            if (string.IsNullOrEmpty(ctype)) return null;
            Match m = Regex.Match(ctype, name + "\\s*=\\s*(\"([^\"]*)\"|([^;\\s]+))", RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            return m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;
        }

        static Encoding CharsetEncoding(string charset)
        {
            if (!string.IsNullOrEmpty(charset))
            {
                string name = charset.Trim().Trim('"', '\'');
                try { return Encoding.GetEncoding(name); } catch { }
                string lower = name.ToLowerInvariant();
                if (lower.Contains("gb")) { try { return Encoding.GetEncoding("GBK"); } catch { } }
            }
            return Encoding.UTF8;
        }

        /// <summary>HTML 转纯文本：去掉脚本/样式，块元素换行，实体解码。</summary>
        public static string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            if (html.Length > 1024 * 1024) throw new FormatException("HTML 正文超出限制");
            string s = SafeReplace(html, "(?is)<(script|style)\\b[^>]*>.*?</\\1\\s*>", " ");
            s = SafeReplace(s, "(?i)<br\\s*/?>", "\n");
            s = SafeReplace(s, "(?i)</?(p|div|tr|li|h[1-6])\\b[^>]*>", "\n");
            s = SafeReplace(s, "(?i)</?(table|ul|ol)\\b[^>]*>", "\n");
            s = SafeReplace(s, "(?i)</?(td|th)\\b[^>]*>", " ");
            s = SafeReplace(s, "<[^>]+>", " ");
            s = System.Net.WebUtility.HtmlDecode(s);
            s = SafeReplace(s, "[ \\t\\u00a0]+", " ");
            s = SafeReplace(s, "(?:\\r?\\n){3,}", "\n\n");
            return s.Trim();
        }

        static string SafeReplace(string text, string pattern, string replacement)
        {
            return Regex.Replace(text, pattern, replacement, RegexOptions.None, TimeSpan.FromMilliseconds(200));
        }

        /// <summary>解码 IMAP 文件夹名使用的修正版 UTF-7（RFC 3501）。</summary>
        public static string DecodeModifiedUtf7(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf('&') < 0) return value ?? "";
            StringBuilder sb = new StringBuilder();
            int i = 0;
            while (i < value.Length)
            {
                char c = value[i];
                if (c != '&')
                {
                    sb.Append(c);
                    i++;
                    continue;
                }
                int end = value.IndexOf('-', i + 1);
                if (end < 0)
                {
                    sb.Append(c);
                    i++;
                    continue;
                }
                string chunk = value.Substring(i + 1, end - i - 1);
                if (chunk.Length == 0)
                {
                    sb.Append('&');
                    i = end + 1;
                    continue;
                }
                try
                {
                    string b64 = chunk.Replace(',', '/');
                    b64 += new string('=', (4 - b64.Length % 4) % 4);
                    sb.Append(Encoding.BigEndianUnicode.GetString(Convert.FromBase64String(b64)));
                }
                catch { sb.Append(value.Substring(i, end - i + 1)); }
                i = end + 1;
            }
            return sb.ToString();
        }
    }
}
