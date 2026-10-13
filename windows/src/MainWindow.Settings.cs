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
          // ---------- 左侧分类 ----------

          internal void SelectSection(string section)
          {
              activeSection = section;
             if (pageSet == null) return;
             bool history = section == "history";
             bool about = section == "about";
             pageLog.Visibility = history ? Visibility.Visible : Visibility.Collapsed;
             pageSet.Visibility = (!history && !about) ? Visibility.Visible : Visibility.Collapsed;
              if (pageAbout != null) pageAbout.Visibility = about ? Visibility.Visible : Visibility.Collapsed;
              bool general = !history && !about && (section == "general" || section == "network" || section == "notify");
               bool security = !history && !about && section == "security";
               bool email = !history && !about && section == "email";
              SetCardState(cardGeneral, general);
              SetCardState(cardNetwork, general);
              SetCardState(cardNtfy, general);
              SetCardState(cardSmsFilters, general);
               SetCardState(cardEmail, email);
               SetCardState(cardEmailFilters, email);
               SetCardState(cardQqPopup, email);
              SetCardState(cardNotify, security);
              SetCardState(cardSecurity, security);
              SetCardState(cardMore, general);
              SetNavState(navHistory, history);
               SetNavState(navGeneral, general);
               SetNavState(navEmail, email);
              SetNavState(navSecurity, security);
              SetNavState(navAbout, about);
               if (general || email || security)
               {
                   if (settingsScroll != null) settingsScroll.ScrollToTop();
                    Border target = security ? cardNotify : email ? cardEmail : cardGeneral;
                   if (target != null) target.BringIntoView();
               }
               if (history) RefreshHistory();
               UpdateReceiveSummary();
         }

          static void SetNavState(Button button, bool selected)
         {
              if (button != null) button.Tag = selected ? "On" : null;
          }

          static void SetCardState(Border card, bool visible)
          {
              if (card != null) card.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
          }

        // ---------- 数据 <-> 界面 ----------

         void LoadValues()
         {
             if (txtPort == null) return;
             loadingValues = true;
             try
             {
                  txtPort.Text = cfg.Port.ToString();
                  txtHistoryRetention.Text = cfg.HistoryRetentionMinutes.ToString();
                 fldToken.Text = cfg.Token;
                 autoPcIp = cfg.PcIp.Length == 0 ? Program.DetectLocalIp() : "";
                 if (autoPcIp == "127.0.0.1") autoPcIp = "";
                 txtPcIp.Text = cfg.PcIp.Length > 0 ? cfg.PcIp : autoPcIp;
                  txtNtfyServer.Text = cfg.NtfyServer;
                  txtNtfyTopic.Text = cfg.NtfyTopic;
                  fldNtfyToken.Text = cfg.NtfyToken;
                  txtMin.Text = cfg.MinLen.ToString();
                   txtMax.Text = cfg.MaxLen.ToString();
                   txtSmsMaxLength.Text = cfg.SmsMaxLength.ToString();
                  txtClear.Text = cfg.AutoClearSeconds.ToString();
                  if (txtFilterKeywords != null) txtFilterKeywords.Text = cfg.FilterKeywords;
                  if (txtFilterRegex != null) txtFilterRegex.Text = cfg.FilterRegex;
                  if (txtAcceptKeywords != null) txtAcceptKeywords.Text = cfg.SmsAcceptKeywords;
                  RenderAcceptKeywordTags();
                  if (txtEmailServer != null)
                  {
                      txtEmailServer.Text = cfg.EmailServer;
                      txtEmailAccount.Text = cfg.EmailAccount;
                       if (fldEmailAuth != null) fldEmailAuth.Text = cfg.EmailAuth;
                      txtEmailPoll.Text = cfg.EmailPollSeconds.ToString();
                      txtEmailKeywords.Text = cfg.EmailKeywords;
                      txtEmailSenders.Text = cfg.EmailSenders;
                  }
                  if (chkEmail != null)
                  {
                      chkEmail.IsChecked = cfg.EmailEnabled;
                      chkEmailJunk.IsChecked = cfg.EmailCheckJunk;
                      chkEmailNotify.IsChecked = cfg.EmailNotify;
                       chkEmailAutoCopy.IsChecked = cfg.EmailAutoCopy;
                       chkEmailSaveBody.IsChecked = cfg.EmailSaveBody;
                      chkQqPopup.IsChecked = cfg.QqPopupEnabled;
                  }
                  if (cmbEmailProvider != null)
                      cmbEmailProvider.SelectedIndex = EmailProviderIndexOf(cfg.EmailServer);
                  if (cfg.EmailAuthUndecryptable && !warnedEmailAuthUndecryptable)
                  {
                      warnedEmailAuthUndecryptable = true;
                      ShowToast("邮箱授权码已失效（换机或换了 Windows 账户），请重新填写");
                  }
                   UpdateEmailAuthHint();
                    RenderEmailKeywordTags();
                    UpdateEmailStatus();
                  chkTip.IsChecked = cfg.ShowTip;
                 if (chkStartup != null) chkStartup.IsChecked = cfg.StartWithWindows;
                  if (cmbPrivacy != null) cmbPrivacy.SelectedIndex = cfg.PrivacyMode < 0 || cfg.PrivacyMode > 2 ? 0 : cfg.PrivacyMode;
                  if (cmbEmailPrivacy != null) cmbEmailPrivacy.SelectedIndex = cfg.EmailPrivacyMode;
                 RefreshSecurityControls();
             }
             finally { loadingValues = false; }
         }

        /// <summary>显式提交记录保留时间；验证失败保留草稿。</summary>
        bool SaveHistoryRetention()
        {
            return ApplySection(SettingsSection.Retention);
        }

          bool ApplySettings() { return ApplySection(SettingsSection.All); }

         /// <summary>保存失败时恢复内存、界面、配置文件和已尝试同步的开机启动设置。</summary>
         bool RestoreSettings(Config previous, bool restoreStartup, bool restoreConfig = true)
         {
             bool restored = true;
             lock (Config.SaveGate)
             {
             cfg.CopyFrom(previous);
             try { if (restoreConfig) cfg.Save(cfgPath); }
             catch (Exception ex)
             {
                 restored = false;
                  Log.Write("settings rollback save failed: " + ex.GetType().Name);
             }
             }
             if (restoreStartup)
             {
                 try { Program.SetStartup(previous.StartWithWindows); }
                 catch (Exception ex)
                 {
                     restored = false;
                  Log.Write("settings rollback startup failed: " + ex.GetType().Name);
                 }
             }
             LoadValues();
             return restored;
         }

        static string SettingsFailureReason(Exception ex)
        {
            if (ex is UnauthorizedAccessException) return "配置目录不可写或文件只读";
            if (ex is IOException) return "配置文件被占用或写入失败";
            if (ex is System.Security.Cryptography.CryptographicException) return "Windows 凭据加密失败";
            if (ex is FormatException) return "配置规则无效";
            if (ex is System.Security.SecurityException) return "系统拒绝配置或启动项访问";
            return "设置应用失败（" + ex.GetType().Name + "）";
        }

         /// <summary>点"检测本机地址"：只有一个可用地址就直接填入，多个则弹出列表选择。</summary>
         void DetectLocalIp(Button anchor)
         {
             if (txtPcIp == null) return;
              List<string> ips = Program.DetectLocalIpList();
              if (ips.Count == 0)
              {
                  ShowToast("未检测到可用的局域网 IPv4 地址");
                  return;
              }
              if (ips.Count == 1)
              {
                  autoPcIp = "";
                  txtPcIp.Text = ips[0];
                 QueueSection(SettingsSection.Sms);
                 return;
             }

             ContextMenu menu = new ContextMenu();
             FrameworkElement host = Content as FrameworkElement;
             if (host != null)
             {
                 menu.Style = host.Resources["FlyoutMenu"] as Style;
                 menu.ItemContainerStyle = host.Resources["FlyoutItem"] as Style;
             }
             Dictionary<string, string> labels = Program.LocalIpLabels();
             foreach (string ip in ips)
             {
                 MenuItem item = new MenuItem();
                 string label;
                 item.Header = ip + (labels.TryGetValue(ip, out label) ? "  ·  " + label : "");
                 string value = ip;
                 if (value == txtPcIp.Text) item.FontWeight = FontWeights.SemiBold;
                   item.Click += delegate
                   {
                       autoPcIp = "";
                       txtPcIp.Text = value;
                       txtPcIp.CaretIndex = value.Length;
                       QueueSection(SettingsSection.Sms);
                  };
                 menu.Items.Add(item);
             }

             if (anchor == null)
             {
                 menu.Placement = PlacementMode.MousePoint;
             }
             else
             {
                 menu.PlacementTarget = anchor;
                 menu.Placement = PlacementMode.Bottom;
             }
             menu.IsOpen = true;
         }

         void TestPcConnectivity()
         {
             string ip = txtPcIp == null ? "" : txtPcIp.Text.Trim();
             int port;
              if (!IsUsablePcIp(ip)
                  || !int.TryParse(txtPort.Text.Trim(), out port) || port < 1 || port > 65535)
             {
                 MessageBox.Show(this, "请输入有效的 IPv4 地址和端口。", "测试失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                 return;
             }

             if (pcTestCancel != null) { pcTestCancel.Cancel(); return; }
             string pcToken = fldToken == null ? "" : fldToken.Text.Trim();
             RunNetworkTest(false, "http://" + ip + ":" + port + "/health", pcToken);
         }

         void TestNtfyConnectivity()
         {
             string server = txtNtfyServer == null ? "" : txtNtfyServer.Text.Trim().TrimEnd('/');
             string topic = txtNtfyTopic == null ? "" : txtNtfyTopic.Text.Trim();
             Uri uri;
              if (!Uri.TryCreate(server, UriKind.Absolute, out uri)
                   || uri.Scheme != Uri.UriSchemeHttps
                   || uri.Host.Length == 0 || uri.UserInfo.Length > 0)
              {
                  MessageBox.Show(this, "请输入有效的 HTTPS 服务器地址。", "测试失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                 return;
             }
              if (ntfyTestCancel != null) { ntfyTestCancel.Cancel(); return; }
             string ntfyToken = fldNtfyToken == null ? "" : fldNtfyToken.Text.Trim();
             RunNetworkTest(true, server + (topic.Length == 0 ? "/" : "/" + Uri.EscapeDataString(topic) + "/json"), ntfyToken);
         }

         static void SetTestButton(Button button, bool enabled, string text)
         {
             if (button == null) return;
             button.IsEnabled = enabled;
             button.Content = text;
         }

         // ---------- 短信接收关键字标签 ----------

         static List<string> AcceptKeywordList(string text)
         {
             List<string> list = new List<string>();
             HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
             foreach (string line in (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
             {
                 string value = line.Trim();
                 if (value.Length > 0 && seen.Add(value)) list.Add(value);
             }
             return list;
         }

         void RenderAcceptKeywordTags()
         {
             if (acceptKeywordTags == null || txtAcceptKeywords == null) return;
             acceptKeywordTags.Children.Clear();
             foreach (string keyword in AcceptKeywordList(txtAcceptKeywords.Text))
             {
                 string value = keyword;
                 Border chip = new Border { Background = MakeBrush(0xE8, 0xF2, 0xFC), CornerRadius = new CornerRadius(12),
                     Padding = new Thickness(10, 2, 6, 2), Margin = new Thickness(0, 0, 6, 6) };
                 StackPanel content = new StackPanel { Orientation = Orientation.Horizontal };
                 content.Children.Add(new TextBlock { Text = value, Foreground = AccentTx, FontSize = 13,
                     VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 250, ToolTip = value });
                 Button remove = new Button { Content = "×", Width = 24, Height = 24, FontSize = 16,
                     Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = FgFaint,
                     Cursor = Cursors.Hand, ToolTip = "移除关键字", Margin = new Thickness(4, 0, 0, 0) };
                 System.Windows.Automation.AutomationProperties.SetName(remove, "移除关键字 " + value);
                 remove.Click += delegate
                 {
                     List<string> words = AcceptKeywordList(txtAcceptKeywords.Text);
                     words.Remove(value);
                     txtAcceptKeywords.Text = string.Join("\n", words.ToArray());
                     QueueSection(SettingsSection.SmsRules);
                 };
                 content.Children.Add(remove); chip.Child = content; acceptKeywordTags.Children.Add(chip);
             }
         }

         void AddAcceptKeywords()
         {
             if (txtAcceptKeywordAdd == null || txtAcceptKeywords == null) return;
             // 平时不显示输入框：第一次点“添加”先展开并聚焦，输入后再点（或回车）才写入。
             if (txtAcceptKeywordAdd.Visibility != Visibility.Visible)
             {
                 txtAcceptKeywordAdd.Visibility = Visibility.Visible;
                 txtAcceptKeywordAdd.Focus();
                 return;
             }
             string text = string.Join("\n", AcceptKeywordList(txtAcceptKeywords.Text + "\n" + txtAcceptKeywordAdd.Text).ToArray());
             string error;
             if (!CodeExtractor.ValidateKeywordLines(text, "接收关键字", out error)) { ShowToast(error); return; }
             txtAcceptKeywords.Text = text;
             txtAcceptKeywordAdd.Clear();
             QueueSection(SettingsSection.SmsRules);
         }

          void OnExport()
          {
              FlushSettings();
             SaveFileDialog dialog = new SaveFileDialog();
             dialog.Title = "导出 codepass 设置";
             dialog.Filter = "配置文件 (*.ini)|*.ini|所有文件 (*.*)|*.*";
             dialog.FileName = "codepass-config.ini";
             if (dialog.ShowDialog(this) != true) return;

              MessageBoxResult choice = MessageBox.Show(this,
                   "是否包含局域网令牌、ntfy 令牌、邮箱授权码和程序锁校验信息？\r\n\r\n包含敏感信息的文件不要发送给他人。",
                 "导出设置", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
             if (choice == MessageBoxResult.Cancel) return;
             try
             {
                 cfg.Save(dialog.FileName, choice == MessageBoxResult.Yes, true);
                 MessageBox.Show(this, "设置已导出。", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
             }
             catch (Exception ex) { MessageBox.Show(this, "导出失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error); }
         }

          void OnImport()
          {
              FlushSettings();
             OpenFileDialog dialog = new OpenFileDialog();
             dialog.Title = "导入 codepass 设置";
             dialog.Filter = "配置文件 (*.ini)|*.ini|所有文件 (*.*)|*.*";
             if (dialog.ShowDialog(this) != true) return;

              Config imported;
               try { imported = Config.Load(dialog.FileName, true); }
              catch (Exception ex)
              {
                  MessageBox.Show(this, "读取配置失败：" + ex.Message, "导入失败", MessageBoxButton.OK, MessageBoxImage.Error);
                  return;
              }
                Uri importedNtfyServer;
                bool importedAddressInvalid = imported.PcIp.Length > 0
                    && !IsUsablePcIp(imported.PcIp);
                bool importedNtfyInvalid = imported.NtfyServer.Length == 0 && imported.NtfyTopic.Length > 0;
                if (imported.NtfyServer.Length > 0
                     && (!Uri.TryCreate(imported.NtfyServer, UriKind.Absolute, out importedNtfyServer)
                          || importedNtfyServer.Scheme != Uri.UriSchemeHttps
                          || importedNtfyServer.Host.Length == 0 || importedNtfyServer.UserInfo.Length > 0))
                    importedNtfyInvalid = true;
                string importedFilterError;
                bool importedFilterInvalid = !CodeExtractor.ValidateFilters(
                    imported.FilterKeywords, imported.FilterRegex, out importedFilterError);
                if (imported.Port < 1 || imported.Port > 65535 || imported.MinLen < 1 || imported.MinLen > 32
                     || imported.MaxLen < imported.MinLen || imported.MaxLen > 32
                     || imported.AutoClearSeconds < 0 || imported.PrivacyMode < 0 || imported.PrivacyMode > 2
                     || imported.EmailPrivacyMode < 0 || imported.EmailPrivacyMode > 2
                      || importedAddressInvalid || importedNtfyInvalid || importedFilterInvalid
                    || (imported.SecretsIncluded && imported.LockEnabled && !imported.LockDataValid)
                   || (imported.SecretsIncluded && !imported.LockEnabled
                       && (imported.AutoLockEnabled || imported.LockSalt.Length > 0 || imported.LockHash.Length > 0)))
             {
                    MessageBox.Show(this, importedFilterInvalid
                        ? "配置文件中的信息过滤规则无效：" + importedFilterError
                        : "配置文件中的端口、地址、验证码长度、隐私模式或密码锁数据无效。", "导入失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                 return;
             }
              if (MessageBox.Show(this, "导入后需要重启 codepass 才会应用端口和 ntfy 设置，是否继续？", "确认导入",
                      MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
               Config previous = new Config();
               previous.CopyFrom(cfg);
               bool importedSecrets = imported.SecretsIncluded;
              if (!importedSecrets)
              {
                    imported.Token = cfg.Token;
                      imported.NtfyToken = cfg.NtfyToken;
                      imported.NtfyTopic = cfg.NtfyTopic;
                       if (!imported.EmailPasswordRequired && string.Equals(imported.EmailServer, cfg.EmailServer, StringComparison.OrdinalIgnoreCase)
                           && string.Equals(imported.EmailAccount, cfg.EmailAccount, StringComparison.OrdinalIgnoreCase))
                       {
                           imported.EmailAuth = cfg.EmailAuth;
                           imported.EmailAuthEncRaw = cfg.EmailAuthEncRaw;
                           imported.EmailAuthUndecryptable = cfg.EmailAuthUndecryptable;
                       }
                    imported.LockEnabled = cfg.LockEnabled;
                   imported.AutoLockEnabled = cfg.AutoLockEnabled;
                  imported.LockSalt = cfg.LockSalt;
                  imported.LockHash = cfg.LockHash;
               }
               if (!imported.WindowWidthIncluded) imported.WindowWidth = cfg.WindowWidth;
                if (!imported.WindowHeightIncluded) imported.WindowHeight = cfg.WindowHeight;
                lock (Config.SaveGate)
                {
                 imported.EmailLastUid = imported.EmailUidValidity = imported.EmailLastUidJunk = imported.EmailUidValidityJunk = 0;
                 imported.EmailMailboxState = "";
                 cfg.CopyFrom(imported);
                }
              ApplyConfiguredWindowSize();
              LoadValues();
               bool startupChanged = previous.StartWithWindows != cfg.StartWithWindows;
               bool startupAttempted = false;
               try
               {
                   cfg.Save(cfgPath);
                    if (startupChanged || Program.StartupSyncFailed)
                    {
                        startupAttempted = true;
                        Program.SetStartup(cfg.StartWithWindows);
                    }
                    settingsRecoveryRequired = false;
                    Program.RefreshEmailWatchers();
                    MessageBox.Show(this, "设置已导入并保存。" + (importedSecrets ? "" : "\r\n脱敏文件未包含令牌、邮箱授权码和密码锁，当前安全设置已保留。")
                       + "\r\n局域网访问令牌立即生效；端口和 ntfy 设置请重启应用。", "导入成功",
                      MessageBoxButton.OK, MessageBoxImage.Information);
                   if (cfg.ShouldAutoLock) LockWindow();
               }
              catch (Exception ex)
              {
                  bool restored = RestoreSettings(previous, startupAttempted);
                  settingsRecoveryRequired = !restored;
                  MessageBox.Show(this, restored
                      ? "导入保存失败：" + ex.Message + "，已还原。"
                      : "导入保存失败且还原失败，请检查配置文件和开机启动设置。", "错误",
                      MessageBoxButton.OK, MessageBoxImage.Error);
              }
         }

         void OnGeneratePhoneConfig()
         {
              if (txtPcIp == null) return;
             string ip = txtPcIp.Text.Trim();
                if (!ApplySection(SettingsSection.Sms | SettingsSection.Ntfy | SettingsSection.SmsRules)) return;
               if (cfg.Token.Length == 0)
               {
                   ShowToast("请先设置随机访问令牌并重启程序，再复制手机端配置");
                   return;
               }
               if (ip.Length == 0) ip = Program.DetectLocalIp();
              if (!IsUsablePcIp(ip))
             {
                 ShowToast("未检测到可用于手机连接的电脑 IPv4 地址");
                 return;
             }
              string text =
                 "# 复制以下内容填到手机端 forward.sh 顶部\r\n" +
                 "PC_IP=" + ShellLiteral(ip) + "\r\n" +
                 "PC_PORT=" + ShellLiteral(txtPort.Text.Trim()) + "\r\n" +
                 "TOKEN=" + ShellLiteral(fldToken.Text.Trim()) + "\r\n" +
                 "NTFY_SERVER=" + ShellLiteral(txtNtfyServer.Text.Trim()) + "\r\n" +
                 "NTFY_TOPIC=" + ShellLiteral(txtNtfyTopic.Text.Trim()) + "\r\n" +
                 "NTFY_TOKEN=" + ShellLiteral(fldNtfyToken.Text.Trim()) + "\r\n" +
                 "MIN_LEN=" + ShellLiteral(txtMin.Text.Trim()) + "\r\n" +
                 "MAX_LEN=" + ShellLiteral(txtMax.Text.Trim()) + "\r\n";
            bool ok = false;
            try { ok = Program.Clipboard.CopyManually(text, new WindowInteropHelper(this).Handle); } catch { }
             MessageBox.Show(this,
                ok ? text : "复制失败（剪贴板被占用），请重试。",
             ok ? "已复制到剪贴板" : "复制失败", MessageBoxButton.OK,
             ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
         }

          static string ShellLiteral(string value)
         {
             if (value == null) value = "";
             value = value.Replace("\r", " ").Replace("\n", " ");
              return "\"" + value.Replace("\\", "\\\\")
                  .Replace("\"", "\\\"")
                  .Replace("$", "\\$")
                  .Replace("`", "\\`") + "\"";
          }

          static bool IsUsablePcIp(string value)
          {
              IPAddress address;
              return IPAddress.TryParse(value, out address)
                  && address.AddressFamily == AddressFamily.InterNetwork
                  && !IPAddress.IsLoopback(address)
                  && !address.Equals(IPAddress.Any);
          }
    }
}
