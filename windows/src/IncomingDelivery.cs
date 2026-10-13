using System;
using System.IO;

namespace Codepass
{
    // 接收通道只提交消息；此处统一规则快照、持久化、去重结果和延迟投递门禁。
    sealed class IncomingDelivery
    {
        readonly Func<Config> configuration;
        readonly Func<bool> running;
        readonly Action<HistoryItem> setLatest;
        readonly Action applyRetention, statusChanged;
        readonly Action<HistoryItem, string, bool, bool, Func<bool>> present;

        internal IncomingDelivery(Func<Config> configuration, Func<bool> running, Action<HistoryItem> setLatest,
            Action applyRetention, Action statusChanged, Action<HistoryItem, string, bool, bool, Func<bool>> present)
        {
            this.configuration = configuration; this.running = running; this.setLatest = setLatest;
            this.applyRetention = applyRetention; this.statusChanged = statusChanged; this.present = present;
        }

        internal DeliveryOutcome HandleIncoming(string text, string src, string sender,
            bool autoCopy, bool notify, bool emailChannel)
        {
            if (!emailChannel) return ReceiveSms(text, src, sender, "", DateTime.UtcNow, autoCopy, notify);
            Config snapshot = new Config(); snapshot.CopyFrom(configuration());
            string code = CodeExtractor.Extract(text, snapshot.MinLen, snapshot.MaxLen);
            return code == null ? DeliveryOutcome.Ignored
                : DeliverIncoming(text, src, sender, autoCopy, notify, true, code, "", snapshot, null);
        }

        internal void SubmitEmail(string text, string sender, string code, string messageKey, Config identity,
            bool allowAutoCopy, Func<bool> canCommit, string mailboxKey = null, long mailUid = 0, bool updateLatest = true)
        {
            DeliverIncoming(text, "email", sender, identity.EmailAutoCopy && allowAutoCopy, identity.EmailNotify,
                true, code, messageKey, identity, canCommit, mailboxKey, mailUid, updateLatest);
        }

        internal void SubmitReceivedEmail(string text, string sender, string code, string messageKey, Config identity,
            bool allowAutoCopy, Func<bool> canCommit, string mailboxKey, long mailUid, bool updateLatest, DateTime receivedUtc)
        {
            DeliverIncoming(text, "email", sender, identity.EmailAutoCopy && allowAutoCopy, identity.EmailNotify,
                true, code, messageKey, identity, canCommit, mailboxKey, mailUid, updateLatest, receivedUtc);
        }

        internal DeliveryOutcome ReceiveSms(string text, string src, string sender, string id,
            DateTime receivedUtc, bool autoCopy, bool notify, string transportKey = null)
        {
            if (!running()) throw new IOException("接收正在停止，请稍后重试投递");
            MessageIdentity.Unwrap(ref text, ref sender, ref id, ref receivedUtc);
            if (receivedUtc < DateTime.UtcNow.AddMinutes(-MessageIdentity.SmsMaxAgeMinutes)
                || receivedUtc > DateTime.UtcNow.AddMinutes(5))
            { RuntimeStatus.SmsResult = "已忽略过期或时间异常的短信"; statusChanged(); return DeliveryOutcome.Ignored; }
            for (int attempt = 0; attempt < 4; attempt++)
            {
                Config snapshot = new Config(); snapshot.CopyFrom(configuration());
                string ignored = SmsExceedsLengthLimit(text, snapshot.SmsMaxLength) ? "SMS skipped: length limit exceeded"
                    : CodeExtractor.IsFiltered(text, snapshot.FilterKeywords, snapshot.FilterRegex) ? "message filtered"
                    : !CodeExtractor.MatchesAcceptKeywords(text, snapshot.SmsAcceptKeywords) ? "SMS skipped: acceptance keywords not matched" : null;
                string code = ignored == null ? CodeExtractor.Extract(text, snapshot.MinLen, snapshot.MaxLen) : null;
                lock (Config.SaveGate)
                {
                    if (!running()) throw new IOException("接收正在停止，请稍后重试投递");
                    if (!SameSmsRules(snapshot, configuration())) continue;
                    if (ignored != null || code == null)
                    {
                        Log.Write("[" + src + "] " + (ignored ?? "未识别到验证码"));
                        RuntimeStatus.SmsResult = ignored == "message filtered" ? "消息命中过滤规则"
                            : ignored == "SMS skipped: length limit exceeded" ? "短信超过长度上限"
                            : ignored != null ? "未命中接收关键字" : "消息中未识别到验证码";
                        statusChanged(); return DeliveryOutcome.Ignored;
                    }
                    DeliveryOutcome result = DeliverIncoming(text, src, sender, autoCopy, notify, false, code,
                        MessageIdentity.SmsKey(text, id), snapshot, null, null, 0, true, receivedUtc, transportKey);
                    RuntimeStatus.SmsResult = result == DeliveryOutcome.Duplicate ? "重复短信已合并" : "已收到验证码";
                    RuntimeStatus.LastSmsUtc = DateTime.UtcNow;
                    statusChanged(); return result;
                }
            }
            throw new IOException("短信设置正在变化，请重试投递");
        }

        static bool SameSmsRules(Config a, Config b)
        {
            return a.SmsMaxLength == b.SmsMaxLength && a.MinLen == b.MinLen && a.MaxLen == b.MaxLen
                && a.FilterKeywords == b.FilterKeywords && a.FilterRegex == b.FilterRegex
                && a.SmsAcceptKeywords == b.SmsAcceptKeywords;
        }

        // 合法代理对算一个字符，空格与换行也计入。
        internal static bool SmsExceedsLengthLimit(string text, int limit)
        {
            if (text == null) return false;
            if (limit < 1 || limit > Config.MaximumSmsMaxLength) limit = Config.DefaultSmsMaxLength;
            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) i++;
                if (++count > limit) return true;
            }
            return false;
        }

        DeliveryOutcome DeliverIncoming(string text, string src, string sender, bool autoCopy, bool notify, bool emailChannel,
            string code, string messageKey, Config identity, Func<bool> canCommit, string mailboxKey = null, long mailUid = 0,
            bool updateLatest = true, DateTime? receivedUtc = null, string transportKey = null)
        {
            lock (Config.SaveGate)
            {
                if (!running()) throw new IOException("接收正在停止，请稍后重试投递");
                if (canCommit != null && !canCommit()) return DeliveryOutcome.Ignored;
                Log.Write("[" + src + "] code recognized");
                if (configuration().Dry) { Log.Write("[dry] skip clipboard"); return DeliveryOutcome.Ignored; }
                HistoryItem it = new HistoryItem();
                DateTime received = receivedUtc.HasValue ? receivedUtc.Value.ToUniversalTime() : DateTime.UtcNow;
                if (received > DateTime.UtcNow) received = DateTime.UtcNow;
                it.ReceivedUtcTicks = received.Ticks;
                it.Time = received.ToLocalTime().ToString("MM-dd HH:mm:ss");
                it.Source = emailChannel ? (MailRules.SenderAddress(sender) ?? "未知发件人") : History.ExtractSender(text, sender);
                it.MessageKey = messageKey; it.TransportKey = transportKey;
                it.MailboxKey = mailboxKey; it.MailUid = mailUid;
                it.CanBeLatest = updateLatest; it.Code = code; it.Channel = emailChannel ? "email" : "sms";
                string historyText = text ?? "";
                if (emailChannel && !identity.EmailSaveBody) historyText = "";
                else if (emailChannel && historyText.Length > 24000) historyText = historyText.Substring(0, 24000) + "\n（内容过长，后续未保存）";
                it.Raw = historyText;
                if (!History.AddUnique(it)) return DeliveryOutcome.Duplicate;
                if (updateLatest) setLatest(it);
                applyRetention();
                if (History.IsExpired(it, configuration().HistoryRetentionMinutes, DateTime.UtcNow)) return DeliveryOutcome.Received;
                Func<bool> canDeliver = delegate
                {
                    lock (Config.SaveGate) return running() && !it.IsDeleted
                        && !History.IsExpired(it, configuration().HistoryRetentionMinutes, DateTime.UtcNow)
                        && (canCommit == null || canCommit());
                };
                present(it, text, autoCopy, notify, canDeliver);
                return DeliveryOutcome.Received;
            }
        }
    }
}
