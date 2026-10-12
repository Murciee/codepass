using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace Codepass
{
    sealed class NtfyReceiver
    {
        readonly Func<Config> configuration;
        readonly Func<string> directory;
        readonly IncomingDelivery delivery;
        readonly Action statusChanged;
        readonly object gate = new object();
        readonly ManualResetEvent stopped = new ManualResetEvent(false);
        Thread thread;
        HttpWebRequest request;
        volatile bool running;

        internal NtfyReceiver(Func<Config> configuration, Func<string> directory, IncomingDelivery delivery, Action statusChanged)
        { this.configuration = configuration; this.directory = directory; this.delivery = delivery; this.statusChanged = statusChanged; }

        internal void Start()
        {
            lock (gate)
            {
                if (thread != null && thread.IsAlive) return;
                running = true; stopped.Reset();
                thread = new Thread(Loop); thread.IsBackground = true; thread.Name = "ntfy"; thread.Start();
            }
        }

        internal void Stop()
        {
            Thread worker;
            lock (gate)
            {
                running = false; stopped.Set(); worker = thread;
                try { if (request != null) request.Abort(); } catch { }
            }
            try { if (worker != null && worker.IsAlive && worker != Thread.CurrentThread) worker.Join(1500); } catch { }
        }

        internal static bool IsHttpsEndpoint(string value)
        {
            Uri uri;
            return Uri.TryCreate(value ?? "", UriKind.Absolute, out uri)
                && uri.Scheme == Uri.UriSchemeHttps && uri.Host.Length > 0 && uri.UserInfo.Length == 0;
        }

        void Loop()
        {
            Config identity = new Config(); identity.CopyFrom(configuration());
            NtfyCheckpoint checkpoint = new NtfyCheckpoint(Path.Combine(directory(), "ntfy-state.txt"), identity.NtfyServer, identity.NtfyTopic);
            while (running)
            {
                try
                {
                    if (!IsHttpsEndpoint(identity.NtfyServer)) { Log.Write("ntfy stopped: server is not HTTPS"); return; }
                    string url = identity.NtfyServer.TrimEnd('/') + "/" + Uri.EscapeDataString(identity.NtfyTopic) + "/json?since=" + checkpoint.Since(DateTime.UtcNow);
                    RuntimeStatus.Ntfy = ConnectionState.Connecting; statusChanged();
                    Log.Write("ntfy subscribing");
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                    req.AllowAutoRedirect = false; req.Timeout = 30000; req.ReadWriteTimeout = 120000;
                    req.UserAgent = "codepass/1.0";
                    if (identity.NtfyToken.Length > 0) req.Headers["Authorization"] = "Bearer " + identity.NtfyToken;
                    lock (gate) { if (!running) return; request = req; }
                    using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                    using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    {
                        if ((int)resp.StatusCode != 200) throw new IOException("ntfy response is not HTTP 200");
                        RuntimeStatus.Ntfy = ConnectionState.Ready; statusChanged();
                        string line;
                        while (running && (line = sr.ReadLine()) != null)
                        {
                            if (!running) break;
                            if (line.Length > 0) ProcessNtfyLine(line, checkpoint);
                        }
                    }
                }
                catch (Exception e)
                { if (running) { Log.Write("ntfy error: " + e.GetType().Name); RuntimeStatus.Ntfy = ConnectionState.Retrying; statusChanged(); } }
                finally { lock (gate) request = null; }
                if (running) stopped.WaitOne(5000);
            }
        }

        internal DeliveryOutcome ProcessNtfyLine(string line, NtfyCheckpoint checkpoint)
        {
            NtfyMessage message;
            try { message = MessageIdentity.ReadJson<NtfyMessage>(line); }
            catch (System.Runtime.Serialization.SerializationException) { Log.Write("ntfy malformed event skipped"); return DeliveryOutcome.Ignored; }
            catch (FormatException) { Log.Write("ntfy malformed event skipped"); return DeliveryOutcome.Ignored; }
            if (message == null || message.Event != "message") return DeliveryOutcome.Ignored;
            if (string.IsNullOrEmpty(message.Id) || message.Id.Length > 256 || message.Time <= 0) return DeliveryOutcome.Ignored;
            if (message.Time > MessageIdentity.UnixTime(DateTime.UtcNow) + 300) return DeliveryOutcome.Ignored;
            if (checkpoint.Contains(message.Id)) return DeliveryOutcome.Duplicate;
            DeliveryOutcome result = DeliveryOutcome.Ignored;
            try
            {
                if (!string.IsNullOrWhiteSpace(message.Message))
                    result = delivery.ReceiveSms(message.Message, "ntfy", message.Title, "", MessageIdentity.FromUnix(message.Time), true, true,
                        "ntfy:" + MessageIdentity.Hash(message.Id));
            }
            catch (FormatException) { Log.Write("ntfy malformed payload skipped"); }
            catch (System.Runtime.Serialization.SerializationException) { Log.Write("ntfy malformed payload skipped"); }
            checkpoint.Acknowledge(message.Time, message.Id);
            return result;
        }
    }
}
