using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

        void InitializeUsability(FrameworkElement root)
        {
            initialNetworkSignature = NetworkSignature(cfg);
            Wire(root, "btnMax", delegate { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; });
            Wire(root, "btnEmailSave", delegate { QueueSection(SettingsSection.Email); });
            Wire(root, "btnCopyLatest", delegate { if (lockOverlay.Visibility != Visibility.Visible) Program.CopyLatestCode(); });
            Wire(root, "btnNewRecords", delegate { RefreshHistory(); historyScroll.ScrollToTop(); Find<Button>(root, "btnNewRecords").Visibility = Visibility.Collapsed; });
            Wire(root, "btnDisableLock", delegate { DisablePasswordLock(); });
            Wire(root, "btnWebhook", delegate { ShowWebhookGuide(); });
            PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (lockOverlay.Visibility == Visibility.Visible) return;
                if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
                { SelectSection("history"); txtHistorySearch.Focus(); txtHistorySearch.SelectAll(); e.Handled = true; }
            };
            IsVisibleChanged += delegate { if (!IsVisible) CloseHistoryActions(); else if (historyDirty) RefreshHistory(); };
            foreach (TextBox box in new TextBox[] { txtEmailServer, txtEmailAccount })
                box.TextChanged += delegate { InvalidateEmailTest(); };
            fldEmailAuth.Changed += InvalidateEmailTest;
            foreach (TextBox box in new TextBox[] { txtPort, txtPcIp, txtNtfyServer, txtNtfyTopic })
                box.TextChanged += delegate { InvalidateNetworkTests(); };
            fldToken.Changed += InvalidateNetworkTests; fldNtfyToken.Changed += InvalidateNetworkTests;
            AddFieldErrors(root);
            InitializeFieldActionRows(root);
            Loaded += delegate { UpdateReceiveSummary(); UpdateCompactShell(); };
            SizeChanged += delegate { UpdateCompactShell(); };
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
                    if (CanShowTestResult(version, networkDraftVersion)) SettingsFeedback(result.Message, result.State == OperationState.Failed);
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
        void UpdateSettingsFieldWidth(bool compact)
        {
            double width = Math.Min(320, Math.Max(140, settingsScroll.ActualWidth - 100 - (compact ? 0 : 150)));
            FrameworkElement root = (FrameworkElement)Content;
            if (Math.Abs((double)root.Resources["SettingsInputWidth"] - width) > 0.5) root.Resources["SettingsInputWidth"] = width;
            foreach (Grid row in fieldActionRows) ArrangeFieldActions(row);
        }
        void ArrangeFieldActions(Grid row)
        {
            if (row.Children.Count != 2 || row.ActualWidth <= 0) return;
            double width = (double)((FrameworkElement)Content).Resources["SettingsInputWidth"];
            bool stacked = row.ActualWidth < width + 252;
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
            if (grid != null && grid.ColumnDefinitions.Count == 2)
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
                    grid.Children.Remove(field);
                    StackPanel wrapper = new StackPanel { Margin = margin };
                    field.Margin = new Thickness(0); Grid.SetRow(wrapper, row); Grid.SetColumn(wrapper, 1);
                    wrapper.Children.Add(field); wrapper.Children.Add(error); grid.Children.Add(wrapper);
                    if (row < grid.RowDefinitions.Count) grid.RowDefinitions[row].Height = GridLength.Auto;
                    fieldErrors[field] = error;
                }
            }
            List<DependencyObject> children = new List<DependencyObject>();
            foreach (object child in LogicalTreeHelper.GetChildren(node)) if (child is DependencyObject) children.Add((DependencyObject)child);
            foreach (DependencyObject child in children) AddFieldErrors(child);
        }
        void ShowWebhookGuide()
        {
            if (!ApplySection(SettingsSection.Sms)) return;
            string ip = txtPcIp.Text.Trim();
            CodepassDialog.CreateWebhookGuide(this, "http://" + ip + ":" + cfg.Port + "/sms").ShowDialog();
        }
    }
}
