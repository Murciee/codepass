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
        // 邮箱服务预设（与下拉顺序一致）；最后一项为自定义。
        static readonly string[] EmailProviderServers = { "imap.qq.com", "imap.163.com", "imap.126.com", "imap.gmail.com", "outlook.office365.com" };

        static int EmailProviderIndexOf(string server)
        {
            for (int i = 0; i < EmailProviderServers.Length; i++)
                if (string.Equals(server, EmailProviderServers[i], StringComparison.OrdinalIgnoreCase)) return i;
            return EmailProviderServers.Length;
        }

        /// <summary>选择服务商预设时自动填服务器地址；“自定义”保持用户输入。</summary>
        void ApplyEmailProviderPreset()
        {
            if (cmbEmailProvider == null || txtEmailServer == null) return;
             int index = cmbEmailProvider.SelectedIndex;
              if (index >= 0 && index < EmailProviderServers.Length)
              {
                  string server = EmailProviderServers[index];
                  if (!string.Equals(server, txtEmailServer.Text.Trim(), StringComparison.OrdinalIgnoreCase))
                  {
                      chkEmail.IsChecked = false;
                      fldEmailAuth.Text = "";
                      txtEmailServer.Text = server;
                  }
              }
             UpdateEmailAuthHint();
         }

         void UpdateEmailAuthHint()
         {
             string server = txtEmailServer == null ? cfg.EmailServer : txtEmailServer.Text.Trim();
             int provider = EmailProviderIndexOf(server);
              bool gmail = provider == 3;
             if (emailAuthHint != null)
                 emailAuthHint.Text = cfg.EmailPasswordRequired ? "请重新填写 IMAP 授权码 / 应用专用密码"
                     : cfg.EmailAuthUndecryptable
                     ? "已失效：换机或换了 Windows 账户，请重新填写"
                     : gmail ? "使用 Google 16 位应用密码"
                     : "使用授权码 / 应用密码，非登录密码";
             if (emailProviderHint != null)
                 emailProviderHint.Text = gmail
                      ? "Gmail：开启两步验证后生成应用密码。"
                     : provider >= 0 && provider <= 2
                     ? "QQ / 163 / 126：开启 IMAP 后获取授权码。"
                      : "使用 IMAP 授权码或应用密码。";
              if (btnEmailAuthHelp != null)
                  btnEmailAuthHelp.Visibility = gmail ? Visibility.Visible : Visibility.Collapsed;
              Button guide = Find<Button>((FrameworkElement)Content, "btnEmailGuide");
              if (guide != null) guide.Visibility = gmail ? Visibility.Visible : Visibility.Collapsed;
         }

         static List<string> EmailKeywordList(string text)
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

         void RenderEmailKeywordTags()
         {
             if (emailKeywordTags == null || txtEmailKeywords == null) return;
             emailKeywordTags.Children.Clear();
             UpdateEmailPreview();
             foreach (string keyword in EmailKeywordList(txtEmailKeywords.Text))
             {
                 string value = keyword;
                 Border chip = new Border { Background = MakeBrush(0xE8, 0xF2, 0xFC), CornerRadius = new CornerRadius(12),
                     Padding = new Thickness(10, 2, 6, 2), Margin = new Thickness(0, 0, 6, 6) };
                 StackPanel content = new StackPanel { Orientation = Orientation.Horizontal };
                 content.Children.Add(new TextBlock { Text = value, Foreground = AccentTx, FontSize = 13,
                     VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 250, ToolTip = value });
                 Button remove = new Button { Content = "×", Width = 24, Height = 24, FontSize = 16,
                     Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = FgFaint,
                     Cursor = Cursors.Hand, ToolTip = "移除关键词", Margin = new Thickness(4, 0, 0, 0) };
                 System.Windows.Automation.AutomationProperties.SetName(remove, "移除关键词 " + value);
                 remove.Click += delegate
                 {
                     List<string> words = EmailKeywordList(txtEmailKeywords.Text);
                     words.Remove(value);
                     txtEmailKeywords.Text = string.Join("\n", words.ToArray());
                     QueueSection(SettingsSection.EmailRules);
                 };
                 content.Children.Add(remove); chip.Child = content; emailKeywordTags.Children.Add(chip);
             }
         }

         void AddEmailKeywords()
         {
             if (txtEmailKeywordAdd == null || txtEmailKeywords == null) return;
             // 平时不显示输入框：第一次点“添加”先展开并聚焦，输入后再点（或回车）才写入。
             if (txtEmailKeywordAdd.Visibility != Visibility.Visible)
             {
                 txtEmailKeywordAdd.Visibility = Visibility.Visible;
                 txtEmailKeywordAdd.Focus();
                 return;
             }
             string text = string.Join("\n", EmailKeywordList(txtEmailKeywords.Text + "\n" + txtEmailKeywordAdd.Text).ToArray());
             string normalized, error;
             if (!MailRules.NormalizeLines(text, out normalized, out error)) { ShowToast("主题关键词无效：" + error); return; }
             txtEmailKeywords.Text = normalized;
             txtEmailKeywordAdd.Clear();
             QueueSection(SettingsSection.EmailRules);
         }

         void UpdateEmailPreview()
         {
             if (lblEmailPreviewResult == null || txtEmailSubjectPreview == null) return;
             string normalized, error;
             lblEmailPreviewResult.Text = !MailRules.NormalizeLines(txtEmailKeywords.Text, out normalized, out error)
                 ? "规则无效：" + error : txtEmailSubjectPreview.Text.Length == 0 ? "输入主题查看是否会被接收"
                 : MailRules.SubjectMatches(txtEmailSubjectPreview.Text, normalized) ? "主题命中；仍需满足发件人和时效规则" : "主题未命中，将跳过";
         }

         public void UpdateEmailStatus()
         {
             string storage = string.Join("\n", new string[] { Config.LastSaveError ?? "", History.LastSaveError ?? "" }).Trim();
             if (lblStorageError != null) lblStorageError.Text = storage;
             Border banner = Find<Border>((FrameworkElement)Content, "storageErrorBanner");
             if (banner != null) banner.Visibility = storage.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
             if (lblQqStatus != null) lblQqStatus.Text = !Program.EmailConfigured() ? "请先配置并启用邮箱"
                 : !cfg.QqPopupEnabled ? "联动已关闭" : "监测中 · 最近提醒 " + QqPopupWatcher.LastSignalText;
              if (chkQqPopup != null) chkQqPopup.IsEnabled = Program.EmailConfigured();
              UpdateReceiveSummary();
         }

         void TestEmailConnection()
         {
              if (emailTestCancel != null) { emailDraftVersion++; emailTestCancel.Cancel(); lblEmailTestResult.Text = "连接测试已取消"; return; }
             Config identity;
             if (!ReadDraft(SettingsSection.Email, out identity)) return;
             if (identity.EmailAccount.Length == 0 || identity.EmailServer.Length == 0
                 || identity.EmailPasswordRequired || identity.EmailAuth.Length == 0)
             { ShowToast("请先填写完整邮箱和 IMAP 授权码 / 应用专用密码"); return; }
              CancellationTokenSource cancel = new CancellationTokenSource(); emailTestCancel = cancel;
              int version = emailDraftVersion;
             btnEmailTest.Content = "取消测试"; lblEmailTestResult.Text = "正在测试认证与只读收件箱连接…";
             Thread worker = new Thread(delegate()
             {
                 OperationResult result;
                 try { result = new OperationResult(OperationState.Success, EmailWatcher.TestConnection(identity, cancel.Token)); }
                 catch (OperationCanceledException) { result = new OperationResult(OperationState.Cancelled, "连接测试已取消"); }
                 catch { result = new OperationResult(cancel.IsCancellationRequested ? OperationState.Cancelled : OperationState.Failed,
                     cancel.IsCancellationRequested ? "连接测试已取消" : "连接测试失败，请检查网络、服务设置与授权"); }
                 try { Dispatcher.BeginInvoke(new Action(delegate
                 {
                      if (emailTestCancel == cancel) emailTestCancel = null;
                      if (cancel.IsCancellationRequested) result = new OperationResult(OperationState.Cancelled, "连接测试已取消");
                     cancel.Dispose(); btnEmailTest.Content = "测试邮箱连接";
                     if (!CanShowTestResult(version, emailDraftVersion)) return;
                     lblEmailTestResult.Text = result.Message + (result.State == OperationState.Success ? " · 此次测试未保存；可点击保存连接" : "");
                     lblEmailTestResult.Foreground = result.State == OperationState.Failed ? Brushes.Firebrick : FgFaint;
                 })); } catch { cancel.Dispose(); }
             });
             worker.IsBackground = true; worker.Name = "email-test"; worker.Start();
         }

    }
}
