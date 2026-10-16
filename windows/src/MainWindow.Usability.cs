using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Codepass
{
    sealed partial class MainWindow
    {
        int emailDraftVersion, networkDraftVersion;
        CancellationTokenSource pcTestCancel, ntfyTestCancel;
        DispatcherTimer historyRefreshTimer;
        bool historyDirty, searchChanged;
        readonly List<Grid> fieldActionRows = new List<Grid>();
        readonly List<Grid> settingsCardRows = new List<Grid>();
        double settingsFieldWidth = 320;
        bool settingsCompact;

        void InitializeUsability(FrameworkElement root)
        {
            initialNetworkSignature = NetworkSignature(cfg);
            Wire(root, "btnMax", delegate { ToggleMaximize(); });
            Wire(root, "btnEmailSave", delegate { QueueSection(SettingsSection.Email); });
            Wire(root, "btnCopyLatest", delegate { if (lockOverlay.Visibility != Visibility.Visible) Program.CopyLatestCode(); });
            Wire(root, "btnNewRecords", delegate { RefreshHistory(); historyScroll.ScrollToTop(); Find<Button>(root, "btnNewRecords").Visibility = Visibility.Collapsed; });
            Wire(root, "btnDisableLock", delegate { DisablePasswordLock(); });
            Wire(root, "btnWebhook", delegate { ShowWebhookGuide(); });
            Wire(root, "btnTestSms", delegate { SimulateTestMessage(false); });
            Wire(root, "btnTestEmail", delegate { SimulateTestMessage(true); });
            PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (lockOverlay.Visibility == Visibility.Visible) return;
                if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
                { SelectSection("history"); txtHistorySearch.Focus(); txtHistorySearch.SelectAll(); e.Handled = true; }
            };
            IsVisibleChanged += delegate
            {
                if (!IsVisible) ReleaseHiddenResources();
                else if (historyDirty && activeSection == "history") RefreshHistory();
            };
            foreach (TextBox box in new TextBox[] { txtEmailServer, txtEmailAccount })
                box.TextChanged += delegate { InvalidateEmailTest(); };
            fldEmailAuth.Changed += InvalidateEmailTest;
            foreach (TextBox box in new TextBox[] { txtPort, txtPcIp, txtNtfyServer, txtNtfyTopic })
                box.TextChanged += delegate { InvalidateNetworkTests(); };
            fldToken.Changed += InvalidateNetworkTests; fldNtfyToken.Changed += InvalidateNetworkTests;
            AddFieldErrors(root);
            AddBlockErrors(root);
            AddCardErrors(root);
            InitializeFieldActionRows(root);
            InitializeSettingsCards(root);
            InitializeSettingsCardClicks(root);
            Loaded += delegate { UpdateReceiveSummary(); UpdateCompactShell(); };
            SizeChanged += delegate { UpdateCompactShell(); };
        }
        /// <summary>测试信息：把一条带验证码的示例消息走完整接收链路（记录、剪贴板、通知）。</summary>
        void SimulateTestMessage(bool email)
        {
            try
            {
                int length = Math.Min(Math.Max(cfg.MinLen, 1), 12);
                string code = "135790246813".Substring(0, length);
                string messageKey = "test-" + (email ? "email-" : "sms-") + DateTime.UtcNow.Ticks;
                if (email)
                {
                    string mail = "您的验证码是 " + code + "，请勿泄露给他人。（codepass 测试邮件）";
                    Program.Incoming.SubmitEmail(mail, "test@codepass.local", code, messageKey, cfg, true, null);
                }
                else
                {
                    string sms = "【codepass 测试】您的验证码是 " + code + "，5 分钟内有效。";
                    Program.Incoming.ReceiveSms(sms, "test", "codepass 测试", messageKey, DateTime.UtcNow, true, true);
                }
                ShowToast("已模拟收到" + (email ? "测试邮件" : "测试短信") + "：验证码 " + code);
            }
            catch (Exception error)
            {
                Log.Write("test message failed: " + error.Message);
                ShowToast("测试信息失败：" + error.Message);
            }
        }
        void InvalidateEmailTest()
        {
            if (loadingValues) return;
            emailConnectionDirty = !EmailDraftMatchesSaved();
            emailDraftVersion++;
            if (emailTestCancel != null) emailTestCancel.Cancel();
            if (lblEmailTestResult != null) lblEmailTestResult.Text = "连接草稿已修改；请重新测试并保存";
            SettingsFeedback("邮箱连接尚未保存 · 可先测试，再保存连接", false);
        }
        void InvalidateNetworkTests()
        {
            if (loadingValues) return;
            networkDraftVersion++;
            if (pcTestCancel != null) pcTestCancel.Cancel();
            if (ntfyTestCancel != null) ntfyTestCancel.Cancel();
        }
        bool CanShowTestResult(int started, int current)
        {
            return started == current && !closeCompleted && IsVisible && lockOverlay.Visibility != Visibility.Visible;
        }
        void CancelConnectionTests()
        {
            emailDraftVersion++; networkDraftVersion++;
            if (emailTestCancel != null) emailTestCancel.Cancel();
            if (pcTestCancel != null) pcTestCancel.Cancel();
            if (ntfyTestCancel != null) ntfyTestCancel.Cancel();
        }
        void RunNetworkTest(bool ntfy, string url, string token)
        {
            CancellationTokenSource cancel = new CancellationTokenSource();
            if (ntfy) ntfyTestCancel = cancel; else pcTestCancel = cancel;
            Button button = ntfy ? btnTestNtfy : btnTestPc;
            SetTestButton(button, true, "取消测试"); SettingsFeedback("正在测试连接…", false);
            int version = networkDraftVersion;
            ThreadPool.QueueUserWorkItem(delegate
            {
                OperationResult result = ConnectionProbe.Http(url, token, ntfy, cancel.Token);
                try { Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (ntfy) ntfyTestCancel = null; else pcTestCancel = null;
                    if (cancel.IsCancellationRequested) result = new OperationResult(OperationState.Cancelled, "测试已取消");
                    cancel.Dispose(); SetTestButton(button, true, "测试连通性");
                    if (CanShowTestResult(version, networkDraftVersion))
                    {
                        // 本机接收服务没在监听时要点出这一层：否则只剩笼统的“连接失败”，看不出是程序自己没绑上。
                        string message = result.Message;
                        if (!ntfy && result.State == OperationState.Failed && RuntimeStatus.Http == ConnectionState.Retrying)
                            message += "（本机接收服务" + RuntimeStatus.ConnectionText(RuntimeStatus.Http) + "；监听地址可能尚未就绪，程序会自动重试）";
                        SettingsFeedback(message, result.State == OperationState.Failed);
                    }
                })); } catch { cancel.Dispose(); }
            });
        }
        void ScheduleHistoryRefresh(bool search)
        {
            historyDirty = true; searchChanged |= search;
            if (!IsVisible || activeSection != "history") return;
            if (historyRefreshTimer == null)
            {
                historyRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                historyRefreshTimer.Tick += delegate
                {
                    historyRefreshTimer.Stop(); bool reset = searchChanged; searchChanged = false;
                    RefreshHistory(); if (reset) historyScroll.ScrollToTop();
                };
            }
            historyRefreshTimer.Stop(); historyRefreshTimer.Start();
        }
        void UpdateReceiveSummary()
        {
            if (Content == null) return;
            FrameworkElement root = (FrameworkElement)Content;
            Program.LatestReceipt receipt = Program.GetLatestReceipt();
            TextBlock code = Find<TextBlock>(root, "lblLatestCode"), meta = Find<TextBlock>(root, "lblLatestMeta");
            if (code != null) code.Text = receipt == null ? "等待验证码" : receipt.Code;
            if (meta != null) meta.Text = receipt == null ? "连接手机短信或邮箱后，在这里快速复制" : (receipt.EmailChannel ? "邮件" : "短信")
                + " · " + new DateTime(receipt.ReceivedUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("HH:mm:ss") + " · " + RuntimeStatus.Clipboard;
            Button copy = Find<Button>(root, "btnCopyLatest"); if (copy != null) copy.IsEnabled = receipt != null;
        }
        /// <summary>电脑接收服务状态：绑定失败会自动重试，状态和实际监听地址都要显示出来，不能只写在日志里。</summary>
        void UpdatePcReceiverStatus()
        {
            if (lblPcStatus == null) return;
            ConnectionState state = RuntimeStatus.Http;
            string bound = Program.Http == null || Program.Http.BoundEndpoint == null ? "" : Program.Http.BoundEndpoint;
            // 配置了局域网地址和令牌却只监听本机（地址非法时会被降级）时服务其实不可达，不能显示成“正常”。
            bool unreachable = state == ConnectionState.Ready && bound.StartsWith("127.0.0.1", StringComparison.Ordinal)
                && cfg.Token.Length > 0 && cfg.PcIp.Trim().Length > 0;
            lblPcStatus.Text = unreachable ? "接收服务：仅本机监听，手机无法连接"
                : "接收服务：" + RuntimeStatus.ConnectionText(state)
                    + (state == ConnectionState.Ready && bound.Length > 0 ? " · " + bound : "");
            if (unreachable || state == ConnectionState.Retrying || state == ConnectionState.Failed) lblPcStatus.Foreground = Brushes.Firebrick;
            else lblPcStatus.ClearValue(TextBlock.ForegroundProperty);
        }

        void UpdateCompactShell()
        {
            ColumnDefinition nav = ((FrameworkElement)Content).FindName("navWidth") as ColumnDefinition;
            if (nav != null) nav.Width = new GridLength(ActualWidth < 900 ? 132 : 178);
            FrameworkElement root = (FrameworkElement)Content;
            bool shortWindow = ActualHeight < 620;
            foreach (string name in new string[] { "historyTitle", "lblLatestMeta", "historySearchHint", "historyKeyboardHint" })
                Find<FrameworkElement>(root, name).Visibility = shortWindow ? Visibility.Collapsed : Visibility.Visible;
            Find<Border>(root, "latestCard").Padding = new Thickness(shortWindow ? 6 : 12);
            Find<Border>(root, "latestCard").Margin = new Thickness(0, 0, 0, shortWindow ? 4 : 8);
            Find<StackPanel>(root, "historySearchPanel").Margin = shortWindow ? new Thickness(0, 2, 0, 4) : new Thickness(0, 8, 0, 10);
            Border record = Find<Border>(root, "historyRecordCard");
            record.Margin = shortWindow ? new Thickness(8, 4, 8, 4) : new Thickness(22, 18, 22, 14);
            record.Padding = shortWindow ? new Thickness(8, 6, 8, 6) : new Thickness(18, 18, 18, 12);
            Find<Border>(root, "historyFooter").Padding = new Thickness(12, shortWindow ? 4 : 12, 12, shortWindow ? 4 : 12);
            UpdateReceiveSummary();
        }
        void InitializeFieldActionRows(DependencyObject node)
        {
            Grid row = node as Grid;
            if (row != null && object.Equals(row.Tag, "FieldActions"))
            {
                fieldActionRows.Add(row);
                row.SizeChanged += delegate { ArrangeFieldActions(row); };
            }
            foreach (object child in LogicalTreeHelper.GetChildren(node))
                if (child is DependencyObject) InitializeFieldActionRows((DependencyObject)child);
        }
        void InitializeSettingsCards(DependencyObject node)
        {
            Grid card = node as Grid;
            if (card != null && IsSettingsCard(card))
            {
                settingsCardRows.Add(card);
                // 卡片最小高度多出来的空间会留在行下面（Auto 行不吃剩余空间），
                // 只把行居中会让内容看着偏上；整块内容居中才对。
                card.VerticalAlignment = VerticalAlignment.Center;
            }
            foreach (object child in LogicalTreeHelper.GetChildren(node))
                if (child is DependencyObject) InitializeSettingsCards((DependencyObject)child);
        }
        /// <summary>整行可点的开关卡片：点卡片任意位置（开关本身除外）都切换开关。</summary>
        void InitializeSettingsCardClicks(DependencyObject node)
        {
            Border card = node as Border;
            if (card != null && object.Equals(card.Tag, "SettingsToggle"))
            {
                CheckBox box = FindCheckBox(card);
                if (box != null)
                {
                    CheckBox target = box;
                    HookToggleMotion(box);
                    card.Cursor = Cursors.Hand;
                    card.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
                    {
                        if (!target.IsEnabled || IsInside(e.OriginalSource, target)) return;
                        target.IsChecked = !(target.IsChecked == true);
                    };
                }
            }
            foreach (object child in LogicalTreeHelper.GetChildren(node))
                if (child is DependencyObject) InitializeSettingsCardClicks((DependencyObject)child);
        }
        /// <summary>开关滑动动效放在代码里做：XAML 里的动画会被“系统报告关闭动画”整段剔除（远程桌面就会误报关闭）。</summary>
        void HookToggleMotion(CheckBox box)
        {
            box.Checked += delegate { AnimateToggle(box); };
            box.Unchecked += delegate { AnimateToggle(box); };
            box.Loaded += delegate { AnimateToggle(box, true); };
        }
        void AnimateToggle(CheckBox box) { AnimateToggle(box, false); }
        /// <summary>静态位置（不播放）用于加载设置和模板首次套用：XAML 的 Setter 不能指向模板里的 Freezable。</summary>
        void AnimateToggle(CheckBox box, bool staticOnly)
        {
            box.ApplyTemplate();
            if (box.Template == null) return;
            TranslateTransform shift = box.Template.FindName("KnobShift", box) as TranslateTransform;
            if (shift == null) return;
            bool on = box.IsChecked == true;
            if (loadingValues || staticOnly)
            {
                shift.BeginAnimation(TranslateTransform.XProperty, null);
                shift.X = on ? 20 : 0;
                return;
            }
            TimeSpan span = TimeSpan.FromMilliseconds(170);
            DoubleAnimation move = new DoubleAnimation(on ? 0 : 20, on ? 20 : 0, span);
            move.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            move.FillBehavior = FillBehavior.HoldEnd;
            Timeline.SetDesiredFrameRate(move, DisplayMotion.RefreshRate(this));
            shift.BeginAnimation(TranslateTransform.XProperty, move, HandoffBehavior.SnapshotAndReplace);
            FrameworkElement track = box.Template.FindName("TrackOn", box) as FrameworkElement;
            if (track == null) return;
            // 轨道蓝底淡入淡出；结束值由模板 Setter 接管，所以用 FillBehavior.Stop。
            DoubleAnimation fade = new DoubleAnimation(on ? 0 : 1, on ? 1 : 0, span);
            fade.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            fade.FillBehavior = FillBehavior.Stop;
            track.BeginAnimation(UIElement.OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
        }
        static CheckBox FindCheckBox(DependencyObject node)
        {
            CheckBox box = node as CheckBox;
            if (box != null) return box;
            foreach (object child in LogicalTreeHelper.GetChildren(node))
                if (child is DependencyObject) { CheckBox found = FindCheckBox((DependencyObject)child); if (found != null) return found; }
            return null;
        }
        static bool IsInside(object source, DependencyObject target)
        {
            DependencyObject node = source as DependencyObject;
            while (node != null)
            {
                if (object.ReferenceEquals(node, target)) return true;
                node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
            }
            return false;
        }
        /// <summary>SettingsCard 的窄宽度布局：控件从右侧移到标题下方（Fluent 同款行为）。
        /// Tag=SettingsCardStacked 的卡片（多行输入、宽按钮组）始终把内容放在标题下方。</summary>
        void ArrangeSettingsCards(bool compact)
        {
            foreach (Grid card in settingsCardRows)
            {
                if (card.Children.Count != 2) continue;
                FrameworkElement content = card.Children[1] as FrameworkElement;
                if (content == null) continue;
                bool below = compact || object.Equals(card.Tag, "SettingsCardStacked");
                if (below)
                {
                    // 内容放到标题下方：对齐方式交给 XAML（通栏输入框拉伸、输入框分组各自贴右）。
                    Grid.SetRow(content, 1); Grid.SetColumn(content, 0); Grid.SetColumnSpan(content, 2);
                    content.Margin = new Thickness(0, 12, 0, 0);
                }
                else
                {
                    Grid.SetRow(content, 0); Grid.SetColumn(content, 1); Grid.SetColumnSpan(content, 1);
                    content.Margin = new Thickness(16, 0, 0, 0);
                    content.HorizontalAlignment = HorizontalAlignment.Right;
                }
            }
        }
        void UpdateSettingsFieldWidth(bool compact)
        {
            // 设置页内容列：上限 560 并在窗口内居中；输入框统一宽度 180（同类控件同长同右线）。
            double column = Math.Max(240, Math.Min(560, settingsScroll.ActualWidth - 44));
            settingsFieldWidth = column - 40;
            settingsCompact = compact;
            double width = Math.Min(180, Math.Max(110, settingsFieldWidth - 170));
            FrameworkElement root = (FrameworkElement)Content;
            if (Math.Abs((double)root.Resources["SettingsInputWidth"] - width) > 0.5) root.Resources["SettingsInputWidth"] = width;
            // 一体化容器（输入框 + 右侧动作段）需要比输入框本身更宽。
            if (Math.Abs((double)root.Resources["SettingsGroupInputWidth"] - (width + 104)) > 0.5) root.Resources["SettingsGroupInputWidth"] = width + 104;
            foreach (Grid row in fieldActionRows) ArrangeFieldActions(row);
        }
        void ArrangeFieldActions(Grid row)
        {
            // 右对齐与窗口宽窄无关：输入框与动作按钮始终贴字段列右边缘。
            row.HorizontalAlignment = HorizontalAlignment.Right;
            if (row.Children.Count != 2 || row.ActualWidth <= 0) return;
            double width = (double)((FrameworkElement)Content).Resources["SettingsInputWidth"];
            // 表单行现在整行贴右，行宽由内容决定，所以放不放得下要看字段列可用宽度。
            bool stacked = settingsFieldWidth < width + 252;
            FrameworkElement action = (FrameworkElement)row.Children[1];
            Grid.SetRow(action, stacked ? 1 : 0); Grid.SetColumn(action, stacked ? 0 : 1);
            Grid.SetColumnSpan(action, stacked ? 2 : 1);
            action.Margin = stacked ? new Thickness(0, 10, 0, 0) : new Thickness(12, 0, 0, 0);
            action.VerticalAlignment = VerticalAlignment.Center;
        }
        void AddFieldErrors(DependencyObject node)
        {
            // 标签和编辑框建立自动化名称；错误使用同行下方的说明，不覆盖草稿。
            Grid grid = node as Grid;
            if (grid != null && grid.ColumnDefinitions.Count == 2 && grid.ColumnDefinitions[0].Width.IsAbsolute)
            {
                List<FrameworkElement> fields = new List<FrameworkElement>();
                foreach (UIElement child in grid.Children) if (child is FrameworkElement) fields.Add((FrameworkElement)child);
                foreach (FrameworkElement field in fields)
                {
                    if (Grid.GetColumn(field) != 1 || (!(field is TextBox) && field.Name != "emailAuthHost")) continue;
                    TextBlock label = null;
                    foreach (FrameworkElement candidate in fields)
                        if (candidate is TextBlock && Grid.GetColumn(candidate) == 0 && Grid.GetRow(candidate) == Grid.GetRow(field)) label = (TextBlock)candidate;
                    if (label != null) AutomationProperties.SetName(field, label.Text);
                    TextBlock error = new TextBlock { Foreground = Brushes.Firebrick, FontSize = 12, TextWrapping = TextWrapping.Wrap,
                        Visibility = Visibility.Collapsed, Margin = new Thickness(0, 3, 0, 4) };
                    int row = Grid.GetRow(field); Thickness margin = field.Margin;
                    HorizontalAlignment align = field.HorizontalAlignment;
                    grid.Children.Remove(field);
                    // 外层容器接管对齐方式（宽窗口贴右、窄窗口回到左侧），字段本身只填满容器。
                    StackPanel wrapper = new StackPanel { Margin = margin, HorizontalAlignment = align };
                    field.Margin = new Thickness(0); field.HorizontalAlignment = HorizontalAlignment.Stretch;
                    Grid.SetRow(wrapper, row); Grid.SetColumn(wrapper, 1);
                    wrapper.Children.Add(field); wrapper.Children.Add(error); grid.Children.Add(wrapper);
                    if (row < grid.RowDefinitions.Count) grid.RowDefinitions[row].Height = GridLength.Auto;
                    fieldErrors[field] = error;
                }
            }
            List<DependencyObject> children = new List<DependencyObject>();
            foreach (object child in LogicalTreeHelper.GetChildren(node)) if (child is DependencyObject) children.Add((DependencyObject)child);
            foreach (DependencyObject child in children) AddFieldErrors(child);
        }
        /// <summary>“标签在上”的多行字段块（Tag=LabelBlock）同样提供同行下方的错误提示与自动化名称。</summary>
        void AddBlockErrors(DependencyObject node)
        {
            Panel panel = node as Panel;
            if (panel != null && object.Equals(panel.Tag, "LabelBlock"))
            {
                TextBlock label = null; TextBox field = null;
                foreach (UIElement child in panel.Children)
                {
                    if (label == null && child is TextBlock) label = (TextBlock)child;
                    else if (field == null && child is TextBox) field = (TextBox)child;
                }
                if (label != null && field != null && !fieldErrors.ContainsKey(field))
                {
                    AutomationProperties.SetName(field, label.Text);
                    panel.Children.Add(MakeError());
                    fieldErrors[field] = (TextBlock)panel.Children[panel.Children.Count - 1];
                }
            }
            List<DependencyObject> children = new List<DependencyObject>();
            foreach (object child in LogicalTreeHelper.GetChildren(node)) if (child is DependencyObject) children.Add((DependencyObject)child);
            foreach (DependencyObject child in children) AddBlockErrors(child);
        }
        static TextBlock MakeError()
        {
            return new TextBlock { Foreground = Brushes.Firebrick, FontSize = 12, TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed, Margin = new Thickness(0, 3, 0, 0) };
        }
        static void CollectTextBoxes(DependencyObject node, List<TextBox> found)
        {
            TextBox box = node as TextBox;
            if (box != null) { found.Add(box); return; }
            foreach (object child in LogicalTreeHelper.GetChildren(node))
                if (child is DependencyObject) CollectTextBoxes((DependencyObject)child, found);
        }
        static TextBlock FirstTextBlock(DependencyObject node)
        {
            TextBlock text = node as TextBlock;
            if (text != null) return text;
            foreach (object child in LogicalTreeHelper.GetChildren(node))
                if (child is DependencyObject) { TextBlock found = FirstTextBlock((DependencyObject)child); if (found != null) return found; }
            return null;
        }
        /// <summary>SettingsCard 里的输入框把错误提示挂在左侧标题/描述下方，保持同一左线。</summary>
        void AddCardErrors(DependencyObject node)
        {
            Grid card = node as Grid;
            if (card != null && card.Children.Count == 2 && IsSettingsCard(card))
            {
                Panel host = card.Children[0] as Panel;
                TextBlock header = host == null ? card.Children[0] as TextBlock : FirstTextBlock(host);
                List<TextBox> boxes = new List<TextBox>();
                CollectTextBoxes(card.Children[1], boxes);
                foreach (TextBox box in boxes)
                {
                    if (host == null || fieldErrors.ContainsKey(box)) continue;
                    if (header != null && string.IsNullOrEmpty(AutomationProperties.GetName(box))) AutomationProperties.SetName(box, header.Text);
                    TextBlock error = MakeError();
                    host.Children.Add(error);
                    fieldErrors[box] = error;
                }
            }
            List<DependencyObject> children = new List<DependencyObject>();
            foreach (object child in LogicalTreeHelper.GetChildren(node)) if (child is DependencyObject) children.Add((DependencyObject)child);
            foreach (DependencyObject child in children) AddCardErrors(child);
        }
        static bool IsSettingsCard(Grid card)
        {
            return object.Equals(card.Tag, "SettingsCard") || object.Equals(card.Tag, "SettingsCardStacked");
        }
        void ShowWebhookGuide()
        {
            if (!ApplySection(SettingsSection.Sms)) return;
            string ip = txtPcIp.Text.Trim();
            CodepassDialog.CreateWebhookGuide(this, "http://" + ip + ":" + cfg.Port + "/sms").ShowDialog();
        }
    }
}
