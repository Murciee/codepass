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
          void ConfigureSecurity()
          {
              FlushSettings();
              PasswordSettingsDialog dialog = new PasswordSettingsDialog(this, cfg, cfgPath, false);
              if (dialog.ShowDialog() == true)
              { RefreshSecurityControls(); SettingsFeedback("密码锁已保存 · 自动锁定设置保持原值", false); if (cfg.ShouldAutoLock) LockWindow(); }
          }

          void DisablePasswordLock()
          {
              FlushSettings();
              if (!cfg.LockEnabled) return;
              PasswordSettingsDialog dialog = new PasswordSettingsDialog(this, cfg, cfgPath, true);
              if (dialog.ShowDialog() == true) { RefreshSecurityControls(); SettingsFeedback("密码锁已关闭", false); }
          }

          void LockWindow()
          {
               if (!cfg.LockEnabled || lockOverlay == null) return;
               CloseHistoryActions();
               if (historyRawWindow != null) historyRawWindow.Close();
               if (diagnosticsWindow != null) diagnosticsWindow.Close();
                CancelConnectionTests();
              // 隐藏可能仍在显示的轻提示，避免验证码短暂浮在锁屏上方。
              HideToast();
               lockOverlay.Visibility = Visibility.Visible;
               SetLockedContent(true);
              if (unlockRevealer != null)
                  unlockRevealer.Clear();
              else if (txtUnlockPassword != null)
              {
                  txtUnlockPassword.Password = "";
              }
              if (txtUnlockPassword != null) txtUnlockPassword.Focus();
              if (lblUnlockError != null) lblUnlockError.Text = "";
          }

          void TryUnlock()
          {
              bool verified = txtUnlockPassword != null && cfg.VerifyPassword(txtUnlockPassword.Password);
              if (unlockRevealer != null) unlockRevealer.Clear();
              else if (txtUnlockPassword != null) txtUnlockPassword.Password = "";
              if (!verified)
              {
                  if (lblUnlockError != null) lblUnlockError.Text = "密码不正确，请重试。";
                  return;
              }
               lockOverlay.Visibility = Visibility.Collapsed;
               SetLockedContent(false);
          }

          void SetLockedContent(bool locked)
          {
              FrameworkElement root = (FrameworkElement)Content;
              foreach (string name in new string[] { "businessContent", "settingsFeedbackHost" })
              {
                  FrameworkElement element = Find<FrameworkElement>(root, name);
                  if (element != null) { element.IsEnabled = !locked; element.Visibility = locked ? Visibility.Hidden : Visibility.Visible; }
              }
          }

         // ---------- 密钥字段掩码 ----------

         void BuildSecretFields(FrameworkElement root)
         {
             Style passwordStyle = root.Resources["InnerPassword"] as Style;
             Style inputStyle = root.Resources["InnerInput"] as Style;
             Style iconStyle = root.Resources["InlineIconBtn"] as Style;
             StackPanel tokenHost = Find<StackPanel>(root, "tokenHost");
             StackPanel ntfyHost = Find<StackPanel>(root, "ntfyTokenHost");
             StackPanel emailAuthHost = Find<StackPanel>(root, "emailAuthHost");
             fldToken = new SecretField(passwordStyle, inputStyle, iconStyle);
              fldNtfyToken = new SecretField(passwordStyle, inputStyle, iconStyle);
              fldEmailAuth = new SecretField(passwordStyle, inputStyle, iconStyle);
                 AttachSecret(tokenHost, fldToken, "局域网接收必填");
               AttachSecret(ntfyHost, fldNtfyToken, "私有服务器可选");
               if (emailAuthHost != null) fldEmailAuth.Attach(emailAuthHost);
         }

          static void AttachSecret(StackPanel host, SecretField field, string hint)
          {
              if (host == null) return;
              field.Attach(host);
              host.ToolTip = hint;
         }

         /// <summary>按当前配置刷新密码锁相关控件的可见/可用状态。</summary>
          void RefreshSecurityControls()
          {
              Button manage = Find<Button>((FrameworkElement)Content, "btnSecurity");
              if (manage != null) manage.Content = cfg.LockEnabled ? "修改密码" : "设置密码锁";
              Button disable = Find<Button>((FrameworkElement)Content, "btnDisableLock");
              if (disable != null) disable.Visibility = cfg.LockEnabled ? Visibility.Visible : Visibility.Collapsed;
             if (chkAutoLock != null)
             {
                 chkAutoLock.IsChecked = cfg.AutoLockEnabled;
                 chkAutoLock.IsEnabled = cfg.LockEnabled;
             }
             if (btnLock != null)
                 btnLock.Visibility = cfg.LockEnabled ? Visibility.Visible : Visibility.Collapsed;
         }

    }
}
