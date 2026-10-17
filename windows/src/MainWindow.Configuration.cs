using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Automation;

namespace Codepass
{
    sealed partial class MainWindow
    {
        string activeSection = "history";
        readonly Dictionary<FrameworkElement, TextBlock> fieldErrors = new Dictionary<FrameworkElement, TextBlock>();
        string initialNetworkSignature;
        readonly SettingsCommitQueue settingsQueue = new SettingsCommitQueue();
        readonly Dictionary<SettingsSection, int> saveVersions = new Dictionary<SettingsSection, int>();
        readonly Dictionary<SettingsSection, string> sectionErrors = new Dictionary<SettingsSection, string>();
        int saveVersion;
        readonly object settingsResultGate = new object();
        readonly Queue<SettingsSaveResult> settingsResults = new Queue<SettingsSaveResult>();
        int settingsSaveFailureCount;
        sealed class SettingsSaveResult
        {
            internal SettingsSection Section;
            internal int Version;
            internal bool Restart;
            internal Exception Error;
        }
        static readonly SettingsSection[] AtomicSections = { SettingsSection.Sms, SettingsSection.SmsRules, SettingsSection.Ntfy,
            SettingsSection.Email, SettingsSection.EmailRules, SettingsSection.Privacy, SettingsSection.Retention, SettingsSection.Window };
        int AdvanceSettingsVersion(SettingsSection section)
        {
            int version = ++saveVersion;
            foreach (SettingsSection part in AtomicSections) if (Includes(section, part)) saveVersions[part] = version;
            return version;
        }
        bool IsCurrentSave(SettingsSaveResult result)
        {
            foreach (SettingsSection part in AtomicSections)
            {
                int version;
                if (Includes(result.Section, part) && (!saveVersions.TryGetValue(part, out version) || version != result.Version)) return false;
            }
            return true;
        }

        static bool Includes(SettingsSection section, SettingsSection flag) { return (section & flag) != 0; }
        bool Invalid(FrameworkElement field, string message)
        {
            TextBlock error;
            if (field != null && fieldErrors.TryGetValue(field, out error)) { error.Text = message; error.Visibility = Visibility.Visible; }
            if (field != null) AutomationProperties.SetHelpText(field, message);
            if (field != null) sectionErrors[SectionFor(field)] = message;
            SettingsFeedback("未保存：" + message + "。输入已保留。", true);
            return false;
        }
        void SettingsFeedback(string message, bool error)
        {
            if (!error && emailPage != null && emailPage.ConnectionDirty) message += " · 邮箱连接草稿未保存";
            if (!error && sectionErrors.Count > 0)
            {
                foreach (string problem in sectionErrors.Values) { message = "仍有未保存设置：" + problem; break; }
                error = true;
            }
            TextBlock label = Find<TextBlock>((FrameworkElement)Content, "lblSettingsFeedback");
            if (label != null) { label.Text = message; label.Foreground = error ? Brushes.Firebrick : FgFaint; }
        }
        bool ReadNumber(TextBox box, string label, string key, out int value)
        {
            SettingDefinition definition = ConfigSettings.Get(key);
            return definition.TryNumber(box.Text, out value) || Invalid(box, definition.NumberError(label));
        }
        bool ReadDraft(SettingsSection section, out Config draft)
        {
            // 草稿只读取本区控件；不在 UI 线程等待后台持有的保存锁。
            draft = new Config();
            foreach (KeyValuePair<FrameworkElement, TextBlock> field in fieldErrors)
                if (Includes(section, SectionFor(field.Key))) { field.Value.Visibility = Visibility.Collapsed; AutomationProperties.SetHelpText(field.Key, ""); }
            if (Includes(section, SettingsSection.Sms))
            {
                if (!ReadNumber(txtPort, "监听端口", "port", out draft.Port)) return false;
                draft.PcIp = txtPcIp.Text.Trim();
                if (cfg.PcIp.Length == 0 && draft.PcIp == autoPcIp) draft.PcIp = "";
                if (draft.PcIp.Length > 0 && !IsUsablePcIp(draft.PcIp)) return Invalid(txtPcIp, "请输入有效的电脑 IPv4 地址");
                draft.Token = fldToken.Text.Trim(); draft.StartWithWindows = chkStartup.IsChecked == true;
            }
            if (Includes(section, SettingsSection.SmsRules))
            {
                if (!ReadNumber(txtMin, "最短验证码长度", "min_len", out draft.MinLen)
                    || !ReadNumber(txtMax, "最长验证码长度", "max_len", out draft.MaxLen)
                    || !ReadNumber(txtSmsMaxLength, "短信长度上限", "sms_max_length", out draft.SmsMaxLength)
                    || !ReadNumber(txtClear, "剪贴板清理秒数", "auto_clear_seconds", out draft.AutoClearSeconds)) return false;
                if (draft.MaxLen < draft.MinLen) return Invalid(txtMax, "最长验证码长度不能小于最短长度");
                draft.FilterKeywords = txtFilterKeywords.Text; draft.FilterRegex = txtFilterRegex.Text;
                draft.SmsAcceptKeywords = txtAcceptKeywords.Text;
                string error;
                if (!CodeExtractor.ValidateFilters(draft.FilterKeywords, draft.FilterRegex, out error)) return Invalid(txtFilterRegex, error);
                if (!CodeExtractor.ValidateKeywordLines(draft.SmsAcceptKeywords, "接收关键字", out error)) return Invalid(txtAcceptKeywords, error);
                draft.ShowTip = chkTip.IsChecked == true;
            }
            if (Includes(section, SettingsSection.Ntfy))
            {
                draft.NtfyServer = txtNtfyServer.Text.Trim().TrimEnd('/'); draft.NtfyTopic = txtNtfyTopic.Text.Trim(); draft.NtfyToken = fldNtfyToken.Text.Trim();
                Uri uri;
                if ((draft.NtfyTopic.Length > 0 || draft.NtfyServer.Length > 0)
                    && (!Uri.TryCreate(draft.NtfyServer, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length > 0))
                    return Invalid(txtNtfyServer, "ntfy 服务器必须是有效的 HTTPS 地址");
            }
            if (!emailPage.ReadDraft(section, draft, Invalid)) return false;
            if (Includes(section, SettingsSection.Privacy))
            { draft.PrivacyMode = cmbPrivacy.SelectedIndex; draft.EmailPrivacyMode = cmbEmailPrivacy.SelectedIndex; draft.AutoLockEnabled = chkAutoLock.IsChecked == true; }
            if (Includes(section, SettingsSection.Retention)
                && !ReadNumber(txtHistoryRetention, "记录保留分钟数", "history_retention_minutes", out draft.HistoryRetentionMinutes)) return false;
            foreach (SettingsSection part in new SettingsSection[] { SettingsSection.Sms, SettingsSection.SmsRules, SettingsSection.Ntfy,
                SettingsSection.Email, SettingsSection.EmailRules, SettingsSection.Privacy, SettingsSection.Retention })
                if (Includes(section, part)) sectionErrors.Remove(part);
            return true;
        }
        bool QueueSection(SettingsSection section)
        {
            if (loadingValues || txtPort == null) return true;
            AdvanceSettingsVersion(section);
            Config draft; if (!ReadDraft(section, out draft)) return false;
            QueueDraft(section, draft); return true;
        }
        void QueueDraft(SettingsSection section, Config draft)
        {
            int version = AdvanceSettingsVersion(section);
            if (section != SettingsSection.Window) SettingsFeedback("正在保存…", false);
            bool restart = false;
            settingsQueue.Enqueue(delegate
            {
                CommitDraft(section, draft);
                restart = NetworkSignature(cfg) != initialNetworkSignature;
            }, delegate(Exception error)
            {
                if (error != null)
                {
                    System.Threading.Interlocked.Increment(ref settingsSaveFailureCount);
                    Log.Write("settings commit failed: " + section + " " + error.GetType().Name);
                }
                lock (settingsResultGate) settingsResults.Enqueue(new SettingsSaveResult
                    { Section = section, Version = version, Restart = restart, Error = error });
                try { Dispatcher.BeginInvoke(new Action(PublishSettingsResults)); } catch { }
            });
        }
        void PublishSettingsResults()
        {
            while (true)
            {
                SettingsSaveResult result;
                lock (settingsResultGate)
                { if (settingsResults.Count == 0) return; result = settingsResults.Dequeue(); }
                if (closeCompleted || !IsCurrentSave(result)) continue;
                if (result.Error != null) { RecordSaveFailure(result.Section, result.Error); continue; }
                foreach (SettingsSection part in AtomicSections) if (Includes(result.Section, part)) sectionErrors.Remove(part);
                settingsRecoveryRequired = sectionErrors.Count > 0;
                emailPage.RefreshHints(); UpdateEmailStatus();
                if (result.Section != SettingsSection.Window) SettingsFeedback(result.Restart ? "已保存 · 短信连接配置待重启生效" : "已保存并生效", false);
            }
        }
        void CommitDraft(SettingsSection section, Config draft)
        {
            bool rules;
            lock (Config.SaveGate)
            {
                rules = Includes(section, SettingsSection.EmailRules) && (cfg.EmailKeywords != draft.EmailKeywords || cfg.EmailSenders != draft.EmailSenders)
                    || Includes(section, SettingsSection.SmsRules) && (cfg.MinLen != draft.MinLen || cfg.MaxLen != draft.MaxLen);
                SettingsCommit.Commit(cfg, draft, section, cfgPath);
            }
            if (Includes(section, SettingsSection.Email | SettingsSection.EmailRules | SettingsSection.SmsRules))
            {
                Program.RefreshEmailWatchers();
                if (rules && Program.EmailConfigured()) EmailWatcher.RequestRecentCheck();
            }
            if (Includes(section, SettingsSection.Retention)) Program.ApplyHistoryRetention();
        }
        void RecordSaveFailure(SettingsSection section, Exception error)
        {
            SettingsCommitException failure = error as SettingsCommitException;
            settingsRecoveryRequired = failure != null && failure.RecoveryFailed || Config.LastSaveError != null;
            string reason = SettingsFailureReason(failure != null ? failure.InnerException : error);
            foreach (SettingsSection part in AtomicSections) if (Includes(section, part)) sectionErrors[part] = reason;
            SettingsFeedback("保存失败：" + reason + (failure != null && failure.RecoveryFailed
                ? "。运行配置保留旧值，但磁盘或启动项恢复未完成，请重试。" : "。运行配置未改变，草稿已保留，可重试。"), true);
        }
        void FlushSettings() { settingsQueue.Drain(); PublishSettingsResults(); }
        bool ApplySection(SettingsSection section)
        {
            if (applying || loadingValues || txtPort == null) return !settingsRecoveryRequired;
            AdvanceSettingsVersion(section);
            FlushSettings();
            Config draft;
            if (!ReadDraft(section, out draft)) return false;
            applying = true;
            try
            {
                Config proposed = new Config(); proposed.CopyFrom(cfg); SettingsCommit.Merge(proposed, draft, section);
                if (SettingsCommit.Equivalent(cfg, proposed) && !settingsRecoveryRequired && !Program.StartupSyncFailed)
                { SettingsFeedback("本区与已保存设置一致", false); return true; }
                CommitDraft(section, draft);
                settingsRecoveryRequired = false;
                emailPage.RefreshHints(); UpdateEmailStatus();
                SettingsFeedback(NetworkSignature(cfg) != initialNetworkSignature ? "已保存 · 短信连接配置待重启生效" : "已保存并生效", false);
                return true;
            }
            catch (Exception ex)
            {
                RecordSaveFailure(section, ex);
                return false;
            }
            finally { applying = false; }
        }
        static string NetworkSignature(Config c) { return MessageIdentity.Hash(c.Port + "\n" + c.PcIp + "\n" + c.Token + "\n" + c.NtfyServer + "\n" + c.NtfyTopic + "\n" + c.NtfyToken); }
        SettingsSection SectionFor(FrameworkElement field)
        {
            if (field == txtHistoryRetention) return SettingsSection.Retention;
            if (field == txtPort || field == txtPcIp || field == chkStartup) return SettingsSection.Sms;
            if (field == txtNtfyServer || field == txtNtfyTopic) return SettingsSection.Ntfy;
            SettingsSection emailSection = emailPage.SectionFor(field);
            if (emailSection != 0) return emailSection;
            if (field == cmbPrivacy || field == cmbEmailPrivacy || field == chkAutoLock) return SettingsSection.Privacy;
            return SettingsSection.SmsRules;
        }
    }
}
