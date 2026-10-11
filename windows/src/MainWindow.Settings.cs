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

         void SelectSection(string section)
         {
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
              SetCardState(cardEmailStatus, email);
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
                    Border target = security ? cardNotify : email ? cardEmailStatus : cardGeneral;
                  if (target != null) target.BringIntoView();
              }
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

        /// <summary>输入完成即应用（失焦、回车、勾选切换）；无效输入提示并还原为已保存值。</summary>
        bool SaveHistoryRetention()
        {
            int minutes;
            if (!int.TryParse(txtHistoryRetention.Text.Trim(), out minutes) || minutes < 0 || minutes > Config.MaximumHistoryRetentionMinutes)
            {
                txtHistoryRetention.Text = cfg.HistoryRetentionMinutes.ToString();
                ShowToast("记录保留时间需在 0～43200 分钟之间，0 停用，已还原");
                return false;
            }
            lock (Config.SaveGate)
            {
                if (minutes != cfg.HistoryRetentionMinutes)
                {
                    int previous = cfg.HistoryRetentionMinutes;
                    cfg.HistoryRetentionMinutes = minutes;
                    try { cfg.Save(cfgPath); }
                    catch
                    {
                        cfg.HistoryRetentionMinutes = previous;
                        txtHistoryRetention.Text = previous.ToString();
                        ShowToast("记录清理设置保存失败，已还原，请检查文件权限");
                        return false;
                    }
                }
            }
            Program.ApplyHistoryRetention();
            return true;
        }

          bool ApplySettings()
         {
              if (applying || loadingValues || txtPort == null) return !settingsRecoveryRequired;
             applying = true;
             try
            {
            int port, mn, mx, cl, smsMaxLength;
            if (!int.TryParse(txtPort.Text.Trim(), out port) || port < 1 || port > 65535)
            {
                 ShowToast("端口无效（1~65535），已还原");
                 txtPort.Text = cfg.Port.ToString();
                 return false;
            }
             if (!int.TryParse(txtMin.Text.Trim(), out mn) || mn < 1 || mn > 32
                 || !int.TryParse(txtMax.Text.Trim(), out mx) || mx < 1 || mx > 32)
             {
                 ShowToast("验证码长度需在 1～32 之间，已还原");
                  txtMin.Text = cfg.MinLen.ToString();
                  txtMax.Text = cfg.MaxLen.ToString();
                  return false;
             }
              if (mx < mn) { mx = mn; txtMax.Text = mx.ToString(); }
              if (!int.TryParse(txtSmsMaxLength.Text.Trim(), out smsMaxLength)
                  || smsMaxLength < 1 || smsMaxLength > Config.MaximumSmsMaxLength)
              {
                  txtSmsMaxLength.Text = cfg.SmsMaxLength.ToString();
                  ShowToast("短信长度上限需在 1～10000 字符之间，已还原");
                  return false;
              }
              if (!int.TryParse(txtClear.Text.Trim(), out cl) || cl < 0)
              {
                   ShowToast("自动清除需为不小于 0 的秒数，已还原");
                   txtClear.Text = cfg.AutoClearSeconds.ToString();
                   return false;
             }

              int emailPoll;
              if (!int.TryParse(txtEmailPoll.Text.Trim(), out emailPoll) || emailPoll < 10 || emailPoll > 600)
              {
                  ShowToast("邮箱检查间隔需在 10～600 秒之间，已还原");
                  txtEmailPoll.Text = cfg.EmailPollSeconds.ToString();
                  return false;
              }
              string newEmailServer = txtEmailServer == null ? "" : txtEmailServer.Text.Trim();
              string newEmailAccount = txtEmailAccount == null ? "" : txtEmailAccount.Text.Trim();
               string newEmailAuth = fldEmailAuth == null ? "" : fldEmailAuth.Text.Trim();
              string newEmailKeywords = txtEmailKeywords == null ? "" : txtEmailKeywords.Text;
               string newEmailSenders = txtEmailSenders == null ? "" : txtEmailSenders.Text;
               string normalizedKeywords, normalizedSenders, mailRuleError;
               if (!MailRules.NormalizeLines(newEmailKeywords, out normalizedKeywords, out mailRuleError))
               {
                   txtEmailKeywords.Text = cfg.EmailKeywords;
                   ShowToast("主题关键词无效：" + mailRuleError + "，已还原"); return false;
               }
                if (!MailRules.NormalizeSenders(newEmailSenders, out normalizedSenders, out mailRuleError))
               {
                   txtEmailSenders.Text = cfg.EmailSenders;
                   ShowToast("发件人规则无效：" + mailRuleError + "，已还原"); return false;
               }
               newEmailKeywords = normalizedKeywords; newEmailSenders = normalizedSenders;
               txtEmailKeywords.Text = newEmailKeywords; txtEmailSenders.Text = newEmailSenders;
              bool newEmailEnabled = chkEmail != null && chkEmail.IsChecked == true;
              bool newEmailJunk = chkEmailJunk != null && chkEmailJunk.IsChecked == true;
              bool newEmailNotify = chkEmailNotify == null || chkEmailNotify.IsChecked == true;
               bool newEmailAutoCopy = chkEmailAutoCopy == null || chkEmailAutoCopy.IsChecked == true;
               bool newEmailSaveBody = chkEmailSaveBody != null && chkEmailSaveBody.IsChecked == true;
               bool newQqPopup = chkQqPopup == null || chkQqPopup.IsChecked == true;
               if (newEmailEnabled
                   && (newEmailServer.Length == 0 || newEmailAccount.Length == 0
                       || newEmailAuth.Length == 0
                      || newEmailServer.IndexOf(' ') >= 0 || newEmailServer.IndexOf('/') >= 0
                      || newEmailServer.IndexOf(':') >= 0))
              {
                  txtEmailServer.Text = cfg.EmailServer;
                  txtEmailAccount.Text = cfg.EmailAccount;
                  if (fldEmailAuth != null) fldEmailAuth.Text = cfg.EmailAuth;
                  if (chkEmail != null) chkEmail.IsChecked = cfg.EmailEnabled;
                  ShowToast("启用邮箱接收需要填写服务器、账号和授权码，已还原");
                  return false;
              }
              string newToken = fldToken.Text.Trim();
              string newPcIp = txtPcIp.Text.Trim();
              if (cfg.PcIp.Length == 0 && autoPcIp.Length > 0 && newPcIp == autoPcIp) newPcIp = "";
              string newNtfyServer = txtNtfyServer.Text.Trim();
              string newNtfyTopic = txtNtfyTopic.Text.Trim();
              string newNtfyToken = fldNtfyToken.Text.Trim();
              string newFilterKeywords = txtFilterKeywords == null ? "" : txtFilterKeywords.Text;
              string newFilterRegex = txtFilterRegex == null ? "" : txtFilterRegex.Text;
              string filterError;
              if (!CodeExtractor.ValidateFilters(newFilterKeywords, newFilterRegex, out filterError))
              {
                  if (txtFilterKeywords != null) txtFilterKeywords.Text = cfg.FilterKeywords;
                  if (txtFilterRegex != null) txtFilterRegex.Text = cfg.FilterRegex;
                  ShowToast("信息过滤规则无效：" + filterError);
                  return false;
              }
             if (newPcIp.Length > 0)
             {
                  if (!IsUsablePcIp(newPcIp))
                 {
                     string restorePcIp = cfg.PcIp.Length > 0 ? cfg.PcIp : autoPcIp;
                     txtPcIp.Text = restorePcIp;
                     if (cfg.PcIp.Length == 0) autoPcIp = restorePcIp;
                     ShowToast("电脑 IPv4 地址无效，已还原");
                     return false;
                 }
             }
             if (newNtfyServer.Length == 0 && newNtfyTopic.Length > 0)
             {
                 txtNtfyServer.Text = cfg.NtfyServer;
                 ShowToast("启用 ntfy 主题时必须填写服务器地址，已还原");
                 return false;
             }
              if (newNtfyServer.Length > 0)
             {
                 Uri ntfyUri;
                  if (!Uri.TryCreate(newNtfyServer, UriKind.Absolute, out ntfyUri)
                       || ntfyUri.Scheme != Uri.UriSchemeHttps
                       || ntfyUri.Host.Length == 0 || ntfyUri.UserInfo.Length > 0)
                  {
                      txtNtfyServer.Text = cfg.NtfyServer;
                      ShowToast("ntfy 服务器必须使用 HTTPS，已还原");
                     return false;
                 }
              }
             bool newShowTip = chkTip.IsChecked == true;
            bool newStartup = chkStartup != null && chkStartup.IsChecked == true;
             int newPrivacy = cmbPrivacy != null && cmbPrivacy.SelectedIndex >= 0 && cmbPrivacy.SelectedIndex <= 2 ? cmbPrivacy.SelectedIndex : 0;
             int newEmailPrivacy = cmbEmailPrivacy != null && cmbEmailPrivacy.SelectedIndex >= 0 && cmbEmailPrivacy.SelectedIndex <= 2 ? cmbEmailPrivacy.SelectedIndex : 0;
            bool newAutoLock = cfg.LockEnabled && chkAutoLock != null && chkAutoLock.IsChecked == true;
            if (port == cfg.Port && newToken == cfg.Token && newPcIp == cfg.PcIp
                && mn == cfg.MinLen && mx == cfg.MaxLen && cl == cfg.AutoClearSeconds
                && smsMaxLength == cfg.SmsMaxLength
                && newShowTip == cfg.ShowTip && newStartup == cfg.StartWithWindows
                  && newPrivacy == cfg.PrivacyMode && newEmailPrivacy == cfg.EmailPrivacyMode && newNtfyServer == cfg.NtfyServer
                   && newNtfyTopic == cfg.NtfyTopic && newNtfyToken == cfg.NtfyToken
                  && newFilterKeywords == cfg.FilterKeywords && newFilterRegex == cfg.FilterRegex
                  && newEmailServer == cfg.EmailServer && newEmailAccount == cfg.EmailAccount
                   && newEmailAuth == cfg.EmailAuth && emailPoll == cfg.EmailPollSeconds
                  && newEmailKeywords == cfg.EmailKeywords && newEmailSenders == cfg.EmailSenders
                  && newEmailEnabled == cfg.EmailEnabled && newEmailJunk == cfg.EmailCheckJunk
                   && newEmailNotify == cfg.EmailNotify && newEmailAutoCopy == cfg.EmailAutoCopy
                   && newEmailSaveBody == cfg.EmailSaveBody
                  && newQqPopup == cfg.QqPopupEnabled
                 && newAutoLock == cfg.AutoLockEnabled
                 && !Program.StartupSyncFailed && !settingsRecoveryRequired)
                 {
                 return true;
             }

              lock (Config.SaveGate)
              {
              bool recoveryWasRequired = settingsRecoveryRequired;
              Config previous = new Config();
             previous.CopyFrom(cfg);
              bool startupChanged = previous.StartWithWindows != newStartup;
               bool mailboxChanged = !string.Equals(cfg.EmailServer, newEmailServer, StringComparison.OrdinalIgnoreCase)
                   || !string.Equals(cfg.EmailAccount, newEmailAccount, StringComparison.OrdinalIgnoreCase);
               bool emailRulesChanged = cfg.EmailKeywords != newEmailKeywords || cfg.EmailSenders != newEmailSenders
                   || cfg.MinLen != mn || cfg.MaxLen != mx;
              if (mailboxChanged)
              {
                   cfg.EmailLastUid = cfg.EmailUidValidity = cfg.EmailLastUidJunk = cfg.EmailUidValidityJunk = 0;
                   cfg.EmailMailboxState = "";
                   cfg.EmailAuthEncRaw = "";
                   cfg.EmailAuthUndecryptable = false;
                   cfg.EmailPasswordRequired = false;
              }

             cfg.Port = port;
            cfg.Token = newToken;
            cfg.PcIp = newPcIp;
            cfg.MinLen = mn;
            cfg.MaxLen = mx;
            cfg.SmsMaxLength = smsMaxLength;
            cfg.AutoClearSeconds = cl;
            cfg.ShowTip = newShowTip;
             cfg.StartWithWindows = newStartup;
             cfg.PrivacyMode = newPrivacy;
             cfg.EmailPrivacyMode = newEmailPrivacy;
            cfg.NtfyServer = newNtfyServer;
              cfg.NtfyTopic = newNtfyTopic;
              cfg.NtfyToken = newNtfyToken;
              cfg.FilterKeywords = newFilterKeywords;
              cfg.FilterRegex = newFilterRegex;
              cfg.EmailServer = newEmailServer;
              cfg.EmailAccount = newEmailAccount;
               cfg.EmailAuth = newEmailAuth;
              if (newEmailAuth.Length > 0)
              {
                  // 用户重新填写了授权码：清除“解密失败保留密文”的标记并恢复提示文字。
                   cfg.EmailAuthUndecryptable = false;
                   cfg.EmailAuthEncRaw = "";
                   cfg.EmailPasswordRequired = false;
                  UpdateEmailAuthHint();
              }
              cfg.EmailPollSeconds = emailPoll;
              cfg.EmailKeywords = newEmailKeywords;
              cfg.EmailSenders = newEmailSenders;
              cfg.EmailEnabled = newEmailEnabled;
              cfg.EmailCheckJunk = newEmailJunk;
              cfg.EmailNotify = newEmailNotify;
               cfg.EmailAutoCopy = newEmailAutoCopy;
               cfg.EmailSaveBody = newEmailSaveBody;
              cfg.QqPopupEnabled = newQqPopup;
             cfg.AutoLockEnabled = newAutoLock;

             bool startupAttempted = false;
             bool configSaved = false;
              try
              {
                  cfg.Save(cfgPath);
                  configSaved = true;
                 if (startupChanged || Program.StartupSyncFailed)
                 {
                      startupAttempted = true;
                      Program.SetStartup(cfg.StartWithWindows);
                  }
                  settingsRecoveryRequired = false;
                  // 邮箱 / QQ 弹窗设置即时生效：无需重启程序。
                   Program.RefreshEmailWatchers();
                   if (emailRulesChanged && Program.EmailConfigured()) EmailWatcher.RequestRecentCheck();
                   UpdateEmailAuthHint();
              }
              catch (Exception ex)
              {
                  bool restored = RestoreSettings(previous, startupAttempted, configSaved);
                  settingsRecoveryRequired = !restored || (!configSaved && recoveryWasRequired);
                  Log.Write("apply settings failed: " + ex.GetType().Name);
                  ShowToast(restored
                      ? "保存失败：" + SettingsFailureReason(ex) + "；已还原，可重试或退出"
                      : "恢复未完成：" + SettingsFailureReason(ex) + "；可重试或退出");
                  return false;
             }
              return true;
              }
              }
            finally { applying = false; }
        }

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
                 ApplySettings();
                 return;
             }

             ContextMenu menu = new ContextMenu();
             FrameworkElement host = Content as FrameworkElement;
             if (host != null)
             {
                 menu.Style = host.Resources["FlyoutMenu"] as Style;
                 menu.ItemContainerStyle = host.Resources["FlyoutItem"] as Style;
             }
             foreach (string ip in ips)
             {
                 MenuItem item = new MenuItem();
                 item.Header = ip;
                 string value = ip;
                 if (value == txtPcIp.Text) item.FontWeight = FontWeights.SemiBold;
                   item.Click += delegate
                   {
                       autoPcIp = "";
                       txtPcIp.Text = value;
                      txtPcIp.CaretIndex = value.Length;
                      ApplySettings();
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

             SetTestButton(btnTestPc, false, "测试中…");
             string pcToken = fldToken == null ? "" : fldToken.Text.Trim();
             ThreadPool.QueueUserWorkItem(delegate
             {
                 string result;
                 try
                 {
                     HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://" + ip + ":" + port + "/health");
                     request.Method = "GET";
                     request.Timeout = 5000;
                     request.ReadWriteTimeout = 5000;
                     request.UserAgent = "codepass/1.0";
                     request.Proxy = null;
                     if (pcToken.Length > 0)
                         request.Headers["X-Token"] = pcToken;
                     using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                         result = response.StatusCode == HttpStatusCode.OK
                             ? "已连接到 " + ip + ":" + port + "，HTTP 服务和令牌验证正常。"
                             : "服务器已响应，但健康检查结果异常。";
                 }
                  catch (TimeoutException) { result = "连接超时，可能是 EasyTier 路由或 Windows 防火墙未放行。"; }
                  catch (WebException ex)
                  {
                      HttpWebResponse response = ex.Response as HttpWebResponse;
                      result = ex.Status == WebExceptionStatus.Timeout ? "连接超时，可能是 EasyTier 路由或 Windows 防火墙未放行。"
                         : response == null ? "连接失败：" + ex.Message
                         : "服务器返回 HTTP " + (int)response.StatusCode + "，请检查端口或访问令牌。";
                 }
                 catch (Exception ex) { result = "测试失败：" + ex.Message; }
                 Dispatcher.BeginInvoke(new Action(delegate
                 {
                     SetTestButton(btnTestPc, true, "测试连通性");
                     MessageBox.Show(this, result, "电脑地址测试", MessageBoxButton.OK,
                         result.StartsWith("已连接") ? MessageBoxImage.Information : MessageBoxImage.Warning);
                 }));
             });
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
              SetTestButton(btnTestNtfy, false, "测试中…");
             string ntfyToken = fldNtfyToken == null ? "" : fldNtfyToken.Text.Trim();
             ThreadPool.QueueUserWorkItem(delegate
             {
                 string result;
                 try
                 {
                     string target = server + (topic.Length == 0 ? "/" : "/" + topic + "/json");
                      HttpWebRequest request = (HttpWebRequest)WebRequest.Create(target);
                      request.AllowAutoRedirect = false;
                     request.Method = "GET";
                     request.Timeout = 5000;
                     request.ReadWriteTimeout = 5000;
                     request.UserAgent = "codepass/1.0";
                     request.Proxy = null;
                     if (ntfyToken.Length > 0)
                         request.Headers["Authorization"] = "Bearer " + ntfyToken;
                     using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                         result = "服务器可访问，HTTP " + (int)response.StatusCode + " " + response.StatusDescription
                                + (topic.Length == 0 ? "。" : "，主题和鉴权可访问。 ");
                 }
                 catch (WebException ex)
                 {
                     HttpWebResponse response = ex.Response as HttpWebResponse;
                     result = response == null ? "服务器访问失败：" + ex.Message
                         : "服务器返回 HTTP " + (int)response.StatusCode + "，请检查主题或访问令牌。";
                 }
                 catch (Exception ex) { result = "测试失败：" + ex.Message; }
                 Dispatcher.BeginInvoke(new Action(delegate
                 {
                     SetTestButton(btnTestNtfy, true, "测试连通性");
                     MessageBox.Show(this, result, "ntfy 测试", MessageBoxButton.OK,
                         result.StartsWith("服务器可访问") ? MessageBoxImage.Information : MessageBoxImage.Warning);
                 }));
             });
         }

         static void SetTestButton(Button button, bool enabled, string text)
         {
             if (button == null) return;
             button.IsEnabled = enabled;
             button.Content = text;
         }

         void OnExport()
         {
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
               if (!ApplySettings()) return;
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
            try { ok = Program.CopyManually(text, new WindowInteropHelper(this).Handle); } catch { }
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
