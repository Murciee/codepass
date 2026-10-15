using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Codepass
{
    // TcpListener 手工解析，免管理员/urlacl；监听器和活动连接均由本模块启停。
    // 绑定失败只退避重试，不作废整个会话：登录自启时虚拟网卡地址（pc_ip）常常晚于本进程就绪。
    sealed class HttpReceiver
    {
        readonly Func<Config> configuration;
        readonly IncomingDelivery delivery;
        readonly Action statusChanged;
        readonly object gate = new object();
        readonly HashSet<TcpClient> clients = new HashSet<TcpClient>();
        readonly ManualResetEvent stopSignal = new ManualResetEvent(false);
        TcpListener listener;
        Thread thread;
        volatile bool running, lanBound;
        // 最近一次成功绑定的端点文本：界面要能看出实际监听在哪（例如地址非法时被降级到本机）。
        internal volatile string BoundEndpoint;
        const int MaxClients = 32;
        static readonly byte[] CrlfCrlf = new byte[] { 13, 10, 13, 10 };
        static readonly byte[] LfLf = new byte[] { 10, 10 };
        // 前几次快（虚拟网卡地址通常几秒内就绪），之后拉长到 30 秒一次，避免长期失败时刷满日志。
        static readonly int[] RetryDelays = new int[] { 1000, 2000, 3000, 5000, 8000, 13000, 20000, 30000 };

        internal HttpReceiver(Func<Config> configuration, IncomingDelivery delivery, Action statusChanged)
        { this.configuration = configuration; this.delivery = delivery; this.statusChanged = statusChanged; }

        internal void Start()
        {
            lock (gate)
            {
                if (thread != null && thread.IsAlive) return;
                stopSignal.Reset(); running = true;
                thread = new Thread(Loop); thread.IsBackground = true; thread.Name = "http"; thread.Start();
            }
        }

        internal void Stop()
        {
            Thread worker;
            lock (gate)
            {
                running = false; worker = thread; BoundEndpoint = "";
                stopSignal.Set(); // 让正在等待下一次重试的线程立刻醒来，退出不用等满一个间隔。
                try { if (listener != null) listener.Stop(); } catch { }
                foreach (TcpClient client in clients) { try { client.Close(); } catch { } }
            }
            try { if (worker != null && worker.IsAlive && worker != Thread.CurrentThread) worker.Join(1500); } catch { }
        }

        /// <summary>第 attempt 次失败后到下一次尝试的等待时间（attempt 从 0 起）。</summary>
        internal static int RetryDelayMs(int attempt)
        {
            return RetryDelays[attempt < 1 ? 0 : Math.Min(attempt, RetryDelays.Length - 1)];
        }

        void Loop()
        {
            RuntimeStatus.Http = ConnectionState.Connecting; RaiseStatus();
            int failures = 0;
            while (running)
            {
                TcpListener active = null;
                try
                {
                    // 每次尝试都重新解析地址：配置里的网卡地址后出现时，下一次重试就能绑上。
                    Config cfg = configuration();
                    IPAddress address = ResolveListenAddress(cfg);
                    lock (gate)
                    {
                        if (!running) return;
                        active = new TcpListener(address, cfg.Port); listener = active; active.Start();
                    }
                    lanBound = !IPAddress.IsLoopback(address);
                    BoundEndpoint = address + ":" + cfg.Port;
                    Log.Write("HTTP listening on " + address + ":" + cfg.Port + " token=" + (cfg.Token.Length == 0 ? "off" : "on")
                        + (failures > 0 ? " (bind retried " + failures + "x)" : ""));
                    failures = 0;
                    RuntimeStatus.Http = ConnectionState.Ready; RaiseStatus();
                    Accept(active);
                }
                catch (Exception e)
                {
                    if (!running) return;
                    RuntimeStatus.Http = ConnectionState.Retrying; RaiseStatus();
                    // 首次必写，长期失败每 10 次记一条，避免无休止刷日志。
                    if (failures == 0 || failures % 10 == 0)
                        Log.Write("HTTP listen FAILED: " + e.Message + "  (监听地址不可用或端口被占用? 将自动重试)");
                }
                finally
                {
                    try { if (active != null) active.Stop(); } catch { }
                    lock (gate) { if (listener == active) listener = null; }
                }
                if (!running) break;
                if (stopSignal.WaitOne(RetryDelayMs(failures))) break; // 只有停止信号才结束重试。
                failures++;
            }
        }

        void Accept(TcpListener active)
        {
            while (running)
            {
                try
                {
                    TcpClient client = active.AcceptTcpClient();
                    lock (gate)
                    {
                        if (!running || clients.Count >= MaxClients) { client.Close(); continue; }
                        clients.Add(client);
                    }
                    ThreadPool.QueueUserWorkItem(HandleClient, client);
                }
                catch (Exception e) { if (running) { Log.Write("accept error: " + e.Message); stopSignal.WaitOne(300); } }
            }
        }

        /// <summary>状态回调属于界面提示：回调自身出错不能被当成绑定失败而触发重试。</summary>
        void RaiseStatus()
        { try { if (statusChanged != null) statusChanged(); } catch { } }

        static IPAddress ResolveListenAddress(Config cfg)
        {
            IPAddress configured;
            if (cfg.Token.Length == 0) return IPAddress.Loopback;
            if (String.IsNullOrWhiteSpace(cfg.PcIp)) return IPAddress.Any;
            if (!IPAddress.TryParse(cfg.PcIp.Trim(), out configured) || configured.AddressFamily != AddressFamily.InterNetwork
                || IPAddress.IsLoopback(configured) || configured.Equals(IPAddress.Any))
            { Log.Write("HTTP security: invalid LAN address; binding 127.0.0.1 instead"); return IPAddress.Loopback; }
            return configured;
        }

        void HandleClient(object state)
        {
            TcpClient client = (TcpClient)state;
            try
            {
                if (!running) return;
                client.ReceiveTimeout = 8000; client.SendTimeout = 8000; client.NoDelay = true;
                using (NetworkStream ns = client.GetStream())
                {
                    string head, body;
                    if (!ReadHttp(ns, out head, out body) || !running) return;
                    string requiredToken = configuration().Token;
                    if (lanBound && requiredToken.Length == 0)
                    {
                        Log.Write("[http] 拒绝: 局域网监听未配置 token");
                        WriteResponse(ns, "403 Forbidden", ""); return;
                    }
                    if (requiredToken.Length > 0 && !TokenOk(head, requiredToken))
                    {
                        Log.Write("[http] 拒绝: token 不匹配");
                        WriteResponse(ns, "403 Forbidden", "{\"ok\":false,\"err\":\"token\"}"); return;
                    }
                    if (IsHealthRequest(head)) { WriteResponse(ns, "200 OK", "{\"ok\":true}"); return; }
                    string[] parts = head.Split('\n')[0].Trim().Split(' ');
                    if (parts.Length != 3 || parts[0] != "POST" || parts[1].Split('?')[0] != "/sms")
                    { WriteResponse(ns, "404 Not Found", ""); return; }
                    if (!string.IsNullOrWhiteSpace(body))
                        delivery.ReceiveSms(body, "http", HeaderValueB64(head, "X-SMS-Sender-B64", HeaderValue(head, "X-SMS-Sender")),
                            HeaderValue(head, "X-SMS-Id"), DateTime.UtcNow, true, true);
                    if (running) WriteResponse(ns, "200 OK", "{\"ok\":true}");
                }
            }
            catch (Exception e) { if (running) Log.Write("client error: " + e.Message); }
            finally { try { client.Close(); } catch { } lock (gate) clients.Remove(client); }
        }

        static void WriteResponse(NetworkStream stream, string status, string body)
        {
            byte[] payload = Encoding.UTF8.GetBytes(body);
            byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: "
                + payload.Length + "\r\nConnection: close\r\n\r\n");
            stream.Write(header, 0, header.Length); stream.Write(payload, 0, payload.Length); stream.Flush();
        }

        static bool TokenOk(string head, string requiredToken)
        {
            if (head == null) return false;
            string[] lines = head.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string s = lines[i].Trim(); int c = s.IndexOf(':');
                if (c > 0 && s.Substring(0, c).Trim().Equals("X-Token", StringComparison.OrdinalIgnoreCase)
                    && s.Substring(c + 1).Trim() == requiredToken) return true;
            }
            if (lines.Length > 0)
            {
                string first = lines[0]; int q = first.IndexOf('?');
                if (q >= 0)
                {
                    int sp = first.IndexOf(' ', q);
                    string query = sp > q ? first.Substring(q + 1, sp - q - 1) : first.Substring(q + 1);
                    foreach (string pair in query.Split('&'))
                    {
                        int eq = pair.IndexOf('=');
                        if (eq > 0 && pair.Substring(0, eq) == "token")
                        {
                            string supplied;
                            try { supplied = Uri.UnescapeDataString(pair.Substring(eq + 1)); } catch { continue; }
                            if (supplied == requiredToken) return true;
                        }
                    }
                }
            }
            return false;
        }

        static bool IsHealthRequest(string head)
        {
            if (head == null) return false;
            string[] parts = head.Split('\n')[0].Trim().Split(' ');
            return parts.Length == 3 && parts[0] == "GET" && parts[1].Split('?')[0] == "/health";
        }

        static bool ReadHttp(NetworkStream ns, out string head, out string body)
        {
            head = null; body = "";
            using (MemoryStream ms = new MemoryStream())
            {
                byte[] buf = new byte[4096]; int headerEnd = -1, cl = -1, sep = 4;
                while (true)
                {
                    int n = ns.Read(buf, 0, buf.Length);
                    if (n <= 0) break;
                    ms.Write(buf, 0, n);
                    byte[] arr = ms.GetBuffer(); int len = (int)ms.Length;
                    if (headerEnd < 0)
                    {
                        headerEnd = IndexOfSeq(arr, len, CrlfCrlf); sep = 4;
                        if (headerEnd < 0) { headerEnd = IndexOfSeq(arr, len, LfLf); sep = 2; }
                        if (headerEnd >= 0)
                        {
                            head = Encoding.ASCII.GetString(arr, 0, headerEnd); cl = ParseContentLength(head);
                            if (cl < 0 && RequestHasNoBody(head)) cl = 0;
                            if (cl < 0 || cl > 1048576 || HeaderValue(head, "Transfer-Encoding").Length > 0) return false;
                        }
                    }
                    if (headerEnd >= 0 && cl >= 0 && len >= headerEnd + sep + cl) break;
                    if (len > 1048576) break;
                }
                if (headerEnd < 0) return false;
                int bodyStart = headerEnd + sep, bodyLen = (int)ms.Length - bodyStart;
                if (cl < 0 || bodyLen < cl) return false;
                body = Encoding.UTF8.GetString(ms.GetBuffer(), bodyStart, cl); return true;
            }
        }

        static int ParseContentLength(string head)
        {
            foreach (string line in head.Split('\n'))
            {
                string s = line.Trim(); int c = s.IndexOf(':');
                if (c > 0 && s.Substring(0, c).Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                { int value; if (int.TryParse(s.Substring(c + 1).Trim(), out value)) return value; }
            }
            return -1;
        }
        static string HeaderValue(string head, string name)
        {
            if (head == null || name == null) return "";
            foreach (string line in head.Split('\n'))
            {
                string s = line.Trim(); int c = s.IndexOf(':');
                if (c > 0 && s.Substring(0, c).Trim().Equals(name, StringComparison.OrdinalIgnoreCase)) return s.Substring(c + 1).Trim();
            }
            return "";
        }
        static string HeaderValueB64(string head, string name, string fallback)
        {
            string value = HeaderValue(head, name);
            if (value.Length == 0) return fallback ?? "";
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); } catch { return fallback ?? ""; }
        }
        static bool RequestHasNoBody(string head)
        {
            if (head == null) return false;
            string first = head.Split('\n')[0].Trim();
            return first.StartsWith("GET ", StringComparison.OrdinalIgnoreCase) || first.StartsWith("HEAD ", StringComparison.OrdinalIgnoreCase);
        }
        static int IndexOfSeq(byte[] hay, int len, byte[] needle)
        {
            for (int i = 0; i + needle.Length <= len; i++)
            {
                bool ok = true;
                for (int j = 0; j < needle.Length; j++) if (hay[i + j] != needle[j]) { ok = false; break; }
                if (ok) return i;
            }
            return -1;
        }
    }
}
