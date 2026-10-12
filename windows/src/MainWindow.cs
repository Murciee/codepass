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
         TextBox txtSmsMaxLength;
         SecretField fldToken, fldNtfyToken;
         CheckBox chkTip, chkStartup, chkAutoLock;
        ComboBox cmbPrivacy, cmbEmailPrivacy;
        TextBox txtEmailServer, txtEmailAccount, txtEmailPoll, txtEmailKeywords, txtEmailSenders;
        SecretField fldEmailAuth;
        CheckBox chkEmail, chkEmailJunk, chkEmailNotify, chkEmailAutoCopy, chkQqPopup;
        ComboBox cmbEmailProvider;
        TextBlock emailAuthHint, emailProviderHint;
        Button btnEmailAuthHelp;
        TextBox txtEmailKeywordAdd;
        WrapPanel emailKeywordTags;
        CheckBox chkEmailSaveBody;
        TextBlock lblEmailPreviewResult;
        TextBlock lblEmailTestResult, lblStorageError, lblQqStatus;
        TextBox txtEmailSubjectPreview;
        Button btnEmailTest;
        CancellationTokenSource emailTestCancel;
        bool warnedEmailAuthUndecryptable;
        Button btnTestPc, btnTestNtfy, btnLock;
        StackPanel listHost;
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
           Border cardNetwork, cardGeneral, cardNotify, cardSecurity, cardMore, cardEmail, cardEmailFilters, cardQqPopup;
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
         string autoPcIp = "";
         bool settingsRecoveryRequired;

         Border selectedRow;
         bool allowClose = false;
         bool closeCompleted;
         Window historyRawWindow, diagnosticsWindow;
         bool wasMinimized;

        static readonly Brush FgBody   = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : MakeBrush(0x1C, 0x1C, 0x1C);
        static readonly Brush AccentTx = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : MakeBrush(0x00, 0x78, 0xD4);
        static readonly Brush FgFaint  = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : MakeBrush(0x61, 0x61, 0x61);
        static readonly Brush RowHover = SystemParameters.HighContrast ? SystemColors.ControlBrush : MakeBrush(0xF5, 0xF5, 0xF5);
        static readonly Brush RowSel   = SystemParameters.HighContrast ? SystemColors.ControlBrush : MakeBrush(0xE9, 0xE9, 0xE9);

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
             AllowsTransparency = true;
                ResizeMode = ResizeMode.CanResize;
             Background = Brushes.Transparent;
              MinWidth = Math.Min(640, Math.Max(360, SystemParameters.WorkArea.Width - 24));
             MinHeight = Math.Min(460, Math.Max(300, SystemParameters.WorkArea.Height - 24));
             ApplyConfiguredWindowSize();
             WindowChrome chrome = new WindowChrome();
             chrome.CaptionHeight = 0;
             chrome.ResizeBorderThickness = new Thickness(10);
             chrome.CornerRadius = new CornerRadius(8);
             chrome.GlassFrameThickness = new Thickness(0);
             WindowChrome.SetWindowChrome(this, chrome);
             SizeChanged += delegate { ScheduleWindowSizeSave(); };
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
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
              chkTip        = Find<CheckBox>(root, "chkTip");
              chkStartup    = Find<CheckBox>(root, "chkStartup");
              chkAutoLock   = Find<CheckBox>(root, "chkAutoLock");
              cmbPrivacy    = Find<ComboBox>(root, "cmbPrivacy");
              cmbEmailPrivacy = Find<ComboBox>(root, "cmbEmailPrivacy");
             btnTestPc     = Find<Button>(root, "btnTestPc");
             btnTestNtfy   = Find<Button>(root, "btnTestNtfy");
             btnLock       = Find<Button>(root, "btnLock");
             listHost      = Find<StackPanel>(root, "listHost");
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
              cardEmail     = Find<Border>(root, "cardEmail");
              cardEmailFilters = Find<Border>(root, "cardEmailFilters");
              cardQqPopup = Find<Border>(root, "cardQqPopup");
              emailAuthHint = Find<TextBlock>(root, "emailAuthHint");
              emailProviderHint = Find<TextBlock>(root, "emailProviderHint");
               btnEmailAuthHelp = Find<Button>(root, "btnEmailAuthHelp");
               txtEmailKeywordAdd = Find<TextBox>(root, "txtEmailKeywordAdd");
               emailKeywordTags = Find<WrapPanel>(root, "emailKeywordTags");
             txtEmailServer = Find<TextBox>(root, "txtEmailServer");
             txtEmailAccount = Find<TextBox>(root, "txtEmailAccount");
             txtEmailPoll = Find<TextBox>(root, "txtEmailPoll");
             txtEmailKeywords = Find<TextBox>(root, "txtEmailKeywords");
             txtEmailSenders = Find<TextBox>(root, "txtEmailSenders");
             chkEmail = Find<CheckBox>(root, "chkEmail");
             chkEmailJunk = Find<CheckBox>(root, "chkEmailJunk");
             chkEmailNotify = Find<CheckBox>(root, "chkEmailNotify");
              chkEmailAutoCopy = Find<CheckBox>(root, "chkEmailAutoCopy");
              chkEmailSaveBody = Find<CheckBox>(root, "chkEmailSaveBody");
              btnEmailTest = Find<Button>(root, "btnEmailTest");
              txtEmailSubjectPreview = Find<TextBox>(root, "txtEmailSubjectPreview");
              lblEmailPreviewResult = Find<TextBlock>(root, "lblEmailPreviewResult");
             chkQqPopup = Find<CheckBox>(root, "chkQqPopup");
             cmbEmailProvider = Find<ComboBox>(root, "cmbEmailProvider");
             dragBar       = Find<Border>(root, "dragBar");
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
              lblEmailTestResult = Find<TextBlock>(root, "lblEmailTestResult");
              lblStorageError = Find<TextBlock>(root, "lblStorageError");
              lblQqStatus = Find<TextBlock>(root, "lblQqStatus");
              BuildSecretFields(root);
              InitializeUsability(root);
              InitializeResponsiveForms(pageSet);
              settingsScroll.SizeChanged += delegate { UpdateResponsiveForms(); };
              Loaded += delegate { UpdateResponsiveForms(); };
             if (btnLock != null) btnLock.Click += delegate { LockWindow(); };

            // 顶栏拖动 + 窗口按钮
            if (dragBar != null)
                dragBar.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
                {
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
               Wire(root, "btnEmailAuthHelp", delegate
               {
                   try { Process.Start(new ProcessStartInfo("https://myaccount.google.com/apppasswords") { UseShellExecute = true }); }
                   catch { ShowToast("无法打开浏览器，请在 Google 账号设置中查找应用专用密码"); }
               });
               Wire(root, "tabSmsHistory", delegate { SelectHistoryChannel("sms"); });
               Wire(root, "tabEmailHistory", delegate { SelectHistoryChannel("email"); });
               Wire(root, "tabAllHistory", delegate { SelectHistoryChannel("all"); });
               Wire(root, "btnHistoryReset", delegate { txtHistorySearch.Clear(); SelectHistoryChannel("all"); });
               txtHistorySearch.TextChanged += delegate { ScheduleHistoryRefresh(true); };
               txtHistorySearch.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
               { if (e.Key == Key.Escape) { txtHistorySearch.Clear(); e.Handled = true; } };
               Wire(root, "btnDiagnostics", delegate { ShowDiagnostics(); });
                Wire(root, "btnEmailKeywordAdd", delegate { AddEmailKeywords(); });
                Wire(root, "btnEmailKeywordDefault", delegate
                {
                    if (MessageBox.Show(this, "恢复默认主题关键词？", "确认", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
                   txtEmailKeywords.Text = new Config().EmailKeywords; QueueSection(SettingsSection.EmailRules);
                });
                 Wire(root, "btnEmailTest", delegate { TestEmailConnection(); });
                 Wire(root, "btnStorageRetry", delegate
                 {
                     if (!ApplySection(SettingsSection.EmailRules)) return;
                     try { cfg.Save(cfgPath); History.RetrySave(); EmailWatcher.Kick(); ShowToast("已重试保存并请求邮件检查"); }
                     catch { ShowToast("保存仍未完成，请检查文件权限与磁盘空间"); }
                     UpdateEmailStatus();
                 });
                Wire(root, "btnEmailGuide", delegate
                {
                    string guide = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GMAIL_APP_PASSWORD.md");
                    if (!File.Exists(guide)) guide = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "docs", "GMAIL_APP_PASSWORD.md"));
                    try { Process.Start(new ProcessStartInfo(File.Exists(guide) ? guide : "https://support.google.com/accounts/answer/185833?hl=zh-Hans") { UseShellExecute = true }); }
                    catch { ShowToast("请查看交付目录的 GMAIL_APP_PASSWORD.md 教程"); }
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
                   txtNtfyServer, txtNtfyTopic, txtFilterKeywords, txtFilterRegex,
                   txtEmailServer, txtEmailAccount, txtEmailPoll, txtEmailKeywords, txtEmailSenders };
            foreach (TextBox tb in boxes)
            {
                if (tb == null) continue;
                if (tb == txtEmailServer || tb == txtEmailAccount) continue;
                tb.LostFocus += delegate(object sender, RoutedEventArgs e) { QueueSection(SectionFor((FrameworkElement)sender)); };
                 tb.KeyDown += delegate(object s, KeyEventArgs e)
                 {
                     // 多行过滤/邮箱输入框用回车换行，失焦时自动保存；其他输入框回车立即保存。
                     if (e.Key == Key.Return
                         && s != txtFilterKeywords && s != txtFilterRegex
                          && s != txtEmailKeywords && s != txtEmailSenders) QueueSection(SectionFor((FrameworkElement)s));
                 };
            }
             if (chkTip != null) chkTip.Click += delegate { QueueSection(SettingsSection.SmsRules); };
             if (chkStartup != null) chkStartup.Click += delegate { QueueSection(SettingsSection.Sms); };
             if (chkAutoLock != null) chkAutoLock.Click += delegate { QueueSection(SettingsSection.Privacy); };
               if (cmbPrivacy != null) cmbPrivacy.SelectionChanged += delegate { QueueSection(SettingsSection.Privacy); };
               if (cmbEmailPrivacy != null) cmbEmailPrivacy.SelectionChanged += delegate { QueueSection(SettingsSection.Privacy); };
               if (fldToken != null) fldToken.Committed += delegate { QueueSection(SettingsSection.Sms); };
               if (fldNtfyToken != null) fldNtfyToken.Committed += delegate { QueueSection(SettingsSection.Ntfy); };
               if (chkEmail != null) chkEmail.Click += delegate { InvalidateEmailTest(); SettingsFeedback("邮箱连接草稿已修改 · 点击保存连接生效", false); };
               if (chkEmailJunk != null) chkEmailJunk.Click += delegate { QueueSection(SettingsSection.EmailRules); };
               if (chkEmailNotify != null) chkEmailNotify.Click += delegate { QueueSection(SettingsSection.EmailRules); };
               if (chkEmailAutoCopy != null) chkEmailAutoCopy.Click += delegate { QueueSection(SettingsSection.EmailRules); };
                if (chkEmailSaveBody != null) chkEmailSaveBody.Click += delegate { QueueSection(SettingsSection.EmailRules); };
               if (chkQqPopup != null) chkQqPopup.Click += delegate { QueueSection(SettingsSection.EmailRules); };
              if (cmbEmailProvider != null) cmbEmailProvider.SelectionChanged += delegate
              {
                  if (loadingValues) return;
                   ApplyEmailProviderPreset();
                   SettingsFeedback("邮箱连接草稿已修改 · 测试后点击保存连接", false);
              };
              if (txtPcIp != null) txtPcIp.TextChanged += delegate { if (!loadingValues) autoPcIp = ""; };
               if (txtEmailServer != null) txtEmailServer.TextChanged += delegate { UpdateEmailAuthHint(); };
               if (txtEmailKeywords != null) txtEmailKeywords.TextChanged += delegate { RenderEmailKeywordTags(); };
               if (txtEmailSubjectPreview != null) txtEmailSubjectPreview.TextChanged += delegate { UpdateEmailPreview(); };
               if (txtEmailKeywordAdd != null)
               {
                   txtEmailKeywordAdd.KeyDown += delegate(object s, KeyEventArgs e)
                   {
                       if (e.Key == Key.Return) { AddEmailKeywords(); e.Handled = true; }
                       if (e.Key == Key.Escape) { txtEmailKeywordAdd.Clear(); txtEmailKeywordAdd.Visibility = Visibility.Collapsed; e.Handled = true; }
                   };
                   // 收起条件：没有内容又失去焦点（有内容时保留输入框）。
                   txtEmailKeywordAdd.LostKeyboardFocus += delegate
                   {
                       if (txtEmailKeywordAdd.Text.Length == 0) txtEmailKeywordAdd.Visibility = Visibility.Collapsed;
                   };
               }
         }

         void WireMultilineScrolling()
         {
             TextBox[] boxes = { txtFilterKeywords, txtFilterRegex, txtEmailKeywords, txtEmailSenders };
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
                try { History.Clear(); RefreshHistory(); }
                catch (IOException) { ShowToast(History.LastSaveError); }
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
                 ShowInTaskbar = false;
                 Hide();
                 return;
              }
              base.OnClosing(e);
          }

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
              if (WindowState != WindowState.Normal || Width < MinWidth || Height < MinHeight) return;
              int width = (int)Math.Round(Width);
              int height = (int)Math.Round(Height);
              if (width == cfg.WindowWidth && height == cfg.WindowHeight) return;
              QueueDraft(SettingsSection.Window, new Config { WindowWidth = width, WindowHeight = height });
          }

          protected override void OnClosed(EventArgs e)
          {
              closeCompleted = true;
              if (historyRefreshTimer != null) historyRefreshTimer.Stop();
              if (windowSizeTimer != null) windowSizeTimer.Stop();
              base.OnClosed(e);
          }
      }
}
