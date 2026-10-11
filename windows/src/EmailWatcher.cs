// 邮箱验证码接收（可选）
// - 极简 IMAP 客户端：只读方式检查邮箱（EXAMINE + BODY.PEEK，不标记已读）
// - 支持 IDLE 实时推送；服务器不支持（如 163/126）或断线时降级为按间隔轮询
// - 只有主题命中关键词（且发件人符合白名单）的邮件才下载正文并提取验证码
// - 水位线（UID）保存在 config.ini：首次启用只处理其后新到的邮件；
//   另设 30 分钟时效窗口，避免把早已过期的验证码复制进剪贴板
// - 停止/重启使用“生成号”语义：RequestStop 递增生成号，旧线程自查退出并由
//   连接断开立即唤醒，避免“停止请求被新线程入口重置吞掉”的竞态

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Net.Mail;
using System.Security.Authentication;
using System.Threading;
using System.Text;
using System.Security.Cryptography;

namespace Codepass
{
    static class EmailWatcher
    {
        static int stopGen;
        static int junkGen;
        static readonly object ConnGate = new object();
        static Imap activeConn;
        static Imap activeJunkConn;
        static readonly Dictionary<CancellationTokenSource, bool> authRequests = new Dictionary<CancellationTokenSource, bool>();
        // 被 QQ 弹窗联动唤醒（Kick）：轮询/IDLE 切片等待立即返回。
        static readonly ManualResetEventSlim wake = new ManualResetEventSlim(false);
        static readonly ManualResetEventSlim junkWake = new ManualResetEventSlim(false);
        static int failures;
        static DateTime lastStaleLogAt = DateTime.MinValue;
        internal static string Status = "尚未连接";
        internal static DateTime LastCheckUtc = DateTime.MinValue;
        internal static string LastMailResult = "";
        internal static string LastFolderResult = "";
        internal static string FolderStatus = "等待检查";
        internal static DateTime LastFolderCheckUtc = DateTime.MinValue;
        internal static string RecentSummary = "";
        internal static bool RecentCheckPending
        {
            get { return Program.cfg != null && (PendingRecentRequest(Program.cfg, false) != 0 || PendingRecentRequest(Program.cfg, true) != 0); }
        }
        sealed class ContentFailure { public int Count; public string Reason; }
        static readonly Dictionary<string, ContentFailure> contentFailures = new Dictionary<string, ContentFailure>();
        static int recentRequest, mainRecentCompleted, junkRecentCompleted;
        static string recentIdentity;

        public static void RequestRecentCheck()
        {
            lock (Config.SaveGate)
            {
                if (!Program.EmailConfigured()) return;
                recentIdentity = RecentIdentity(Program.cfg);
                Interlocked.Increment(ref recentRequest);
                Kick(); junkWake.Set();
                Program.EmailStatusChanged();
            }
        }

        static string RecentIdentity(Config identity)
        {
            return HashKey(MailboxKey(identity, "") + "\n" + identity.EmailAuth);
        }

        internal static int PendingRecentRequest(Config identity, bool junk)
        {
            lock (Config.SaveGate)
            {
                if (!Program.EmailConfigured() || recentIdentity == null || recentIdentity != RecentIdentity(identity)
                    || recentIdentity != RecentIdentity(Program.cfg)) return 0;
                int requested = Volatile.Read(ref recentRequest);
                int completed = junk ? Volatile.Read(ref junkRecentCompleted) : Volatile.Read(ref mainRecentCompleted);
                return requested == completed ? 0 : requested;
            }
        }

        static void MailResult(int gen, bool junk, Config identity, string message)
        {
            lock (Config.SaveGate)
            {
                if (!CanCommit(gen, junk, identity)) return;
                if (junk) LastFolderResult = message;
                else LastMailResult = message;
                Program.EmailStatusChanged();
            }
        }

        static void SetStatus(int gen, Config identity, string value)
        {
            lock (Config.SaveGate)
            {
            if (!CanCommit(gen, false, identity)) return;
            Status = value;
            Program.EmailStatusChanged();
            }
        }

        static void FolderState(int gen, Config identity, string value, bool success)
        {
            lock (Config.SaveGate)
            {
                if (!CanCommit(gen, true, identity)) return;
                FolderStatus = value;
                if (success) LastFolderCheckUtc = DateTime.UtcNow;
                Program.EmailStatusChanged();
            }
        }

        static void Checked(int gen, bool folders, Config identity)
        {
            lock (Config.SaveGate)
            {
                if (!CanCommit(gen, folders, identity)) return;
                if (!folders) LastCheckUtc = DateTime.UtcNow;
                Program.EmailStatusChanged();
            }
        }

        static string FailureCategory(Exception ex)
        {
            if (ex is MailContentException) return "content";
            if (ex is AuthenticationException) return "tls";
            if (ex is ImapException) return "protocol_or_auth";
            if (Config.LastSaveError != null || History.LastSaveError != null) return "storage";
            return ex is IOException || ex is SocketException ? "network" : "unexpected";
        }

        internal static string TestConnection(Config identity, CancellationToken token)
        {
            Imap imap = new Imap();
            try
            {
                using (CancellationTokenRegistration registration = token.Register(imap.Close))
                {
                    token.ThrowIfCancellationRequested();
                    if (identity.EmailPasswordRequired || identity.EmailAuth.Trim().Length == 0)
                        throw new ImapException("请先填写 IMAP 授权码或应用专用密码");
                    imap.Connect(identity.EmailServer, 993, 15000);
                    token.ThrowIfCancellationRequested();
                    imap.PrepareAndLogin(identity.EmailAccount, identity.EmailAuth);
                    token.ThrowIfCancellationRequested();
                    imap.Examine("INBOX");
                    return "连接成功 · 认证与只读收件箱可用 · " + (imap.HasCapability("IDLE") ? "支持实时 IDLE" : "使用定时轮询");
                }
            }
            finally { imap.Close(); }
        }

        static Config SnapshotConfig()
        {
            Config snapshot = new Config(); snapshot.CopyFrom(Program.cfg); return snapshot;
        }

        internal static bool CanCommit(int gen, bool junk, Config identity)
        {
            return GenAlive(gen, junk) && Program.Running && Program.EmailConfigured()
                && string.Equals(identity.EmailServer, Program.cfg.EmailServer, StringComparison.OrdinalIgnoreCase)
                && string.Equals(identity.EmailAccount, Program.cfg.EmailAccount, StringComparison.OrdinalIgnoreCase)
                && identity.EmailAuth == Program.cfg.EmailAuth;
        }
        const int StaleMailMinutes = 30;

        // ---------- 线程入口 ----------

        /// <summary>创建监测线程前取一个生成号；传入 MainLoop/JunkLoop 作为该线程的身份，
        /// 之后任何 RequestStop 都会使旧生成号失效，本线程据此自查退出。</summary>
        public static int BeginMainGeneration()
        {
            return Interlocked.Increment(ref stopGen);
        }

        public static int BeginJunkGeneration()
        {
            return Interlocked.Increment(ref junkGen);
        }

        static bool GenAlive(int gen, bool junk)
        {
            int current = junk ? Volatile.Read(ref junkGen) : Volatile.Read(ref stopGen);
            return gen == current;
        }

        public static void MainLoop(int gen)
        {
            failures = 0;
            Log.Write("email watcher started");
            while (GenAlive(gen, false) && Program.Running)
            {
                if (!Program.EmailConfigured())
                {
                    Thread.Sleep(2000);
                    continue;
                }
                try
                {
                    RunSession(gen);
                }
                catch (Exception ex)
                {
                    if (!GenAlive(gen, false) || !Program.Running) break;
                    failures = Math.Min(12, failures + 1);
                    Log.Write("email error category=" + FailureCategory(ex));
                    SetStatus(gen, SnapshotConfig(), FailureCategory(ex) == "storage" ? "保存未完成，正在重试（详见保存提示）"
                        : ex is MailContentException ? "单封邮件结构异常，正在重试" : "连接失败，正在退避重试（" + ex.GetType().Name + "）");
                    // 退避等待：5/10/.../60 秒，可被 Kick 或停止请求提前唤醒。
                    int delay = Math.Min(60, 5 * failures);
                    wake.Wait(delay * 1000);
                    wake.Reset();
                }
            }
            Log.Write("email watcher stopped");
        }

        public static void JunkLoop(int gen)
        {
            int folderFailures = 0;
            Log.Write("email folders watcher started");
            while (GenAlive(gen, true) && Program.Running)
            {
                if (!Program.EmailConfigured())
                {
                    Thread.Sleep(3000);
                    continue;
                }
                try
                {
                    JunkCheckOnce(gen);
                    folderFailures = 0;
                }
                catch (Exception ex)
                {
                    if (!GenAlive(gen, true) || !Program.Running) break;
                    folderFailures = Math.Min(12, folderFailures + 1);
                    Log.Write("email folders error category=" + FailureCategory(ex));
                    FolderState(gen, SnapshotConfig(), "检查失败，正在重试（" + FailureCategory(ex) + "）", false);
                }
                int seconds = Program.cfg.EmailPollSeconds;
                if (seconds < 10) seconds = 10;
                if (seconds > 600) seconds = 600;
                if (folderFailures > 0) seconds = Math.Max(seconds, Math.Min(60, 5 * folderFailures));
                junkWake.Wait(seconds * 1000);
                junkWake.Reset();
            }
            Log.Write("email folders watcher stopped");
        }

        /// <summary>停止收件箱/垃圾箱监测：生成号递增让旧线程退出，并断开连接立即唤醒。</summary>
        public static void RequestStop()
        {
            lock (Config.SaveGate)
            {
            Interlocked.Increment(ref stopGen);
            Interlocked.Increment(ref junkGen);
            if (!Program.EmailConfigured() || recentIdentity != RecentIdentity(Program.cfg))
            {
                recentIdentity = null;
                mainRecentCompleted = junkRecentCompleted = Volatile.Read(ref recentRequest);
                contentFailures.Clear();
                LastMailResult = LastFolderResult = RecentSummary = "";
                LastCheckUtc = LastFolderCheckUtc = DateTime.MinValue;
                FolderStatus = "等待检查";
            }
            }
            CancelAuthentication(false);
            CloseActive(true, true);
            wake.Set();
            junkWake.Set();
        }

        public static void RequestStopJunk()
        {
            lock (Config.SaveGate) Interlocked.Increment(ref junkGen);
            CancelAuthentication(true);
            CloseActive(false, true);
            junkWake.Set();
        }

        static void CancelAuthentication(bool onlyJunk)
        {
            List<CancellationTokenSource> pending = new List<CancellationTokenSource>();
            lock (ConnGate)
                foreach (KeyValuePair<CancellationTokenSource, bool> request in authRequests)
                    if (!onlyJunk || request.Value) pending.Add(request.Key);
            foreach (CancellationTokenSource source in pending)
                try { source.Cancel(); } catch (ObjectDisposedException) { }
        }

        /// <summary>QQ 弹窗等外部信号：立即检查一次邮箱，不等下一个轮询间隔。</summary>
        public static void Kick()
        {
            wake.Set();
            junkWake.Set();
        }

        static void CloseActive(bool closeMain, bool closeJunk)
        {
            if (closeMain)
            {
                Imap c;
                lock (ConnGate) c = activeConn;
                if (c != null) { try { c.Close(); } catch { } }
            }
            if (closeJunk)
            {
                Imap c;
                lock (ConnGate) c = activeJunkConn;
                if (c != null) { try { c.Close(); } catch { } }
            }
        }

        static string Describe(Exception ex)
        {
            if (ex is ImapException) return ex.Message;
            if (ex is IOException || ex is SocketException) return "网络或持久化失败";
            if (ex is System.Security.Authentication.AuthenticationException) return "TLS 握手失败";
            return ex.GetType().Name;
        }

        // ---------- 会话 ----------

        static void RunSession(int gen)
        {
            Config cfg = SnapshotConfig();
            Imap imap = new Imap();
            lock (Config.SaveGate)
            lock (ConnGate)
            {
                if (!CanCommit(gen, false, cfg)) return;
                activeConn = imap;
            }
            try
            {
                SetStatus(gen, cfg, "正在连接邮箱");
                ConnectAndLogin(imap, cfg, gen, false);
                failures = 0;   // 登录成功才清零退避，授权码错误时退避才能逐步拉长
                Log.Write("email connected: " + cfg.EmailServer);
                bool idleSupported = imap.HasCapability("IDLE");

                Mailbox mb = imap.Examine("INBOX");
                long water = InitializeWater(cfg, mb, gen, false);
                water = ProcessNew(imap, water, false, gen, cfg, mb);
                ProcessRequestedRecent(imap, gen, cfg, mb);

                if (idleSupported)
                {
                    Log.Write("email: realtime mode (IDLE)");
                    SetStatus(gen, cfg, "已连接 · 实时接收（IDLE）");
                    try { IdleLoop(imap, ref water, gen, cfg, mb); }
                    catch (ImapException)
                    {
                        if (!GenAlive(gen, false)) return;
                        Log.Write("email: IDLE rejected, switching to polling");
                        imap.Noop();
                        SetStatus(gen, cfg, "已连接 · IDLE 不可用，改为轮询");
                        PollLoop(imap, ref water, gen, cfg, mb);
                    }
                }
                else
                {
                    Log.Write("email: polling mode");
                    SetStatus(gen, cfg, "已连接 · 定时轮询");
                    PollLoop(imap, ref water, gen, cfg, mb);
                }
            }
            finally
            {
                lock (ConnGate) { if (activeConn == imap) activeConn = null; }
                imap.Close();
            }
        }

        static void PollLoop(Imap imap, ref long water, int gen, Config identity, Mailbox mailbox)
        {
            while (GenAlive(gen, false) && Program.Running && Program.EmailConfigured())
            {
                int seconds = Program.cfg.EmailPollSeconds;
                if (seconds < 10) seconds = 10;
                if (seconds > 600) seconds = 600;
                bool kicked = wake.Wait(seconds * 1000);
                wake.Reset();
                if (!GenAlive(gen, false) || !Program.EmailConfigured()) break;
                if (kicked) Log.Write("email: kicked by qq popup");
                imap.Noop();
                water = ProcessNew(imap, water, false, gen, identity, mailbox);
                ProcessRequestedRecent(imap, gen, identity, mailbox);
            }
        }

        static void IdleLoop(Imap imap, ref long water, int gen, Config identity, Mailbox mailbox)
        {
            while (GenAlive(gen, false) && Program.Running && Program.EmailConfigured())
            {
                imap.StartIdle();
                // 每次最多空闲 25 分钟（RFC 2177 建议不超过 29 分钟），
                // 期间按 800ms 切片检查停止/Kick 信号。
                bool gotEvent = false;
                int waited = 0;
                while (GenAlive(gen, false) && Program.Running
                       && Program.EmailConfigured() && waited < 25 * 60 * 1000)
                {
                    if (wake.IsSet)
                    {
                        wake.Reset();
                        if (GenAlive(gen, false) && Program.EmailConfigured())
                            Log.Write("email: kicked by qq popup");
                        break;
                    }
                    if (imap.WaitForIdleData(800) == IdleWait.Data) { gotEvent = true; break; }
                    waited += 800;
                }
                imap.EndIdle();
                if (!GenAlive(gen, false) || !Program.EmailConfigured()) break;
                if (gotEvent) Log.Write("email: new mail signal (IDLE)");
                water = ProcessNew(imap, water, false, gen, identity, mailbox);
                ProcessRequestedRecent(imap, gen, identity, mailbox);
            }
        }

        static void JunkCheckOnce(int gen)
        {
            Config cfg = SnapshotConfig();
            Imap imap = new Imap();
            lock (Config.SaveGate)
            lock (ConnGate)
            {
                if (!CanCommit(gen, true, cfg)) return;
                activeJunkConn = imap;
            }
            try
            {
                ConnectAndLogin(imap, cfg, gen, true);
                List<ImapFolder> folders = ReceiveFolders(imap.ListFolderDetails(), cfg.EmailCheckJunk);
                FolderState(gen, cfg, "正在检查 " + folders.Count + " 个目录", false);
                if (folders.Count == 0)
                {
                    Log.Write("email folders: no additional receiving folder found");
                    int emptyRequest = PendingRecentRequest(cfg, true);
                    if (emptyRequest != 0 && CanCommit(gen, true, cfg)) Interlocked.Exchange(ref junkRecentCompleted, emptyRequest);
                    FolderState(gen, cfg, "没有额外接收目录", true);
                    return;
                }
                int requested = PendingRecentRequest(cfg, true);
                bool recheck = requested != 0;
                bool complete = true;
                int succeeded = 0;
                foreach (ImapFolder folder in folders)
                {
                    if (!CanCommit(gen, true, cfg)) return;
                    try
                    {
                        ProcessReceiveFolder(imap, gen, cfg, folder, recheck);
                        succeeded++;
                    }
                    catch (ImapException) { complete = false; Log.Write("email folders: folder temporarily unavailable, retry next check"); }
                    catch (MailContentException) { complete = false; Log.Write("email folders: content retry pending"); }
                }
                if (recheck && complete && CanCommit(gen, true, cfg)) Interlocked.Exchange(ref junkRecentCompleted, requested);
                FolderState(gen, cfg, succeeded + "/" + folders.Count + " 个目录检查成功" + (complete ? "" : "，其余稍后重试"), complete);
            }
            finally
            {
                lock (ConnGate) { if (activeJunkConn == imap) activeJunkConn = null; }
                imap.Close();
            }
        }

        static void ConnectAndLogin(Imap imap, Config cfg, int gen, bool junk)
        {
            using (CancellationTokenSource source = new CancellationTokenSource())
            {
                lock (ConnGate)
                {
                    if (!GenAlive(gen, junk)) throw new OperationCanceledException();
                    authRequests.Add(source, junk);
                }
                Config identity = new Config(); identity.CopyFrom(cfg);
                try
                {
                    using (CancellationTokenRegistration registration = source.Token.Register(imap.Close))
                    {
                        source.Token.ThrowIfCancellationRequested();
                        if (identity.EmailPasswordRequired || identity.EmailAuth.Trim().Length == 0)
                            throw new ImapException("请先填写 IMAP 授权码或应用专用密码");
                        imap.Connect(identity.EmailServer.Trim(), 993, 15000);
                        source.Token.ThrowIfCancellationRequested();
                        if (!GenAlive(gen, junk)) throw new OperationCanceledException();
                        imap.PrepareAndLogin(identity.EmailAccount, identity.EmailAuth);
                    }
                }
                finally { lock (ConnGate) authRequests.Remove(source); }
            }
        }

        internal static void ProcessReceiveFolder(Imap imap, int gen, Config identity, ImapFolder folder, bool recheck)
        {
            Mailbox mailbox = imap.Examine(folder.Name);
            long previousWater;
            bool knownFolder = Program.cfg.TryMailboxWater(MailboxKey(identity, mailbox.Name), mailbox.UidValidity, out previousWater);
            bool initialCategory = !knownFolder && !folder.Junk && !MailRules.IsJunkFolderName(Mime.DecodeModifiedUtf7(folder.Name));
            // 初次分类目录补查成功后才初始化水位；失败则下一轮可完整重试。
            if (initialCategory) ProcessRecentBefore(imap, gen, true, identity, mailbox, mailbox.UidNext);
            long water = InitializeWater(identity, mailbox, gen, true);
            ProcessNew(imap, water, true, gen, identity, mailbox);
            if (recheck && !initialCategory) ProcessRecent(imap, gen, true, identity, mailbox);
        }

        internal static List<ImapFolder> JunkFolders(List<ImapFolder> available)
        {
            List<ImapFolder> folders = new List<ImapFolder>();
            foreach (ImapFolder folder in available)
            {
                if (folder.Selectable && (folder.Junk || MailRules.IsJunkFolderName(Mime.DecodeModifiedUtf7(folder.Name))))
                    folders.Add(folder);
            }
            folders.Sort(delegate(ImapFolder a, ImapFolder b) { return b.Junk.CompareTo(a.Junk); });
            return folders;
        }

        internal static List<ImapFolder> ReceiveFolders(List<ImapFolder> available, bool includeJunk)
        {
            List<ImapFolder> folders = new List<ImapFolder>();
            HashSet<string> added = new HashSet<string>(StringComparer.Ordinal);
            foreach (ImapFolder folder in available)
            {
                string decoded = Mime.DecodeModifiedUtf7(folder.Name);
                if (!folder.Selectable || folder.ExcludedFromReceive || decoded.Equals("INBOX", StringComparison.OrdinalIgnoreCase)
                    || MailRules.IsOutgoingFolderPath(decoded, folder.Delimiter)) continue;
                bool excludedTree = false;
                foreach (ImapFolder parent in available)
                {
                    string parentName = Mime.DecodeModifiedUtf7(parent.Name);
                    bool blocked = parent.ExcludedFromReceive || MailRules.IsOutgoingFolderPath(parentName, parent.Delimiter)
                        || (!includeJunk && (parent.Junk || MailRules.IsJunkFolderName(parentName)));
                    if (blocked && !string.IsNullOrEmpty(parent.Delimiter)
                        && folder.Name.StartsWith(parent.Name + parent.Delimiter, StringComparison.Ordinal))
                    { excludedTree = true; break; }
                }
                if (excludedTree) continue;
                if (!includeJunk && (folder.Junk || MailRules.IsJunkFolderName(decoded))) continue;
                if (added.Add(folder.Name)) folders.Add(folder);
            }
            return folders;
        }

        internal static string MailboxKey(Config identity, string folder)
        {
            return HashKey(identity.EmailServer.Trim().ToLowerInvariant() + "\n" + identity.EmailAccount.Trim().ToLowerInvariant() + "\n" + folder);
        }

        static string HashKey(string text)
        {
            using (SHA256 sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }

        static long InitializeWater(Config identity, Mailbox mb, int gen, bool junk)
        {
            if (mb.UidValidity <= 0 || mb.UidNext <= 0) throw new ImapException("邮箱未提供有效 UID 水位");
            lock (Config.SaveGate)
            {
                if (!CanCommit(gen, junk, identity)) throw new OperationCanceledException();
                long water;
                if (Program.cfg.TryMailboxWater(MailboxKey(identity, mb.Name), mb.UidValidity, out water))
                {
                    History.AcknowledgeMailbox(MailboxKey(identity, mb.Name) + ":" + mb.UidValidity, water);
                    return water;
                }
                water = mb.UidNext - 1;
                SaveMailboxWater(identity, mb, water, junk);
                return water;
            }
        }

        // ---------- 新邮件处理 ----------

        static void ProcessRequestedRecent(Imap imap, int gen, Config identity, Mailbox mailbox)
        {
            int requested = PendingRecentRequest(identity, false);
            if (requested == 0) return;
            ProcessRecent(imap, gen, false, identity, mailbox);
            if (CanCommit(gen, false, identity)) Interlocked.Exchange(ref mainRecentCompleted, requested);
            Program.EmailStatusChanged();
        }

        internal static void ProcessRecent(Imap imap, int gen, bool junk, Config identity, Mailbox mailbox)
        {
            ProcessRecentBefore(imap, gen, junk, identity, mailbox, long.MaxValue);
        }

        static void ProcessRecentBefore(Imap imap, int gen, bool junk, Config identity, Mailbox mailbox, long beforeUid)
        {
            List<long> uids = imap.SearchRecentUids(DateTimeOffset.UtcNow.AddMinutes(-StaleMailMinutes), 200);
            int processed = 0;
            foreach (long uid in uids)
            {
                if (uid >= beforeUid) continue;
                if (!CanCommit(gen, junk, identity)) return;
                ProcessWithRetry(imap, uid, gen, junk, identity, mailbox, true);
                processed++;
            }
            Checked(gen, junk, identity);
            if (uids.Count == 0) MailResult(gen, junk, identity, "最近复查：没有候选邮件");
            lock (Config.SaveGate)
            {
                if (!CanCommit(gen, junk, identity)) return;
                RecentSummary = "最近复查（" + (junk ? "分类目录" : "收件箱") + "）：候选 " + imap.LastRecentCandidateCount
                    + " · 已检查 " + processed + (imap.LastRecentCandidateCount > 200 ? " · 仅取最新 200 封；较早邮件请重新发送" : "");
            }
            Program.EmailStatusChanged();
        }

        /// <summary>处理水位线之后的新邮件；返回推进后的水位线。</summary>
        static long ProcessNew(Imap imap, long water, bool junk, int gen, Config identity, Mailbox mailbox)
        {
            int rounds = 0;
            while (GenAlive(gen, junk) && Program.Running
                   && CanCommit(gen, junk, identity))
            {
                rounds++;
                List<long> uids = imap.SearchUidAbove(water, 200);
                if (uids.Count == 0) break;
                Log.Write((junk ? "email junk: " : "email: ") + uids.Count + " new message(s)");
                foreach (long uid in uids)
                {
                    if (!GenAlive(gen, junk) || !Program.EmailConfigured()) return water;
                    ProcessWithRetry(imap, uid, gen, junk, identity, mailbox, false);
                    // 仅成功处理、明确跳过或已达到隔离阈值时推进；未知异常必须重试。
                    water = uid;
                    if (!GenAlive(gen, junk)) return water;   // 会话已过期：不落盘，交给新会话
                }
                lock (Config.SaveGate)
                {
                    if (!CanCommit(gen, junk, identity)) return water;
                    SaveMailboxWater(identity, mailbox, water, junk);
                }
                Checked(gen, junk, identity);
                if (rounds % 20 == 0) Thread.Yield();
            }
            Checked(gen, junk, identity);
            Program.EmailStatusChanged();
            return water;
        }

        static void ProcessWithRetry(Imap imap, long uid, int gen, bool junk, Config identity, Mailbox mailbox, bool recent)
        {
            string key = MailboxKey(identity, mailbox.Name) + ":" + mailbox.UidValidity + ":" + uid;
            try
            {
                ProcessOne(imap, uid, gen, junk, identity, mailbox, recent);
                lock (Config.SaveGate) contentFailures.Remove(key);
            }
            catch (Exception ex)
            {
                MailContentException content = ex as MailContentException;
                if (content == null)
                {
                    lock (Config.SaveGate)
                        if (CanCommit(gen, junk, identity)) contentFailures.Remove(key);
                    throw;
                }
                string reason = content.ReasonCode;
                lock (Config.SaveGate)
                {
                    if (!CanCommit(gen, junk, identity)) throw new OperationCanceledException();
                    ContentFailure failure;
                    if (!contentFailures.TryGetValue(key, out failure) || failure.Reason != reason)
                    {
                        if (contentFailures.Count >= 256) contentFailures.Clear();
                        contentFailures[key] = failure = new ContentFailure { Reason = reason };
                    }
                    failure.Count++;
                    TraceMail(identity, mailbox, uid, null, reason + "_retry_" + failure.Count);
                    if (failure.Count < 3) throw new MailContentException(reason, "单封邮件结构异常，稍后重试");
                    // 仅相同内容错误连续三次才隔离；不改邮箱、不放宽规则。
                    MailResult(gen, junk, identity, "邮件 UID " + uid + " 连续三次内容异常，已隔离；后续邮件继续处理");
                    TraceMail(identity, mailbox, uid, null, "isolated_" + reason);
                }
            }
        }

        static void TraceMail(Config identity, Mailbox mailbox, long uid, DateTimeOffset? received, string result)
        {
            string folder = MailboxKey(identity, mailbox.Name).Substring(0, 12);
            string age = received.HasValue ? Math.Floor((DateTimeOffset.UtcNow - received.Value).TotalMinutes).ToString(CultureInfo.InvariantCulture) : "unknown";
            Log.Write("email event folder=" + folder + " uidvalidity=" + mailbox.UidValidity + " uid=" + uid + " age_min=" + age + " result=" + result);
        }

        static void SaveMailboxWater(Config identity, Mailbox mailbox, long water, bool junk)
        {
            string oldState = Program.cfg.EmailMailboxState;
            long oldUid = junk ? Program.cfg.EmailLastUidJunk : Program.cfg.EmailLastUid;
            long oldValidity = junk ? Program.cfg.EmailUidValidityJunk : Program.cfg.EmailUidValidity;
            try
            {
                Program.cfg.SetMailboxWater(MailboxKey(identity, mailbox.Name), mailbox.UidValidity, water);
                if (junk) { Program.cfg.EmailLastUidJunk = water; Program.cfg.EmailUidValidityJunk = mailbox.UidValidity; }
                else { Program.cfg.EmailLastUid = water; Program.cfg.EmailUidValidity = mailbox.UidValidity; }
                Program.cfg.Save(Path.Combine(Program.appDir, "config.ini"));
                History.AcknowledgeMailbox(MailboxKey(identity, mailbox.Name) + ":" + mailbox.UidValidity, water);
            }
            catch
            {
                Program.cfg.EmailMailboxState = oldState;
                if (junk) { Program.cfg.EmailLastUidJunk = oldUid; Program.cfg.EmailUidValidityJunk = oldValidity; }
                else { Program.cfg.EmailLastUid = oldUid; Program.cfg.EmailUidValidity = oldValidity; }
                throw new IOException("邮件水位保存失败，稍后重试");
            }
        }

        static void ProcessOne(Imap imap, long uid, int gen, bool junk, Config cfg, Mailbox mailbox, bool recentOnly = false)
        {
            MailHeader receivedHeader = imap.FetchHeaderDetails(uid);
            byte[] rawHeader = receivedHeader.Data;
            if (rawHeader == null)
            {
                if (!receivedHeader.Found)
                {
                MailResult(gen, junk, cfg, "最近邮件：已被移走或删除，跳过");
                TraceMail(cfg, mailbox, uid, receivedHeader.Received, "vanished");
                    return;
                }
                MailResult(gen, junk, cfg, "邮件头读取未完成，正在重试");
                throw new ImapException("邮件头读取未完成，稍后重试");
            }
            string headerText = Mime.Latin1(rawHeader);
            bool fresh = receivedHeader.Received.HasValue
                && receivedHeader.Received.Value <= DateTimeOffset.UtcNow.AddMinutes(5)
                && DateTimeOffset.UtcNow - receivedHeader.Received.Value <= TimeSpan.FromMinutes(StaleMailMinutes);
            if (recentOnly && !fresh) { TraceMail(cfg, mailbox, uid, receivedHeader.Received, "not_recent"); return; }
            if (receivedHeader.Received.HasValue && DateTimeOffset.UtcNow - receivedHeader.Received.Value > TimeSpan.FromMinutes(StaleMailMinutes))
            {
                // 停用一段时间再启用、或长时间离线后的积压：旧验证码早已失效，直接跳过。
                // 大积压时限流写日志（每 5 分钟最多一条），避免刷满 app.log。
                if ((DateTime.Now - lastStaleLogAt).TotalMinutes >= 5)
                {
                    lastStaleLogAt = DateTime.Now;
                    Log.Write("email: skipping mail older than " + StaleMailMinutes + " min");
                }
                TraceMail(cfg, mailbox, uid, receivedHeader.Received, "stale");
                return;
            }
            string subject = Mime.DecodeWords(Mime.HeaderValue(headerText, "Subject"));
            string from = Mime.DecodeWords(Mime.HeaderValue(headerText, "From"));
            if (!MailRules.SubjectMatches(subject, cfg.EmailKeywords))
            {
                // 不匹配的邮件不下载正文、不改变已读状态。
                Log.Write("email: uid " + uid + " skipped (subject)");
                MailResult(gen, junk, cfg, "最近邮件：主题未命中关键词");
                TraceMail(cfg, mailbox, uid, receivedHeader.Received, "subject_skipped");
                return;
            }
            if (!MailRules.SenderAllowed(from, cfg.EmailSenders))
            {
                Log.Write("email: uid " + uid + " skipped (sender)");
                MailResult(gen, junk, cfg, "最近邮件：发件人未命中白名单");
                TraceMail(cfg, mailbox, uid, receivedHeader.Received, "sender_skipped");
                return;
            }

            string code = CodeExtractor.ExtractExplicit(subject, cfg.MinLen, cfg.MaxLen);
            string text = subject;
            bool bodyTruncated = false;
            if (code == null)
            {
                string body = imap.FetchTextForCode(uid, cfg.MinLen, cfg.MaxLen);
                bodyTruncated = imap.LastTextTruncated;
                if (body.Length > 0)
                {
                    text = subject + "\r\n" + body;
                    code = CodeExtractor.ExtractExplicit(body, cfg.MinLen, cfg.MaxLen) ?? CodeExtractor.Extract(body, cfg.MinLen, cfg.MaxLen);
                }
            }
            if (code == null)
            {
                Log.Write("email: uid " + uid + " no code found");
                MailResult(gen, junk, cfg, "最近邮件：主题已命中，正文未找到符合长度的验证码");
                TraceMail(cfg, mailbox, uid, receivedHeader.Received, "no_code");
                return;
            }
            Log.Write("email: code recognized");
            if (bodyTruncated) text += "\n（邮件正文超出读取上限，以下内容未保存）";
            string id = Mime.HeaderValue(headerText, "Message-ID");
            string key = HashKey(MailboxKey(cfg, "") + "\n" + (string.IsNullOrWhiteSpace(id)
                ? mailbox.Name + "\n" + mailbox.UidValidity + "\n" + uid : id.Trim()));
            Program.SubmitReceivedEmail(text, from, code, key, cfg, fresh && !recentOnly, delegate { return CanCommit(gen, junk, cfg); },
                recentOnly ? null : MailboxKey(cfg, mailbox.Name) + ":" + mailbox.UidValidity, recentOnly ? 0 : uid, !recentOnly,
                receivedHeader.Received.HasValue ? receivedHeader.Received.Value.UtcDateTime : DateTime.UtcNow);
            MailResult(gen, junk, cfg, "最近邮件：已识别验证码（相同邮件不重复收录）");
            TraceMail(cfg, mailbox, uid, receivedHeader.Received, recentOnly ? "recognized_history_only" : "recognized");
        }
    }

    // ---------- 邮件规则（纯函数，便于测试）----------

    static class MailRules
    {
        public static bool NormalizeLines(string value, out string normalized, out string error)
        {
            List<string> values = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            normalized = ""; error = null;
            foreach (string raw in SplitLines(value))
            {
                string line = raw.Trim();
                if (line.Length == 0 || !seen.Add(line)) continue;
                if (line.Length > 256) { error = "每条最多 256 字符"; return false; }
                if (values.Count >= 64) { error = "最多 64 条"; return false; }
                foreach (char c in line)
                    if (char.IsControl(c)) { error = "规则不能包含控制字符"; return false; }
                values.Add(line);
            }
            normalized = string.Join("\n", values.ToArray());
            if (normalized.Length > 8192) { error = "规则合计最多 8192 字符"; return false; }
            return true;
        }

        public static bool SubjectMatches(string subject, string keywordLines)
        {
            if (string.IsNullOrEmpty(subject)) return false;
            string normalized, error;
            if (!NormalizeLines(keywordLines, out normalized, out error)) return false;
            string[] keywords = SplitLines(normalized);
            int count = Math.Min(keywords.Length, 64);
            for (int i = 0; i < count; i++)
            {
                string kw = keywords[i].Trim();
                if (kw.Length > 0 && kw.Length <= 256
                    && subject.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        public static bool SenderAllowed(string from, string whitelistLines)
        {
            string normalized, error;
            if (!NormalizeSenders(whitelistLines, out normalized, out error)) return false;
            bool hasWhitelist = false;
            string[] rules = SplitLines(normalized);
            int count = Math.Min(rules.Length, 64);
            for (int i = 0; i < count; i++)
                if (rules[i].Trim().Length > 0) { hasWhitelist = true; break; }
            if (!hasWhitelist) return true;
            string address = SenderAddress(from);
            if (address == null) return false;
            for (int i = 0; i < count; i++)
            {
                string rule = rules[i].Trim();
                if (rule.Length > 0 && rule.Length <= 256
                    && SenderRuleMatches(address, rule))
                    return true;
            }
            return false;
        }

        public static string SenderAddress(string from)
        {
            if (string.IsNullOrWhiteSpace(from) || from.Length > 4096) return null;
            try
            {
                MailAddress parsed = new MailAddress(from);
                string address = parsed.Address;
                return address.IndexOf('@') > 0 ? address.ToLowerInvariant() : null;
            }
            catch (FormatException) { return null; }
        }

        static bool SenderRuleMatches(string address, string rule)
        {
            if (rule.IndexOf('@') > 0)
                return string.Equals(address, rule, StringComparison.OrdinalIgnoreCase);
            string domain = rule.TrimStart('@').ToLowerInvariant();
            if (domain.IndexOf('.') <= 0 || domain.Contains(" ") || domain.Contains("<") || domain.Contains(">")) return false;
            string actualDomain = address.Substring(address.LastIndexOf('@') + 1);
            return string.Equals(actualDomain, domain, StringComparison.OrdinalIgnoreCase);
        }

        public static bool NormalizeSenders(string value, out string normalized, out string error)
        {
            if (!NormalizeLines(value, out normalized, out error)) return false;
            foreach (string rule in normalized.Split('\n'))
            {
                if (rule.Length == 0) continue;
                if (rule.IndexOf('@') > 0)
                {
                    string address = SenderAddress(rule);
                    if (address == null || !string.Equals(address, rule, StringComparison.OrdinalIgnoreCase))
                    { error = "请填写完整邮箱地址，不能使用显示名称或片段"; return false; }
                }
                else
                {
                    string domain = rule.TrimStart('@');
                    if (domain.IndexOf('.') <= 0 || domain.EndsWith(".") || Uri.CheckHostName(domain) != UriHostNameType.Dns)
                    { error = "请填写完整域名，例如 example.com；不支持片段匹配"; return false; }
                }
            }
            return true;
        }

        public static bool IsJunkFolderName(string decodedName)
        {
            if (string.IsNullOrEmpty(decodedName)) return false;
            string[] markers = { "垃圾", "广告", "junk", "spam" };
            foreach (string marker in markers)
                if (decodedName.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        public static bool IsOutgoingFolderName(string decodedName)
        {
            return IsOutgoingFolderPath(decodedName, "/");
        }

        public static bool IsOutgoingFolderPath(string decodedName, string delimiter)
        {
            string name = (decodedName ?? "").Trim();
            string[] excluded = { "Sent", "Sent Messages", "Sent Mail", "Draft", "Drafts", "Trash", "Deleted",
                "Deleted Items", "Deleted Messages", "All Mail", "已发送", "已发送邮件", "发件箱", "草稿", "草稿箱",
                "已删除", "已删除邮件", "回收站", "全部邮件" };
            string[] segments = string.IsNullOrEmpty(delimiter) ? new string[] { name }
                : name.Split(new string[] { delimiter }, StringSplitOptions.None);
            foreach (string segment in segments)
                foreach (string value in excluded)
                    if (segment.Trim().Equals(value, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static string[] SplitLines(string value)
        {
            return (value ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }
    }
}
