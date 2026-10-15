using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Interop;
using System.Windows.Shell;
using System.Windows.Threading;
using MessageBox = Codepass.CodepassDialog;

namespace Codepass
{
    sealed partial class MainWindow
    {
        // ---------- 记录 ----------
        ContextMenu historyActionsMenu;

        void SelectHistoryChannel(string channel)
        {
            historyChannel = channel == "email" ? "email" : channel == "sms" ? "sms" : "all";
            RefreshHistory();
            if (historyScroll != null) historyScroll.ScrollToTop();
        }

        public void RefreshHistory()
        {
            if (listHost == null) return;
            CloseHistoryActions();
            EnsureHistoryScroll();
            historyDirty = false;
            if (historyRefreshTimer != null) historyRefreshTimer.Stop();
            double offset = historyScroll == null ? 0 : historyScroll.VerticalOffset;
            selectedRow = null;
            List<HistoryItem> visible = new List<HistoryItem>();
            int smsCount = 0, emailCount = 0;
            foreach (HistoryItem it in History.Snapshot())
            {
                bool email = it.Channel == "email";
                if (email) emailCount++; else smsCount++;
                if (HistorySearch.Matches(it, historyChannel, txtHistorySearch == null ? "" : txtHistorySearch.Text)) visible.Add(it);
            }
            listHost.ItemsSource = null;
            listHost.ItemsSource = visible;
            SetNavState(tabSmsHistory, historyChannel == "sms");
            SetNavState(tabEmailHistory, historyChannel == "email");
            SetNavState(tabAllHistory, historyChannel == "all");
            if (tabAllHistory != null) tabAllHistory.Content = "全部 (" + (smsCount + emailCount) + ")";
            if (tabSmsHistory != null) tabSmsHistory.Content = "短信 (" + smsCount + ")";
            if (tabEmailHistory != null) tabEmailHistory.Content = "邮件 (" + emailCount + ")";
            if (lblCount != null)
                lblCount.Text = "显示 " + visible.Count + " / " + (smsCount + emailCount) + " 条 · 最多 " + History.Max + " 条";
            if (lblHistoryEmpty != null)
            {
                lblHistoryEmpty.Text = txtHistorySearch != null && txtHistorySearch.Text.Trim().Length > 0
                    ? "没有匹配的记录，请调整搜索词或重置筛选"
                    : historyChannel == "email" ? "暂无邮箱验证码记录" : historyChannel == "sms" ? "暂无短信验证码记录" : "暂无验证码记录";
                lblHistoryEmpty.Visibility = visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            if (historyScroll != null)
            {
                listHost.UpdateLayout();
                historyScroll.ScrollToVerticalOffset(offset);
            }
            UpdateReceiveSummary();
            Button news = Find<Button>((FrameworkElement)Content, "btnNewRecords");
            if (news != null && offset <= 1) news.Visibility = Visibility.Collapsed;
        }

        internal void EnsureHistoryScroll()
        {
            if (historyScroll != null || listHost == null) return;
            listHost.ApplyTemplate();
            historyScroll = FindVisualChild<ScrollViewer>(listHost);
        }

        static T FindVisualChild<T>(DependencyObject root) where T : class
        {
            if (root == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                T found = child as T;
                if (found != null) return found;
                found = FindVisualChild<T>(child);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>窗口隐藏时释放历史页动态控件；记录数据保留，下次显示时完整重建。</summary>
        internal void ReleaseHistoryRows()
        {
            CloseHistoryActions();
            if (listHost != null) listHost.ItemsSource = null;
            selectedRow = null;
            selectedHistoryRow = null;
            historyDirty = true;
            searchChanged = false;
            if (historyRefreshTimer != null) historyRefreshTimer.Stop();
        }

        internal void MarkHistoryDirty()
        {
            historyDirty = true;
        }

        Border BuildRow(HistoryItem it)
        {
            Grid g = new Grid();
            AddCol(g, 112, false);
            AddCol(g, 1, true);
            AddCol(g, 100, false);
            AddCol(g, 2, true);
            AddCol(g, 72, false);

            g.Children.Add(Cell(it.Time, 0, FgBody, false));
            TextBlock senderCell = Cell((it.Channel == "email" ? "邮件 · " : "短信 · ") + it.Source, 1, FgBody, false);
            senderCell.ToolTip = String.IsNullOrEmpty(it.Source) ? null : it.Source;
            g.Children.Add(senderCell);
            HistoryItem latest = History.LatestUnexpiredRecord(cfg.HistoryRetentionMinutes, DateTime.UtcNow);
            Button codeCell = new Button { Content = it.Code, Foreground = object.ReferenceEquals(it, latest) ? AccentTx : FgBody, FontWeight = FontWeights.SemiBold,
                Style = ((FrameworkElement)Content).Resources["SecondaryBtn"] as Style, Margin = new Thickness(4, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(codeCell, 2);
            System.Windows.Automation.AutomationProperties.SetName(codeCell, "复制此条验证码");
            g.Children.Add(codeCell);
              TextBlock rawCell = Cell(string.IsNullOrEmpty(it.Raw) && it.Channel == "email" ? "未保存正文" : it.Raw, 3, FgFaint, false);
              rawCell.ToolTip = "查看原文";
              rawCell.ToolTipOpening += delegate
              {
                  EnsureRawTooltip(rawCell, it);
              };
             ToolTipService.SetInitialShowDelay(rawCell, 300);
             ToolTipService.SetShowDuration(rawCell, 30000);
             rawCell.Background = Brushes.Transparent;
             rawCell.Cursor = Cursors.Hand;
             rawCell.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs args)
             {
                 if (args.ClickCount != 2) return;
                 ShowHistoryRaw(it);
                 args.Handled = true;
             };
              g.Children.Add(rawCell);
            Button actions = new Button { Content = "操作 ▾", Style = ((FrameworkElement)Content).Resources["SecondaryBtn"] as Style,
                Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(actions, 4); actions.Click += delegate { ShowHistoryActions(it, actions); };
            System.Windows.Automation.AutomationProperties.SetName(actions, "记录操作：查看原文或删除此条");
            actions.ToolTip = "查看原文或删除这条记录";
            g.Children.Add(actions);

            Border row = new Border();
            row.Height = 40;
            row.Margin = new Thickness(0, 2, 0, 2);
            row.CornerRadius = new CornerRadius(4);
            row.Background = Brushes.Transparent;
            row.Cursor = Cursors.Hand;
            row.Child = g;
            row.ToolTip = "单击验证码可复制；双击原文查看全部内容";

            string code = it.Code;
            codeCell.Cursor = Cursors.Hand;
            codeCell.ToolTip = it.CanBeLatest ? "单击复制验证码" : "仅历史：复查/首次补查不更新最新验证码；仍可单击手动复制";
            codeCell.Click += delegate(object s, RoutedEventArgs e)
            {
                if (string.IsNullOrEmpty(code) || it.IsDeleted || lockOverlay.Visibility == Visibility.Visible) return;
                // 只复制验证码；点击“原文”等区域不再占用剪贴板（避免点击卡顿）。
                try
                {
                    if (Program.Clipboard.CopyManually(code, new WindowInteropHelper(this).Handle)) ShowToast("已复制验证码 " + code);
                    else ShowToast("复制失败，剪贴板被占用，请重试");
                }
                catch { ShowToast("复制失败，剪贴板被占用，请重试"); }
            };
            row.MouseEnter += delegate { if (row != selectedRow) row.Background = RowHover; };
            row.MouseLeave += delegate { if (row != selectedRow) row.Background = Brushes.Transparent; };
            row.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                if (selectedRow != null) selectedRow.Background = Brushes.Transparent;
                selectedRow = row;
                row.Background = RowSel;
            };
            return row;
        }

        internal void SelectHistoryRow(HistoryRowControl row)
        {
            if (selectedHistoryRow != null && !ReferenceEquals(selectedHistoryRow, row)) selectedHistoryRow.Background = Brushes.Transparent;
            selectedHistoryRow = row;
            if (row != null) row.Background = RowSel;
        }

        internal string HistoryPreviewText(HistoryItem item)
        {
            string text = item == null ? "" : (item.Raw ?? "");
            if (text.Length <= 240) return text;
            return text.Substring(0, 240) + "…";
        }

        internal void CopyHistoryCode(HistoryItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.Code) || item.IsDeleted || lockOverlay.Visibility == Visibility.Visible) return;
            try
            {
                if (Program.Clipboard.CopyManually(item.Code, new WindowInteropHelper(this).Handle)) ShowToast("已复制验证码 " + item.Code);
                else ShowToast("复制失败，剪贴板被占用，请重试");
            }
            catch { ShowToast("复制失败，剪贴板被占用，请重试"); }
        }

        internal void EnsureHistoryRawTooltip(TextBlock cell, HistoryItem item)
        { EnsureRawTooltip(cell, item); }

        internal void ShowHistoryRawFromRow(HistoryItem item)
        { ShowHistoryRaw(item); }

        internal void ShowHistoryActionsFromRow(HistoryItem item, Button anchor)
        { ShowHistoryActions(item, anchor); }

        void ShowHistoryActions(HistoryItem item, Button anchor)
        {
            if (item.IsDeleted || lockOverlay.Visibility == Visibility.Visible) return;
            CloseHistoryActions();
            FrameworkElement root = (FrameworkElement)Content;
            ContextMenu menu = new ContextMenu { Style = root.Resources["FlyoutMenu"] as Style,
                ItemContainerStyle = root.Resources["FlyoutItem"] as Style, PlacementTarget = anchor, Placement = PlacementMode.Bottom };
            MenuItem view = new MenuItem { Header = "查看原文" };
            view.Click += delegate { CloseHistoryActions(); ShowHistoryRaw(item); };
            MenuItem remove = new MenuItem { Header = "删除此条" };
            remove.Click += delegate { CloseHistoryActions(); DeleteHistoryRecord(item); };
            menu.Items.Add(view); menu.Items.Add(remove);
            menu.Closed += delegate { if (historyActionsMenu == menu) historyActionsMenu = null; };
            historyActionsMenu = menu;
            menu.IsOpen = true;
        }

        void CloseHistoryActions()
        {
            ContextMenu menu = historyActionsMenu;
            historyActionsMenu = null;
            if (menu != null) menu.IsOpen = false;
        }

        void DeleteHistoryRecord(HistoryItem item)
        {
            if (lockOverlay.Visibility == Visibility.Visible) return;
            try
            {
                bool removed = History.Remove(item);
                RefreshHistory();
                ShowToast(removed ? "已删除这条记录" : "这条记录已不存在");
                if (txtHistorySearch != null) txtHistorySearch.Focus();
            }
            catch (IOException) { ShowToast("删除失败，原记录已保留；请先解决历史保存错误"); }
        }

        static void AddCol(Grid g, double width, bool star)
        {
            ColumnDefinition c = new ColumnDefinition();
            if (star) c.Width = new GridLength(width > 0 ? width : 1, GridUnitType.Star);
            else c.Width = new GridLength(width);
            g.ColumnDefinitions.Add(c);
        }
        static void EnsureRawTooltip(TextBlock cell, HistoryItem item)
        {
            if (cell.ToolTip is ToolTip) return;
            cell.ToolTip = new ToolTip { MaxWidth = 560, Content = new ScrollViewer { MaxHeight = 320,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = new TextBlock { Text = HistoryRawText(item), TextWrapping = TextWrapping.Wrap, MaxWidth = 520 } } };
        }

        static TextBlock Cell(string text, int col, Brush fg, bool bold)
        {
            TextBlock t = new TextBlock();
            t.Text = text;
            t.FontSize = 13;
            t.Foreground = fg;
            t.VerticalAlignment = VerticalAlignment.Center;
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.TextAlignment = TextAlignment.Center;
            t.TextTrimming = TextTrimming.CharacterEllipsis;
            if (col == 1 || col == 3)
            {
                t.HorizontalAlignment = HorizontalAlignment.Stretch;
                t.TextAlignment = col == 1 ? TextAlignment.Center : TextAlignment.Left;
                t.Margin = new Thickness(8, 0, 8, 0);
            }
            if (bold) t.FontWeight = FontWeights.SemiBold;
            Grid.SetColumn(t, col);
            return t;
        }

        static string HistoryRawText(HistoryItem item)
        {
            if (!string.IsNullOrEmpty(item.Raw)) return item.Raw;
            return item.Channel == "email" ? "这条邮件未保存正文。可在邮箱设置中开启“在记录中保存邮件内容”，仅对之后收到的邮件生效。"
                : "这条记录没有原文内容。";
        }

        Window CreateHistoryRawWindow(HistoryItem item)
        {
            Window dialog = new Window
            {
                Title = "记录原文", Width = 760, Height = 520, MinWidth = 480, MinHeight = 300,
                Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false, ResizeMode = ResizeMode.CanResize,
                Background = MakeBrush(0xF3, 0xF3, 0xF3),
                FontFamily = FontFamily, FontSize = 14, Icon = Icon
            };
            Grid layout = new Grid { Margin = new Thickness(20) };
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.Children.Add(new TextBlock { Text = item.Time + "  ·  " + item.Source,
                TextWrapping = TextWrapping.Wrap, Foreground = FgBody, Margin = new Thickness(0, 0, 0, 12) });
            TextBox original = new TextBox
            {
                Name = "txtHistoryRawFull", Text = HistoryRawText(item), IsReadOnly = true,
                AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(12), Background = Brushes.White, Foreground = FgBody,
                BorderBrush = MakeBrush(0xDD, 0xDD, 0xDD), BorderThickness = new Thickness(1)
            };
            Grid.SetRow(original, 1); layout.Children.Add(original);
            Button close = new Button { Content = "关闭", MinWidth = 84, Height = 34,
                HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            FrameworkElement root = Content as FrameworkElement;
            if (root != null) close.Style = root.Resources["SecondaryBtn"] as Style;
            close.Click += delegate { dialog.Close(); };
            Grid.SetRow(close, 2); layout.Children.Add(close);
            dialog.Content = layout;
            dialog.PreviewKeyDown += delegate(object sender, KeyEventArgs args)
            {
                if (args.Key == Key.Escape) { dialog.Close(); args.Handled = true; }
            };
            historyRawWindow = dialog;
            dialog.Closed += delegate { if (historyRawWindow == dialog) historyRawWindow = null; };
            dialog.StateChanged += delegate
            {
                if (dialog.WindowState == WindowState.Minimized && cfg.ShouldAutoLock) LockWindow();
            };
            return dialog;
        }

        void ShowHistoryRaw(HistoryItem item)
        {
            if (item.IsDeleted || (lockOverlay != null && lockOverlay.Visibility == Visibility.Visible)) return;
            Window dialog = CreateHistoryRawWindow(item);
            dialog.ShowDialog();
        }

        public void OnNewRecord(HistoryItem it)
        {
            UpdateReceiveSummary();
            if (historyScroll != null && historyScroll.VerticalOffset > 1)
            {
                Button news = Find<Button>((FrameworkElement)Content, "btnNewRecords");
                if (news != null) news.Visibility = Visibility.Visible;
            }
            ScheduleHistoryRefresh(false);
        }

        // 页面内轻提示：显示约 1.4 秒后淡出, 用于复制成功/失败反馈。
        void ShowToast(string message)
        {
            if (toastBox == null) return;
            if (toastText != null) toastText.Text = message;
            toastBox.BeginAnimation(UIElement.OpacityProperty, null);
            toastBox.Opacity = 1;
            toastBox.Visibility = Visibility.Visible;
            if (toastTimer == null)
            {
                toastTimer = new DispatcherTimer();
                toastTimer.Interval = TimeSpan.FromMilliseconds(1400);
                toastTimer.Tick += delegate
                {
                    toastTimer.Stop();
                    if (toastBox == null) return;
                    if (!SystemParameters.ClientAreaAnimation) { toastBox.Visibility = Visibility.Collapsed; return; }
                    DoubleAnimation fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
                    fade.Completed += delegate
                    {
                        if (toastBox != null) toastBox.Visibility = Visibility.Collapsed;
                    };
                    toastBox.BeginAnimation(UIElement.OpacityProperty, fade);
                };
            }
            toastTimer.Stop();
            toastTimer.Start();
        }

        // 立即隐藏轻提示（例如锁定窗口前）。
        void HideToast()
        {
            if (toastTimer != null) toastTimer.Stop();
            if (toastBox == null) return;
            toastBox.BeginAnimation(UIElement.OpacityProperty, null);
            toastBox.Opacity = 1;
            toastBox.Visibility = Visibility.Collapsed;
        }
    }
}
