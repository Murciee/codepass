// 柔和圆角界面 —— WPF, 纯代码 + 运行时解析 XAML, 无需 XAML 编译器
// 窗口为无边框 + 像素透明, 自绘圆角外壳与顶栏 (Win10 也能得到真圆角)

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
using System.Windows.Automation;
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
    sealed partial class MainWindow : Window
    {
        readonly Config cfg;
        readonly string cfgPath;

         TextBox txtPort, txtPcIp, txtNtfyServer, txtNtfyTopic, txtMin, txtMax, txtClear, txtFilterKeywords, txtFilterRegex;
         TextBox txtSmsMaxLength, txtAcceptKeywords;
         SecretField fldToken, fldNtfyToken;
         CheckBox chkTip, chkStartup, chkAutoLock;
        ComboBox cmbPrivacy, cmbEmailPrivacy;
        EmailSettingsPage emailPage;
        TextBox txtAcceptKeywordAdd;
        WrapPanel acceptKeywordTags;
        TextBlock lblStorageError;
        Button btnTestPc, btnTestNtfy, btnLock;
        TextBlock lblPcStatus;
        ListBox listHost;
        TextBlock lblCount, lblHistoryEmpty;
        TextBox txtHistorySearch;
        TextBox txtHistoryRetention;
        Button tabAllHistory, tabSmsHistory, tabEmailHistory;
        ScrollViewer historyScroll;
        string historyChannel = "all";
        readonly List<ResponsiveForm> responsiveForms = new List<ResponsiveForm>();
          Button navHistory, navGeneral, navEmail, navSecurity, navAbout;
         Grid pageSet, pageLog, pageAbout;
         ScrollViewer settingsScroll;
            Border cardNetwork, cardGeneral, cardNotify, cardSecurity, cardMore;
        Border cardNtfy, cardSmsFilters;
        Border dragBar;
        Border lockOverlay;
         PasswordBox txtUnlockPassword;
         TextBox txtUnlockPlain;
         Button btnUnlockEye;
         PasswordRevealer unlockRevealer;
         TextBlock lblUnlockError;

        Border toastBox;
        TextBlock toastText;
        DispatcherTimer toastTimer;

         Button btnGithub, btnUpdate;
         TextBlock lblAboutVersion, lblUpdateStatus;
         bool updateBusy;
         bool applying;
         bool loadingValues;
         DispatcherTimer windowSizeTimer;
         Button btnMinCaption, btnMaxCaption, btnCloseCaption;
         WindowChrome windowChrome;
         HwndSource captionSource;
         string autoPcIp = "";
         bool settingsRecoveryRequired;

         Border selectedRow;
         HistoryRowControl selectedHistoryRow;
         bool allowClose = false;
         bool closeCompleted;
         Window historyRawWindow, diagnosticsWindow;
         bool wasMinimized;

        static readonly Brush FgBody   = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : MakeBrush(0x1C, 0x1C, 0x1C);
        static readonly Brush AccentTx = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : MakeBrush(0x00, 0x78, 0xD4);
        static readonly Brush FgFaint  = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : MakeBrush(0x61, 0x61, 0x61);
        static readonly Brush RowHover = SystemParameters.HighContrast ? SystemColors.ControlBrush : MakeBrush(0xF5, 0xF5, 0xF5);
        static readonly Brush RowSel   = SystemParameters.HighContrast ? SystemColors.ControlBrush : MakeBrush(0xE9, 0xE9, 0xE9);
        internal static Brush BodyBrush { get { return FgBody; } }
        internal static Brush AccentBrush { get { return AccentTx; } }
        internal static Brush FaintBrush { get { return FgFaint; } }
        internal static Brush RowHoverBrush { get { return RowHover; } }
        internal static Brush RowSelectedBrush { get { return RowSel; } }
        internal Config Configuration { get { return cfg; } }
        internal HistoryRowControl SelectedHistoryRow { get { return selectedHistoryRow; } }
        internal ScrollViewer HistoryScroll { get { EnsureHistoryScroll(); return historyScroll; } }

        static Brush MakeBrush(byte r, byte g, byte b)
        {
            SolidColorBrush b2 = new SolidColorBrush(Color.FromRgb(r, g, b));
            b2.Freeze();
            return b2;
        }



        // ------------------------------------------------------------------

        public MainWindow(Config cfg, string cfgPath)
        {
            this.cfg = cfg;
            this.cfgPath = cfgPath;

             Title = "codepass";
            WindowStyle = WindowStyle.None;
             AllowsTransparency = false;
                ResizeMode = ResizeMode.CanResize;
             Background = MakeBrush(0xF3, 0xF3, 0xF3);
              MinWidth = Math.Min(640, Math.Max(360, SystemParameters.WorkArea.Width - 24));
             MinHeight = Math.Min(460, Math.Max(300, SystemParameters.WorkArea.Height - 24));
             ApplyConfiguredWindowSize();
             WindowChrome chrome = new WindowChrome();
             chrome.CaptionHeight = 0;
             chrome.ResizeBorderThickness = new Thickness(10);
             chrome.CornerRadius = new CornerRadius(8);
             chrome.GlassFrameThickness = new Thickness(0);
             WindowChrome.SetWindowChrome(this, chrome);
             windowChrome = chrome;
             SourceInitialized += delegate { WindowShape.Apply(this); WindowShape.ApplyModernFrame(this); WindowShape.ApplyMica(this, Content as Panel); HookCaptionHitTest(); UpdateCaptionButtons(); };
             SizeChanged += delegate { ScheduleWindowSizeSave(); WindowShape.Apply(this); };
             LocationChanged += delegate { ScheduleWindowSizeSave(); };
             StateChanged += delegate { WindowShape.Apply(this); UpdateCaptionButtons(); };
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
             ApplyConfiguredWindowPosition();
            FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI");
            FontSize = 13.5;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
             System.Windows.Media.Imaging.BitmapSource iconSource = null;
             System.Drawing.Icon ico = null;
             try
             {
                  ico = System.Drawing.Icon.ExtractAssociatedIcon(
                      System.Reflection.Assembly.GetExecutingAssembly().Location);
                  if (ico == null) ico = (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();
                 iconSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                     ico.Handle, Int32Rect.Empty,
                     System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                 iconSource.Freeze();
                 Icon = iconSource;
             }
             catch
             {
                 try
                 {
                     if (ico != null) ico.Dispose();
                     ico = (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();
                     iconSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                         ico.Handle, Int32Rect.Empty,
                         System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                     iconSource.Freeze();
                     Icon = iconSource;
                 }
                 catch { }
             }
             finally
             {
                 if (ico != null) ico.Dispose();
             }

             Grid root = (Grid)XamlReader.Parse(UiAppearance.Prepare(UiXaml, SystemParameters.HighContrast, SystemParameters.ClientAreaAnimation));
             Content = root;
             Image appLogo = Find<Image>(root, "imgAppLogo");
             if (appLogo != null && iconSource != null) appLogo.Source = iconSource;

             txtPort       = Find<TextBox>(root, "txtPort");
             txtHistorySearch = Find<TextBox>(root, "txtHistorySearch");
             txtHistoryRetention = Find<TextBox>(root, "txtHistoryRetention");
             tabAllHistory = Find<Button>(root, "tabAllHistory");
             cardNtfy = Find<Border>(root, "cardNtfy");
             cardSmsFilters = Find<Border>(root, "cardSmsFilters");
             txtPcIp       = Find<TextBox>(root, "txtPcIp");
              txtNtfyServer = Find<TextBox>(root, "txtNtfyServer");
             txtNtfyTopic  = Find<TextBox>(root, "txtNtfyTopic");
            txtMin        = Find<TextBox>(root, "txtMin");
            txtSmsMaxLength = Find<TextBox>(root, "txtSmsMaxLength");
              txtMax        = Find<TextBox>(root, "txtMax");
              txtClear      = Find<TextBox>(root, "txtClear");
              txtFilterKeywords = Find<TextBox>(root, "txtFilterKeywords");
              txtFilterRegex = Find<TextBox>(root, "txtFilterRegex");
              txtAcceptKeywords = Find<TextBox>(root, "txtAcceptKeywords");
              chkTip        = Find<CheckBox>(root, "chkTip");
              chkStartup    = Find<CheckBox>(root, "chkStartup");
              chkAutoLock   = Find<CheckBox>(root, "chkAutoLock");
              cmbPrivacy    = Find<ComboBox>(root, "cmbPrivacy");
              cmbEmailPrivacy = Find<ComboBox>(root, "cmbEmailPrivacy");
             btnTestPc     = Find<Button>(root, "btnTestPc");
             lblPcStatus   = Find<TextBlock>(root, "lblPcStatus");
             btnTestNtfy   = Find<Button>(root, "btnTestNtfy");
             btnLock       = Find<Button>(root, "btnLock");
             listHost      = Find<ListBox>(root, "listHost");
             if (listHost != null) listHost.Tag = this;
             lblCount      = Find<TextBlock>(root, "lblCount");
             lblHistoryEmpty = Find<TextBlock>(root, "lblHistoryEmpty");
             tabSmsHistory = Find<Button>(root, "tabSmsHistory");
             tabEmailHistory = Find<Button>(root, "tabEmailHistory");
             historyScroll = Find<ScrollViewer>(root, "historyScroll");
            navHistory   = Find<Button>(root, "navHistory");
             navGeneral   = Find<Button>(root, "navGeneral");
             navEmail     = Find<Button>(root, "navEmail");
            navSecurity  = Find<Button>(root, "navSecurity");
            navAbout     = Find<Button>(root, "navAbout");
            pageSet       = Find<Grid>(root, "pageSet");
            pageLog       = Find<Grid>(root, "pageLog");
            pageAbout     = Find<Grid>(root, "pageAbout");
            settingsScroll = Find<ScrollViewer>(root, "settingsScroll");
            cardGeneral   = Find<Border>(root, "cardGeneral");
            cardNetwork   = Find<Border>(root, "cardNetwork");
            cardNotify    = Find<Border>(root, "cardNotify");
            cardSecurity  = Find<Border>(root, "cardSecurity");
            cardMore      = Find<Border>(root, "cardMore");
                acceptKeywordTags = Find<WrapPanel>(root, "acceptKeywordTags");
                txtAcceptKeywordAdd = Find<TextBox>(root, "txtAcceptKeywordAdd");
             dragBar       = Find<Border>(root, "dragBar");
             btnMinCaption = Find<Button>(root, "btnMin");
             btnMaxCaption = Find<Button>(root, "btnMax");
             btnCloseCaption = Find<Button>(root, "btnClose");
             if (btnMinCaption != null) btnMinCaption.ToolTip = "最小化";
             if (btnCloseCaption != null) btnCloseCaption.ToolTip = "隐藏到托盘";
             lockOverlay   = Find<Border>(root, "lockOverlay");
             txtUnlockPassword = Find<PasswordBox>(root, "txtUnlockPassword");
             txtUnlockPlain = Find<TextBox>(root, "txtUnlockPlain");
             btnUnlockEye = Find<Button>(root, "btnUnlockEye");
              if (txtUnlockPassword != null && txtUnlockPlain != null && btnUnlockEye != null)
                  unlockRevealer = new PasswordRevealer(txtUnlockPassword, txtUnlockPlain, btnUnlockEye);
              lblUnlockError = Find<TextBlock>(root, "lblUnlockError");
              toastBox = Find<Border>(root, "toastBox");
              toastText = Find<TextBlock>(root, "toastText");
              btnGithub = Find<Button>(root, "btnGithub");
              btnUpdate = Find<Button>(root, "btnUpdate");
              lblAboutVersion = Find<TextBlock>(root, "lblAboutVersion");
              lblUpdateStatus = Find<TextBlock>(root, "lblUpdateStatus");
              if (lblAboutVersion != null)
                   lblAboutVersion.Text = "codepass v" + Updater.AppVersion;
               lblStorageError = Find<TextBlock>(root, "lblStorageError");
               BuildSecretFields(root);
               InitializeEmailPage(root);
              InitializeUsability(root);
              InitializeResponsiveForms(pageSet);
              settingsScroll.SizeChanged += delegate { UpdateResponsiveForms(); };
              Loaded += delegate { UpdateResponsiveForms(); EnsureHistoryScroll(); if (historyDirty && activeSection == "history") RefreshHistory(); };
             if (btnLock != null) btnLock.Click += delegate { LockWindow(); };

            // 顶栏拖动 + 窗口按钮
            if (dragBar != null)
                dragBar.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
                {
                    // 命中测试接管顶栏时这里不会触发；保留拖动作为兜底，并补上双击最大化/还原。
                    if (e.ClickCount == 2) { ToggleMaximize(); return; }
                    if (e.ButtonState == MouseButtonState.Pressed)
                    {
                        try { DragMove(); } catch { }
                    }
                };
            Wire(root, "btnMin", delegate { WindowState = WindowState.Minimized; });
            Wire(root, "btnClose", delegate { Close(); });

             Wire(root, "btnDetect", delegate(object sender, RoutedEventArgs e) { DetectLocalIp(sender as Button); });
             Wire(root, "btnTestPc", delegate { TestPcConnectivity(); });
             Wire(root, "btnTestNtfy", delegate { TestNtfyConnectivity(); });
             Wire(root, "btnGen", delegate { OnGeneratePhoneConfig(); });
             Wire(root, "btnImport", delegate { OnImport(); });
             Wire(root, "btnExport", delegate { OnExport(); });
             Wire(root, "btnSecurity", delegate { ConfigureSecurity(); });
             Wire(root, "btnUnlock", delegate { TryUnlock(); });
              Wire(root, "btnGithub", delegate { OpenRepoPage(); });
               Wire(root, "btnUpdate", delegate { CheckUpdate(); });
               Wire(root, "tabSmsHistory", delegate { SelectHistoryChannel("sms"); });
               Wire(root, "tabEmailHistory", delegate { SelectHistoryChannel("email"); });
               Wire(root, "tabAllHistory", delegate { SelectHistoryChannel("all"); });
               Wire(root, "btnHistoryReset", delegate { txtHistorySearch.Clear(); SelectHistoryChannel("all"); });
               txtHistorySearch.TextChanged += delegate { ScheduleHistoryRefresh(true); };
               txtHistorySearch.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
               { if (e.Key == Key.Escape) { txtHistorySearch.Clear(); e.Handled = true; } };
               Wire(root, "btnDiagnostics", delegate { ShowDiagnostics(); });
                 Wire(root, "btnAcceptKeywordAdd", delegate { AddAcceptKeywords(); });
                 Wire(root, "btnAcceptKeywordDefault", delegate
                 {
                     if (MessageBox.Show(this, "恢复默认接收关键字（留空表示全部收取）？", "确认", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
                     txtAcceptKeywords.Text = new Config().SmsAcceptKeywords; QueueSection(SettingsSection.SmsRules);
                 });
                 Wire(root, "btnStorageRetry", delegate
                 {
                     if (!ApplySection(SettingsSection.EmailRules)) return;
                     try { cfg.Save(cfgPath); History.RetrySave(); EmailWatcher.Kick(); ShowToast("已重试保存并请求邮件检查"); }
                     catch { ShowToast("保存仍未完成，请检查文件权限与磁盘空间"); }
                     UpdateEmailStatus();
                 });
            Wire(root, "btnClear", delegate
            {
                 if (MessageBox.Show(this, "确定清空全部记录？", "确认",
                        MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
                {
                    try { History.Clear(); RefreshHistory(); }
                    catch (IOException) { ShowToast(History.LastSaveError); }
                }
            });

              if (navHistory != null) navHistory.Click += delegate { SelectSection("history"); };
               if (navGeneral != null) navGeneral.Click += delegate { SelectSection("general"); };
               if (navEmail != null) navEmail.Click += delegate { SelectSection("email"); };
              if (navSecurity != null) navSecurity.Click += delegate { SelectSection("security"); };
              if (navAbout != null) navAbout.Click += delegate { SelectSection("about"); };
             StateChanged += delegate
             {
                 if (WindowState == WindowState.Minimized) wasMinimized = true;
                 else if (wasMinimized)
                 {
                     wasMinimized = false;
                     if (cfg.ShouldAutoLock) LockWindow();
                 }
             };

              LoadValues();
              RefreshHistory();
              WireAutoApply(root);
               txtHistoryRetention.LostFocus += delegate { QueueSection(SettingsSection.Retention); };
              txtHistoryRetention.KeyDown += delegate(object sender, KeyEventArgs e)
               { if (e.Key == Key.Return) { QueueSection(SettingsSection.Retention); e.Handled = true; } };
               WireMultilineScrolling();
              SelectSection("history");
             if (cfg.LockOnOpen) LockWindow();
#if CODEPASS_TEST
             // 测试构建专用：设置 CODEPASS_UPDATE_AUTO=1 时启动即自动检查更新（便于本地联调）
             if (Environment.GetEnvironmentVariable("CODEPASS_UPDATE_AUTO") == "1")
                 Loaded += delegate { CheckUpdate(); };
#endif
        }

         /// <summary>输入完成即应用：文本失焦/回车、勾选切换、下拉选择、密钥框失焦时自动保存。</summary>
         void WireAutoApply(FrameworkElement root)
        {
               TextBox[] boxes = new TextBox[] { txtPort, txtPcIp, txtMin, txtMax, txtClear, txtSmsMaxLength,
                    txtNtfyServer, txtNtfyTopic, txtFilterKeywords, txtFilterRegex, txtAcceptKeywords };
            foreach (TextBox tb in boxes)
            {
                if (tb == null) continue;
                tb.LostFocus += delegate(object sender, RoutedEventArgs e) { QueueSection(SectionFor((FrameworkElement)sender)); };
                 tb.KeyDown += delegate(object s, KeyEventArgs e)
                 {
                     // 多行过滤/邮箱输入框用回车换行，失焦时自动保存；其他输入框回车立即保存。
                     if (e.Key == Key.Return
                          && s != txtFilterKeywords && s != txtFilterRegex && s != txtAcceptKeywords) QueueSection(SectionFor((FrameworkElement)s));
                 };
            }
             if (chkTip != null) chkTip.Click += delegate { QueueSection(SettingsSection.SmsRules); };
             if (chkStartup != null) chkStartup.Click += delegate { QueueSection(SettingsSection.Sms); };
             if (chkAutoLock != null) chkAutoLock.Click += delegate { QueueSection(SettingsSection.Privacy); };
               if (cmbPrivacy != null) cmbPrivacy.SelectionChanged += delegate { QueueSection(SettingsSection.Privacy); };
               if (cmbEmailPrivacy != null) cmbEmailPrivacy.SelectionChanged += delegate { QueueSection(SettingsSection.Privacy); };
               if (fldToken != null) fldToken.Committed += delegate { QueueSection(SettingsSection.Sms); };
               if (fldNtfyToken != null) fldNtfyToken.Committed += delegate { QueueSection(SettingsSection.Ntfy); };
              if (txtPcIp != null) txtPcIp.TextChanged += delegate { if (!loadingValues) autoPcIp = ""; };
                if (txtAcceptKeywords != null) txtAcceptKeywords.TextChanged += delegate { RenderAcceptKeywordTags(); };
               if (txtAcceptKeywordAdd != null)
                {
                    txtAcceptKeywordAdd.KeyDown += delegate(object s, KeyEventArgs e)
                    {
                        if (e.Key == Key.Return) { AddAcceptKeywords(); e.Handled = true; }
                        if (e.Key == Key.Escape) { txtAcceptKeywordAdd.Clear(); txtAcceptKeywordAdd.Visibility = Visibility.Collapsed; e.Handled = true; }
                    };
                    // 收起条件：没有内容又失去焦点（有内容时保留输入框）。
                    txtAcceptKeywordAdd.LostKeyboardFocus += delegate
                    {
                        if (txtAcceptKeywordAdd.Text.Length == 0) txtAcceptKeywordAdd.Visibility = Visibility.Collapsed;
                    };
                }
         }

         void WireMultilineScrolling()
         {
             TextBox[] boxes = { txtFilterKeywords, txtFilterRegex, txtAcceptKeywords };
             foreach (TextBox box in boxes)
                 if (box != null) box.PreviewMouseWheel += ScrollMultilineTextBox;
         }

         static void ScrollMultilineTextBox(object sender, MouseWheelEventArgs e)
         {
             TextBox box = sender as TextBox;
             if (box == null || e.Delta == 0) return;
             box.ApplyTemplate();
             ScrollViewer scroll = box.Template.FindName("PART_ContentHost", box) as ScrollViewer;
             if (scroll == null || scroll.ScrollableHeight <= 0.5) return;
             // 溢出时，包括上下边界，都由输入框消费滚轮，避免意外滚动外层设置页。
             int lines = SystemParameters.WheelScrollLines;
             double step = lines < 0 ? scroll.ViewportHeight : lines * box.FontSize * 1.4;
             box.ScrollToVerticalOffset(box.VerticalOffset - e.Delta / 120.0 * step);
             e.Handled = true;
         }

        static void Wire(FrameworkElement root, string name, RoutedEventHandler h)
        {
            Button b = Find<Button>(root, name);
            if (b != null) b.Click += h;
        }

        static T Find<T>(FrameworkElement root, string name) where T : class
        {
            object o = root.FindName(name);
            if (o == null) o = LogicalTreeHelper.FindLogicalNode(root, name);
            return o as T;
        }

        // 保存表单原始位置，窄窗口只改变布局，不重新创建输入框或触发保存。
        sealed class ResponsiveForm
        {
            internal Grid Grid;
            internal GridLength LabelWidth;
            internal readonly List<GridLength> Rows = new List<GridLength>();
            internal readonly List<FormCell> Cells = new List<FormCell>();
            internal bool Compact;
        }

        sealed class FormCell
        {
            internal FrameworkElement Element;
            internal int Row, Column;
            internal Thickness Margin;
            internal HorizontalAlignment Align;
        }

        void InitializeResponsiveForms(DependencyObject node)
        {
            Grid grid = node as Grid;
             if (grid != null && grid.ColumnDefinitions.Count == 2
                 && grid.ColumnDefinitions[0].Width.IsAbsolute && grid.ColumnDefinitions[1].Width.IsStar)
             {
                grid.ColumnDefinitions[0].Width = new GridLength(150);
                ResponsiveForm form = new ResponsiveForm { Grid = grid, LabelWidth = new GridLength(150) };
                if (grid.RowDefinitions.Count == 0) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                foreach (RowDefinition row in grid.RowDefinitions) { row.Height = GridLength.Auto; form.Rows.Add(GridLength.Auto); }
                foreach (UIElement child in grid.Children)
                {
                    FrameworkElement element = child as FrameworkElement;
                    if (element != null)
                    {
                        // 表单行的垂直位置只在这里决定：左列标签与右列输入框对齐到同一行中心。
                        // 标签样式（FieldLabel）不得再自带 Height / Padding / Margin，否则会整体偏离。
                        element.VerticalAlignment = VerticalAlignment.Top;
                        element.Margin = Grid.GetColumn(element) == 0 ? new Thickness(0, 14, 14, 0) : new Thickness(0, 6, 0, 8);
                        form.Cells.Add(new FormCell { Element = element, Row = Grid.GetRow(element), Column = Grid.GetColumn(element), Margin = element.Margin, Align = element.HorizontalAlignment });
                    }
                }
                responsiveForms.Add(form);
            }
            foreach (object child in LogicalTreeHelper.GetChildren(node))
                if (child is DependencyObject) InitializeResponsiveForms((DependencyObject)child);
        }

        void UpdateResponsiveForms()
        {
            if (settingsScroll == null || settingsScroll.ActualWidth <= 0) return;
             bool compact = settingsScroll.ActualWidth < 520;
             UpdateSettingsFieldWidth(compact);
             ArrangeSettingsCards(compact);
            foreach (ResponsiveForm form in responsiveForms)
            {
                if (form.Compact == compact) continue;
                form.Compact = compact;
                form.Grid.ColumnDefinitions[0].Width = compact ? new GridLength(0) : form.LabelWidth;
                form.Grid.RowDefinitions.Clear();
                int rows = Math.Max(1, form.Rows.Count);
                for (int i = 0; i < (compact ? rows * 2 : rows); i++)
                    form.Grid.RowDefinitions.Add(new RowDefinition { Height = compact ? GridLength.Auto : form.Rows.Count == 0 ? GridLength.Auto : form.Rows[i] });
                foreach (FormCell cell in form.Cells)
                {
                    Grid.SetColumn(cell.Element, compact ? 1 : cell.Column);
                    Grid.SetRow(cell.Element, compact ? cell.Row * 2 + (cell.Column == 0 ? 0 : 1) : cell.Row);
                    cell.Element.Margin = compact ? cell.Column == 0 ? new Thickness(0, 10, 0, 4) : new Thickness(0, 0, 0, 8) : cell.Margin;
                    // 宽窗口靠右对齐（iOS 风格）；窄窗口标签移到上方，控件跟着回到左侧，勾选项随文字排列。
                    cell.Element.HorizontalAlignment = compact && (cell.Align == HorizontalAlignment.Right || cell.Element is CheckBox)
                        ? HorizontalAlignment.Left : cell.Align;
                }
            }
        }


        // ---------- 托盘窗口行为 ----------

        public void ShowFromTray()
        {
            Show();
            ShowInTaskbar = true;
            WindowState = WindowState.Normal;
            Activate();
            Topmost = true;
            Topmost = false;
        }

        internal void ClearHistoryFromTray()
        {
            if (MessageBox.Show(this, "确定清空全部记录？", "确认",
                   MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
            {
                try
                {
                    History.Clear();
                    if (IsVisible) RefreshHistory();
                    else
                    {
                        MarkHistoryDirty();
                        UpdateReceiveSummary();
                    }
                }
                catch (IOException)
                {
                    if (IsVisible) ShowToast(History.LastSaveError);
                    else Log.Write("clear history from tray failed: " + (History.LastSaveError ?? "storage error"));
                }
            }
        }

         public bool ExitApp()
         {
             allowClose = true;
             closeCompleted = false;
             Close();
             return closeCompleted;
         }

          protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
          {
                 CancelConnectionTests();
                FlushSettings();
               // 保存失败会还原输入并提示；不能因此阻止关闭/托盘退出或系统关机。
               if (!SaveHistoryRetention()) Log.Write("closing with history retention setting not saved");
                foreach (SettingsSection section in new SettingsSection[] { SettingsSection.Sms, SettingsSection.SmsRules, SettingsSection.Ntfy, SettingsSection.EmailRules, SettingsSection.Privacy })
                    if (!ApplySection(section)) Log.Write("closing with settings not saved; previous configuration retained");
               SaveWindowSize();
               FlushSettings();
              if (!allowClose)
             {
                 e.Cancel = true;
                 if (cfg.ShouldAutoLock) LockWindow();
                 ReleaseHiddenResources();
                 ShowInTaskbar = false;
                 Hide();
                 return;
              }
              base.OnClosing(e);
          }

          /// <summary>关闭到托盘时释放可重建的页面资源，但保留窗口和设置草稿。</summary>
          void ReleaseHiddenResources()
          {
              ReleaseHistoryRows();
              if (historyRawWindow != null) historyRawWindow.Close();
              if (diagnosticsWindow != null) diagnosticsWindow.Close();
              HideToast();
              if (historyRefreshTimer != null) { historyRefreshTimer.Stop(); historyRefreshTimer = null; }
              if (windowSizeTimer != null) { windowSizeTimer.Stop(); windowSizeTimer = null; }
              if (toastTimer != null) { toastTimer.Stop(); toastTimer = null; }
          }

          // ---- 顶栏：最大化/还原状态、系统命中测试与窗口位置记忆 ----

          /// <summary>最大化/还原：顶栏按钮、双击标题栏与系统命中测试共用同一个入口。</summary>
          internal void ToggleMaximize()
          {
              WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
          }

          internal const string CaptionMaximizeGlyph = "\uE922";   // ChromeMaximize
          internal const string CaptionRestoreGlyph = "\uE923";    // ChromeRestore

          internal static string CaptionGlyphFor(bool maximized) { return maximized ? CaptionRestoreGlyph : CaptionMaximizeGlyph; }
          internal static string CaptionLabelFor(bool maximized) { return maximized ? "还原" : "最大化"; }

          /// <summary>最大化后图标、悬停提示与无障碍名称要一起变成"还原"，否则按钮描述与当前动作不一致。</summary>
          internal void UpdateCaptionButtons()
          {
              if (btnMaxCaption == null) return;
              bool maximized = WindowState == WindowState.Maximized;
              btnMaxCaption.Content = CaptionGlyphFor(maximized);
              btnMaxCaption.ToolTip = CaptionLabelFor(maximized);
              AutomationProperties.SetName(btnMaxCaption, CaptionLabelFor(maximized));
          }

          // 命中码：只有顶栏交给系统（拖动吸附、双击最大化）；三个窗口按钮保持 client 消息。
          const int WmNcHitTest = 0x0084;
          internal const int HitCaption = 2;

          /// <summary>把顶栏交给系统做命中测试；窗口其它区域（含四边缩放带）保持原有行为。</summary>
          void HookCaptionHitTest()
          {
              if (dragBar == null) return;
              try
              {
                  IntPtr handle = new WindowInteropHelper(this).Handle;
                  if (handle == IntPtr.Zero) return;
                  HwndSource source = HwndSource.FromHwnd(handle);
                  if (source == null || captionSource == source) return;
                  source.AddHook(CaptionHitTest);
                  captionSource = source;
              }
              catch { }
          }

          /// <summary>命中测试钩子：任何异常都必须退回默认处理，否则整个窗口的鼠标输入会失效。</summary>
          IntPtr CaptionHitTest(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
          {
              if (message != WmNcHitTest) return IntPtr.Zero;
              try
              {
                  int hit = CaptionHitFor(PointFromScreen(new Point(ScreenX(lParam), ScreenY(lParam))));
                  if (hit == 0) return IntPtr.Zero;
                  handled = true;
                  return new IntPtr(hit);
              }
              catch { return IntPtr.Zero; }
          }

          static int ScreenX(IntPtr lParam) { return unchecked((short)(lParam.ToInt64() & 0xFFFF)); }
          static int ScreenY(IntPtr lParam) { return unchecked((short)((lParam.ToInt64() >> 16) & 0xFFFF)); }

          /// <summary>顶栏空白区返回 HTCAPTION（拖动吸附与双击最大化），0 表示交回 WPF。</summary>
          internal int CaptionHitFor(Point clientPoint)
          {
              if (dragBar == null) return 0;
              if (lockOverlay != null && lockOverlay.Visibility == Visibility.Visible) return 0;   // 锁屏时不响应拖动
              Rect bar = ClientRect(dragBar);
              if (bar.IsEmpty) return 0;
              // 四边与四角留给窗口缩放：拖动区收进缩放边框，避免把边缘拖动也变成移动窗口。
              double inset = windowChrome == null ? 10 : windowChrome.ResizeBorderThickness.Left;
              bar = Rect.Intersect(bar, new Rect(inset, inset,
                  Math.Max(0, ActualWidth - inset * 2), Math.Max(0, ActualHeight - inset * 2)));
              if (bar.IsEmpty) return 0;
              return CaptionHitCode(bar.Contains(clientPoint), InCaptionButton(clientPoint));
          }

          /// <summary>三个窗口按钮的命中区域；按钮必须留在客户区，否则悬停、按下与提示都会失效。</summary>
          bool InCaptionButton(Point clientPoint)
          {
              return ClientRect(btnMaxCaption).Contains(clientPoint)
                  || ClientRect(btnMinCaption).Contains(clientPoint)
                  || ClientRect(btnCloseCaption).Contains(clientPoint);
          }

          /// <summary>
          /// 顶栏命中决策：窗口按钮保持 WPF 点击（client 消息才有悬停/按下/提示）；
          /// 其余顶栏交给系统按标题栏处理（拖动吸附、双击最大化/还原）。
          /// Windows 11 的贴靠布局需要把最大化按钮交给系统（HTMAXBUTTON），代价是按钮失去 WPF 的
          /// 悬停、按下与提示反馈，因此不启用。
          /// </summary>
          internal static int CaptionHitCode(bool inDragBar, bool inCaptionButton)
          {
              if (inCaptionButton) return 0;
              return inDragBar ? HitCaption : 0;
          }

          /// <summary>元素在窗口客户区内的矩形；未布局或已释放时返回空矩形。</summary>
          Rect ClientRect(FrameworkElement element)
          {
              if (element == null) return Rect.Empty;
              try
              {
                  if (element.ActualWidth <= 0 || element.ActualHeight <= 0) return Rect.Empty;
                  return new Rect(element.TranslatePoint(new Point(0, 0), this), element.RenderSize);
              }
              catch { return Rect.Empty; }
          }

          /// <summary>没有记忆位置时保持系统居中；记忆位置在当前虚拟桌面外时放弃，避免窗口开在看不见的地方。</summary>
          void ApplyConfiguredWindowPosition()
          {
              if (!cfg.WindowPositionIncluded) return;
              double left, top;
              if (!TryClampWindowPosition(cfg.WindowLeft, cfg.WindowTop, Width, Height,
                      SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                      SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight, out left, out top)) return;
              WindowStartupLocation = WindowStartupLocation.Manual;
              Left = left; Top = top;
          }

          /// <summary>
          /// 把记忆位置限制在虚拟桌面内：横向至少留 160、纵向至少留 48 逻辑像素可见（多屏可为负坐标）。
          /// 数值不可用或桌面尺寸异常时返回 false，调用方保持默认居中。
          /// </summary>
          internal static bool TryClampWindowPosition(double left, double top, double width, double height,
              double virtualLeft, double virtualTop, double virtualWidth, double virtualHeight,
              out double clampedLeft, out double clampedTop)
          {
              clampedLeft = left; clampedTop = top;
              if (double.IsNaN(left) || double.IsNaN(top) || double.IsInfinity(left) || double.IsInfinity(top)) return false;
              if (virtualWidth < 200 || virtualHeight < 200) return false;
              double visibleWidth = Math.Min(width < 1 ? 200 : width, 160);
              double visibleHeight = Math.Min(height < 1 ? 200 : height, 48);
              double maxLeft = Math.Max(virtualLeft, virtualLeft + virtualWidth - visibleWidth);
              double maxTop = Math.Max(virtualTop, virtualTop + virtualHeight - visibleHeight);
              clampedLeft = Math.Min(Math.Max(left, virtualLeft), maxLeft);
              clampedTop = Math.Min(Math.Max(top, virtualTop), maxTop);
              return true;
          }

          /// <summary>尺寸与位置一起延迟保存（拖动、缩放、双击最大化都走这里）。</summary>
          void ScheduleWindowSizeSave()
          {
              if (!IsLoaded || WindowState != WindowState.Normal) return;
              if (windowSizeTimer == null)
              {
                  windowSizeTimer = new DispatcherTimer();
                  windowSizeTimer.Interval = TimeSpan.FromMilliseconds(450);
                  windowSizeTimer.Tick += delegate
                  {
                      windowSizeTimer.Stop();
                      SaveWindowSize();
                  };
              }
              windowSizeTimer.Stop();
              windowSizeTimer.Start();
          }

          void ApplyConfiguredWindowSize()
          {
              int workWidth = (int)SystemParameters.WorkArea.Width;
              int workHeight = (int)SystemParameters.WorkArea.Height;
              int savedWidth = cfg.WindowWidth < MinWidth ? 1100 : cfg.WindowWidth;
              int savedHeight = cfg.WindowHeight < MinHeight ? 760 : cfg.WindowHeight;
              Width = Math.Min(Math.Max(savedWidth, (int)MinWidth), Math.Max((int)MinWidth, workWidth - 24));
              Height = Math.Min(Math.Max(savedHeight, (int)MinHeight), Math.Max((int)MinHeight, workHeight - 24));
          }

          void SaveWindowSize()
          {
              // 未完成首次布局时 Left/Top 还是 0,0；存下来会把窗口钉在左上角。
              if (!IsLoaded || WindowState != WindowState.Normal || Width < MinWidth || Height < MinHeight) return;
              int width = (int)Math.Round(Width);
              int height = (int)Math.Round(Height);
              int left = (int)Math.Round(Left);
              int top = (int)Math.Round(Top);
              if (width == cfg.WindowWidth && height == cfg.WindowHeight
                  && cfg.WindowPositionIncluded && left == cfg.WindowLeft && top == cfg.WindowTop) return;
              QueueDraft(SettingsSection.Window, new Config
              {
                  WindowWidth = width, WindowHeight = height,
                  WindowLeft = left, WindowTop = top, WindowPositionIncluded = true
              });
          }

          protected override void OnClosed(EventArgs e)
          {
              closeCompleted = true;
              if (historyRefreshTimer != null) historyRefreshTimer.Stop();
              if (windowSizeTimer != null) windowSizeTimer.Stop();
              if (toastTimer != null) toastTimer.Stop();
              base.OnClosed(e);
          }
      }
}
