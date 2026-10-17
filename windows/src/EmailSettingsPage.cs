using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Codepass
{
    // 邮箱页面组件：控件与草稿归页面所有，保存、提示和网络测试通过明确的入口接入宿主。
    sealed class EmailSettingsPage
    {
        sealed class FieldBinding
        {
            internal FrameworkElement Control;
            internal SettingDefinition Definition;
            internal string Label;
        }

        readonly FrameworkElement root;
        readonly Config cfg;
        readonly Func<bool> canPresent;
        readonly Func<Config> readConnection;
        readonly Func<Config, CancellationToken, string> testConnection;
        readonly List<FieldBinding> fields = new List<FieldBinding>();
        readonly Border[] cards;
        readonly TextBox txtEmailServer, txtEmailAccount, txtEmailKeywords, txtEmailSenders, txtEmailKeywordAdd, txtEmailSubjectPreview;
        readonly CheckBox chkEmail, chkQqPopup;
        readonly ComboBox cmbEmailProvider;
        readonly SecretField fldEmailAuth;
        readonly StackPanel emailAuthHost;
        readonly WrapPanel emailKeywordTags;
        readonly TextBlock emailAuthHint, emailProviderHint, lblEmailPreviewResult, lblEmailTestResult, lblQqStatus;
        readonly Button btnEmailTest, btnEmailAuthHelp, btnEmailGuide;
        readonly Brush faint = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : new SolidColorBrush(Color.FromRgb(0x61, 0x61, 0x61));
        readonly Brush accent = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));
        bool loading, warnedAuthUndecryptable;
        int emailDraftVersion;
        CancellationTokenSource emailTestCancel;

        internal event Action<SettingsSection> SaveRequested;
        internal event Action<string> ToastRequested;
        internal event Action<string> FeedbackRequested;
        internal event Action SimulateRequested;

        internal EmailSettingsPage(FrameworkElement root, Config cfg, Func<bool> canPresent,
            Func<Config> readConnection, Func<Config, CancellationToken, string> testConnection)
        {
            this.root = root; this.cfg = cfg; this.canPresent = canPresent;
            this.readConnection = readConnection; this.testConnection = testConnection;
            cards = new Border[] { Find<Border>("cardEmail"), Find<Border>("cardEmailFilters"), Find<Border>("cardQqPopup") };
            txtEmailServer = Find<TextBox>("txtEmailServer"); txtEmailAccount = Find<TextBox>("txtEmailAccount");
            txtEmailKeywords = Find<TextBox>("txtEmailKeywords"); txtEmailSenders = Find<TextBox>("txtEmailSenders");
            txtEmailKeywordAdd = Find<TextBox>("txtEmailKeywordAdd"); txtEmailSubjectPreview = Find<TextBox>("txtEmailSubjectPreview");
            chkEmail = Find<CheckBox>("chkEmail"); chkQqPopup = Find<CheckBox>("chkQqPopup");
            cmbEmailProvider = Find<ComboBox>("cmbEmailProvider"); emailAuthHost = Find<StackPanel>("emailAuthHost");
            emailKeywordTags = Find<WrapPanel>("emailKeywordTags"); emailAuthHint = Find<TextBlock>("emailAuthHint");
            emailProviderHint = Find<TextBlock>("emailProviderHint"); lblEmailPreviewResult = Find<TextBlock>("lblEmailPreviewResult");
            lblEmailTestResult = Find<TextBlock>("lblEmailTestResult"); lblQqStatus = Find<TextBlock>("lblQqStatus");
            btnEmailTest = Find<Button>("btnEmailTest"); btnEmailAuthHelp = Find<Button>("btnEmailAuthHelp");
            btnEmailGuide = Find<Button>("btnEmailGuide");
            fldEmailAuth = new SecretField(root.Resources["InnerPassword"] as Style,
                root.Resources["InnerInput"] as Style, root.Resources["InlineIconBtn"] as Style);
            fldEmailAuth.Attach(emailAuthHost);

            Bind("email_enabled", "chkEmail", "启用邮箱接收");
            Bind("email_server", "txtEmailServer", "IMAP 服务器");
            Bind("email_account", "txtEmailAccount", "邮箱账号");
            Bind("email_poll_seconds", "txtEmailPoll", "邮箱检查间隔");
            Bind("email_keywords_b64", "txtEmailKeywords", "主题关键词");
            Bind("email_senders_b64", "txtEmailSenders", "发件人白名单");
            Bind("email_check_junk", "chkEmailJunk", "检查垃圾文件夹");
            Bind("email_notify", "chkEmailNotify", "到达提示");
            Bind("email_autocopy", "chkEmailAutoCopy", "自动复制");
            Bind("email_save_body", "chkEmailSaveBody", "保存邮件正文");
            Bind("qq_popup_enabled", "chkQqPopup", "QQ 弹窗联动");
            SettingAttribute poll = ConfigSettings.Get("email_poll_seconds").Setting;
            Find<TextBlock>("emailPollHint").Text = poll.Minimum + "～" + poll.Maximum + "；支持 IDLE 时实时接收，其他目录定时检查";

            fldEmailAuth.Changed += InvalidateConnection;
            cmbEmailProvider.SelectionChanged += delegate
            {
                if (loading) return;
                ApplyProviderPreset(); Feedback("邮箱连接草稿已修改 · 测试后点击保存连接");
            };
            txtEmailServer.TextChanged += delegate { if (!loading) RefreshHints(); };
            txtEmailKeywords.TextChanged += delegate { if (!loading) RenderKeywordTags(); };
            txtEmailSubjectPreview.TextChanged += delegate { UpdatePreview(); };
            txtEmailKeywordAdd.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Return) { AddKeywords(); e.Handled = true; }
                if (e.Key == Key.Escape) { txtEmailKeywordAdd.Clear(); txtEmailKeywordAdd.Visibility = Visibility.Collapsed; e.Handled = true; }
            };
            txtEmailKeywordAdd.LostKeyboardFocus += delegate
            { if (txtEmailKeywordAdd.Text.Length == 0) txtEmailKeywordAdd.Visibility = Visibility.Collapsed; };
            Find<Button>("btnEmailKeywordAdd").Click += delegate { AddKeywords(); };
            Find<Button>("btnEmailKeywordDefault").Click += delegate
            {
                if (CodepassDialog.Show(Window.GetWindow(root), "恢复默认主题关键词？", "确认", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
                txtEmailKeywords.Text = (string)ConfigSettings.Get("email_keywords_b64").Setting.DefaultValue;
                Save(SettingsSection.EmailRules);
            };
            Find<Button>("btnEmailSave").Click += delegate { Save(SettingsSection.Email); };
            Find<Button>("btnTestEmail").Click += delegate { if (SimulateRequested != null) SimulateRequested(); };
            btnEmailTest.Click += delegate { TestConnection(); };
            btnEmailAuthHelp.Click += delegate
            {
                try { Process.Start(new ProcessStartInfo("https://myaccount.google.com/apppasswords") { UseShellExecute = true }); }
                catch { Toast("无法打开浏览器，请在 Google 账号设置中查找应用专用密码"); }
            };
            btnEmailGuide.Click += delegate
            {
                string guide = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GMAIL_APP_PASSWORD.md");
                if (!File.Exists(guide)) guide = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "docs", "GMAIL_APP_PASSWORD.md"));
                try { Process.Start(new ProcessStartInfo(File.Exists(guide) ? guide : "https://support.google.com/accounts/answer/185833?hl=zh-Hans") { UseShellExecute = true }); }
                catch { Toast("请查看交付目录的 GMAIL_APP_PASSWORD.md 教程"); }
            };
        }

        T Find<T>(string name) where T : FrameworkElement
        {
            T control = root.FindName(name) as T;
            if (control == null) throw new InvalidOperationException("邮箱页面缺少控件：" + name);
            return control;
        }

        void Bind(string key, string name, string label)
        {
            FieldBinding binding = new FieldBinding { Definition = ConfigSettings.Get(key), Control = Find<FrameworkElement>(name), Label = label };
            TextBox text = binding.Control as TextBox;
            CheckBox check = binding.Control as CheckBox;
            Type type = binding.Definition.Field.FieldType;
            if (text == null && check == null || check != null && type != typeof(bool) || text != null && type != typeof(string) && type != typeof(int))
                throw new InvalidOperationException("邮箱配置控件类型不匹配：" + key);
            fields.Add(binding);
            SettingsSection section = binding.Definition.Setting.Section;
            if (text != null)
            {
                if (section == SettingsSection.Email) text.TextChanged += delegate { InvalidateConnection(); };
                else
                {
                    text.LostFocus += delegate { Save(section); };
                    text.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Return && !text.AcceptsReturn) Save(section); };
                }
            }
            if (check != null) check.Click += delegate
            {
                if (section == SettingsSection.Email) { InvalidateConnection(); Feedback("邮箱连接草稿已修改 · 点击保存连接生效"); }
                else Save(section);
            };
        }

        internal Border Anchor { get { return cards[0]; } }
        internal bool ConnectionDirty
        {
            get { return txtEmailServer.Text.Trim() != cfg.EmailServer || txtEmailAccount.Text.Trim() != cfg.EmailAccount
                || fldEmailAuth.Text.Trim() != cfg.EmailAuth || (chkEmail.IsChecked == true) != cfg.EmailEnabled; }
        }

        internal void SetVisible(bool visible)
        { foreach (Border card in cards) card.Visibility = visible ? Visibility.Visible : Visibility.Collapsed; }

        internal void AttachMultilineScrolling(MouseWheelEventHandler handler)
        {
            foreach (FieldBinding binding in fields)
            {
                TextBox text = binding.Control as TextBox;
                if (text != null && text.AcceptsReturn) text.PreviewMouseWheel += handler;
            }
        }

        internal void Load()
        {
            CancelTests(); loading = true;
            lblEmailTestResult.Text = "";
            lblEmailTestResult.Foreground = faint;
            try
            {
                foreach (FieldBinding binding in fields)
                {
                    object value = binding.Definition.Get(cfg);
                    TextBox text = binding.Control as TextBox;
                    if (text != null) text.Text = value == null ? "" : value.ToString();
                    else ((CheckBox)binding.Control).IsChecked = (bool)value;
                }
                fldEmailAuth.Text = cfg.EmailAuth;
                cmbEmailProvider.SelectedIndex = ProviderIndexOf(cfg.EmailServer);
                if (cfg.EmailAuthUndecryptable && !warnedAuthUndecryptable)
                { warnedAuthUndecryptable = true; Toast("邮箱授权码已失效（换机或换了 Windows 账户），请重新填写"); }
            }
            finally { loading = false; }
            RefreshHints(); RenderKeywordTags();
        }

        internal SettingsSection SectionFor(FrameworkElement control)
        {
            if (control == cmbEmailProvider || control == emailAuthHost) return SettingsSection.Email;
            foreach (FieldBinding binding in fields)
                if (binding.Control == control) return binding.Definition.Setting.Section;
            return (SettingsSection)0;
        }

        internal bool ReadDraft(SettingsSection section, Config draft, Func<FrameworkElement, string, bool> invalid)
        {
            foreach (FieldBinding binding in fields)
            {
                if ((binding.Definition.Setting.Section & section) == 0) continue;
                CheckBox check = binding.Control as CheckBox;
                if (check != null) { binding.Definition.Set(draft, check.IsChecked == true); continue; }
                string text = ((TextBox)binding.Control).Text;
                if (binding.Definition.Field.FieldType == typeof(int))
                {
                    int value;
                    if (!binding.Definition.TryNumber(text, out value)) return invalid(binding.Control, binding.Definition.NumberError(binding.Label));
                    binding.Definition.Set(draft, value);
                }
                else binding.Definition.Set(draft, text);
            }
            if ((section & SettingsSection.Email) != 0)
            {
                draft.EmailServer = draft.EmailServer.Trim(); draft.EmailAccount = draft.EmailAccount.Trim(); draft.EmailAuth = fldEmailAuth.Text.Trim();
                if (draft.EmailEnabled && (draft.EmailServer.Length == 0 || draft.EmailServer.IndexOfAny(new char[] { ' ', '/', ':' }) >= 0))
                    return invalid(txtEmailServer, "请输入 IMAP 服务器域名");
                if (draft.EmailEnabled && draft.EmailAccount.Length == 0) return invalid(txtEmailAccount, "启用接收前请填写邮箱账号");
                if (draft.EmailEnabled && draft.EmailAuth.Length == 0) return invalid(emailAuthHost, "启用接收前请填写授权码");
                if (draft.EmailAuth.Length > 0) draft.EmailPasswordRequired = false;
            }
            if ((section & SettingsSection.EmailRules) != 0)
            {
                string error;
                if (!MailRules.NormalizeLines(draft.EmailKeywords, out draft.EmailKeywords, out error)) return invalid(txtEmailKeywords, error);
                if (!MailRules.NormalizeSenders(draft.EmailSenders, out draft.EmailSenders, out error)) return invalid(txtEmailSenders, error);
            }
            return true;
        }

        void Save(SettingsSection section) { if (!loading && SaveRequested != null) SaveRequested(section); }
        void Toast(string text) { if (ToastRequested != null) ToastRequested(text); }
        void Feedback(string text) { if (!loading && FeedbackRequested != null) FeedbackRequested(text); }

        void InvalidateConnection()
        {
            if (loading) return;
            CancelTests(); lblEmailTestResult.Text = "连接草稿已修改；请重新测试并保存";
            Feedback("邮箱连接尚未保存 · 可先测试，再保存连接");
        }

        internal void CancelTests()
        {
            emailDraftVersion++;
            if (emailTestCancel != null) emailTestCancel.Cancel();
        }

        static readonly string[] ProviderServers = { "imap.qq.com", "imap.163.com", "imap.126.com", "imap.gmail.com", "outlook.office365.com" };
        static int ProviderIndexOf(string server)
        {
            for (int i = 0; i < ProviderServers.Length; i++)
                if (string.Equals(server, ProviderServers[i], StringComparison.OrdinalIgnoreCase)) return i;
            return ProviderServers.Length;
        }

        void ApplyProviderPreset()
        {
            int index = cmbEmailProvider.SelectedIndex;
            if (index >= 0 && index < ProviderServers.Length
                && !string.Equals(ProviderServers[index], txtEmailServer.Text.Trim(), StringComparison.OrdinalIgnoreCase))
            { chkEmail.IsChecked = false; fldEmailAuth.Text = ""; txtEmailServer.Text = ProviderServers[index]; }
            RefreshHints();
        }

        internal void RefreshHints()
        {
            int provider = ProviderIndexOf(txtEmailServer.Text.Trim()); bool gmail = provider == 3;
            emailAuthHint.Text = cfg.EmailPasswordRequired ? "请重新填写 IMAP 授权码 / 应用专用密码"
                : cfg.EmailAuthUndecryptable ? "已失效：换机或换了 Windows 账户，请重新填写"
                : gmail ? "使用 Google 16 位应用密码" : "使用授权码 / 应用密码，非登录密码";
            emailProviderHint.Text = gmail ? "Gmail：开启两步验证后生成应用密码。"
                : provider >= 0 && provider <= 2 ? "QQ / 163 / 126：开启 IMAP 后获取授权码。" : "使用 IMAP 授权码或应用密码。";
            btnEmailAuthHelp.Visibility = btnEmailGuide.Visibility = gmail ? Visibility.Visible : Visibility.Collapsed;
        }

        static List<string> KeywordList(string text)
        {
            List<string> list = new List<string>(); HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            { string value = line.Trim(); if (value.Length > 0 && seen.Add(value)) list.Add(value); }
            return list;
        }

        void RenderKeywordTags()
        {
            emailKeywordTags.Children.Clear(); UpdatePreview();
            foreach (string keyword in KeywordList(txtEmailKeywords.Text))
            {
                string value = keyword;
                Border chip = new Border { Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xF2, 0xFC)), CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(10, 2, 6, 2), Margin = new Thickness(0, 0, 6, 6) };
                StackPanel content = new StackPanel { Orientation = Orientation.Horizontal };
                content.Children.Add(new TextBlock { Text = value, Foreground = accent, FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 250, ToolTip = value });
                Button remove = new Button { Content = "×", Width = 24, Height = 24, FontSize = 16,
                    Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = faint,
                    Cursor = Cursors.Hand, ToolTip = "移除关键词", Margin = new Thickness(4, 0, 0, 0) };
                System.Windows.Automation.AutomationProperties.SetName(remove, "移除关键词 " + value);
                remove.Click += delegate
                {
                    List<string> words = KeywordList(txtEmailKeywords.Text); words.Remove(value);
                    txtEmailKeywords.Text = string.Join("\n", words.ToArray()); Save(SettingsSection.EmailRules);
                };
                content.Children.Add(remove); chip.Child = content; emailKeywordTags.Children.Add(chip);
            }
        }

        void AddKeywords()
        {
            if (txtEmailKeywordAdd.Visibility != Visibility.Visible)
            { txtEmailKeywordAdd.Visibility = Visibility.Visible; txtEmailKeywordAdd.Focus(); return; }
            string text = string.Join("\n", KeywordList(txtEmailKeywords.Text + "\n" + txtEmailKeywordAdd.Text).ToArray());
            string normalized, error;
            if (!MailRules.NormalizeLines(text, out normalized, out error)) { Toast("主题关键词无效：" + error); return; }
            txtEmailKeywords.Text = normalized; txtEmailKeywordAdd.Clear(); Save(SettingsSection.EmailRules);
        }

        void UpdatePreview()
        {
            string normalized, error;
            lblEmailPreviewResult.Text = !MailRules.NormalizeLines(txtEmailKeywords.Text, out normalized, out error)
                ? "规则无效：" + error : txtEmailSubjectPreview.Text.Length == 0 ? "输入主题查看是否会被接收"
                : MailRules.SubjectMatches(txtEmailSubjectPreview.Text, normalized) ? "主题命中；仍需满足发件人和时效规则" : "主题未命中，将跳过";
        }

        internal void UpdateStatus(bool configured, string lastSignal)
        {
            lblQqStatus.Text = !configured ? "请先配置并启用邮箱" : !cfg.QqPopupEnabled ? "联动已关闭" : "监测中 · 最近提醒 " + lastSignal;
            chkQqPopup.IsEnabled = configured;
        }

        void TestConnection()
        {
            if (emailTestCancel != null) { CancelTests(); lblEmailTestResult.Text = "连接测试已取消"; return; }
            Config identity = readConnection();
            if (identity == null) return;
            if (identity.EmailAccount.Length == 0 || identity.EmailServer.Length == 0 || identity.EmailPasswordRequired || identity.EmailAuth.Length == 0)
            { Toast("请先填写完整邮箱和 IMAP 授权码 / 应用专用密码"); return; }
            CancellationTokenSource cancel = new CancellationTokenSource(); emailTestCancel = cancel;
            int version = emailDraftVersion;
            btnEmailTest.Content = "取消测试"; lblEmailTestResult.Text = "正在测试认证与只读收件箱连接…";
            Thread worker = new Thread(delegate()
            {
                OperationResult result;
                try { result = new OperationResult(OperationState.Success, testConnection(identity, cancel.Token)); }
                catch (OperationCanceledException) { result = new OperationResult(OperationState.Cancelled, "连接测试已取消"); }
                catch { result = new OperationResult(cancel.IsCancellationRequested ? OperationState.Cancelled : OperationState.Failed,
                    cancel.IsCancellationRequested ? "连接测试已取消" : "连接测试失败，请检查网络、服务设置与授权"); }
                try { root.Dispatcher.BeginInvoke(new Action(delegate
                {
                    bool ownsTest = emailTestCancel == cancel;
                    if (ownsTest) { emailTestCancel = null; btnEmailTest.Content = "测试邮箱连接"; }
                    bool cancelled = cancel.IsCancellationRequested; cancel.Dispose();
                    if (!ownsTest || cancelled || version != emailDraftVersion || !canPresent()) return;
                    lblEmailTestResult.Text = result.Message + (result.State == OperationState.Success ? " · 此次测试未保存；可点击保存连接" : "");
                    lblEmailTestResult.Foreground = result.State == OperationState.Failed ? Brushes.Firebrick : faint;
                })); } catch { cancel.Dispose(); }
            });
            worker.IsBackground = true; worker.Name = "email-test"; worker.Start();
        }
    }
}
