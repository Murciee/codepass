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
              if (cfg.LockEnabled)
              {
                  PasswordDialog dialog = new PasswordDialog(this, "验证当前密码", "请输入当前密码",
                      delegate(string password) { return cfg.VerifyPassword(password) ? null : "密码不正确。"; });
                  if (dialog.ShowDialog() != true) { dialog.ClearInput(); return; }
                  dialog.ClearInput();
                  MessageBoxResult action = MessageBox.Show(this, "点击“是”修改密码，点击“否”关闭密码锁。\r\n\r\n密码锁仅保护界面，不加密磁盘文件；自动锁定默认关闭，可在设置页勾选。", "安全性",
                      MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                  if (action == MessageBoxResult.No)
                  {
                      Config previous = new Config();
                      previous.CopyFrom(cfg);
                      cfg.ClearPassword();
                      try { cfg.Save(cfgPath); }
                      catch (Exception ex)
                      {
                          bool restored = RestoreSettings(previous, false);
                          settingsRecoveryRequired = !restored;
                          MessageBox.Show(this, restored
                              ? "保存失败：" + ex.Message + "，已还原。"
                              : "保存失败且还原失败，请检查配置文件。", "安全性", MessageBoxButton.OK, MessageBoxImage.Error);
                          return;
                      }
                      RefreshSecurityControls();
                      settingsRecoveryRequired = false;
                      MessageBox.Show(this, "密码锁已关闭。", "安全性", MessageBoxButton.OK, MessageBoxImage.Information);
                     return;
                 }
                 if (action != MessageBoxResult.Yes) return;
             }

               PasswordDialog first = new PasswordDialog(this, "设置程序密码", "请输入新密码（至少 8 个字符）",
                   delegate(string password) { return password.Length >= 8 ? null : "密码至少需要 8 个字符。"; });
               if (first.ShowDialog() != true) { first.ClearInput(); return; }
               string newPassword = first.Password;
               first.ClearInput();
               PasswordDialog second = new PasswordDialog(this, "确认程序密码", "请再次输入新密码",
                   delegate(string password) { return password == newPassword ? null : "两次输入的密码不一致。"; });
               if (second.ShowDialog() != true) { second.ClearInput(); return; }
               second.ClearInput();
               Config oldSecurity = new Config();
               oldSecurity.CopyFrom(cfg);
              try
              {
                  cfg.SetPassword(newPassword);
                  cfg.Save(cfgPath);
              }
               catch (Exception ex)
               {
                   bool restored = RestoreSettings(oldSecurity, false);
                   settingsRecoveryRequired = !restored;
                   MessageBox.Show(this, restored
                      ? "保存失败：" + ex.Message + "，已还原。"
                      : "保存失败且还原失败，请检查配置文件。", "安全性", MessageBoxButton.OK, MessageBoxImage.Error);
                  return;
               }
              settingsRecoveryRequired = false;
              RefreshSecurityControls();
             MessageBox.Show(this, cfg.AutoLockEnabled
                 ? "密码锁已启用。"
                 : "密码锁已启用；自动锁定保持关闭，可用“立即锁定”手动锁定。", "安全性",
                 MessageBoxButton.OK, MessageBoxImage.Information);
             if (cfg.ShouldAutoLock) LockWindow();
         }

          void LockWindow()
          {
               if (!cfg.LockEnabled || lockOverlay == null) return;
               if (historyRawWindow != null) historyRawWindow.Close();
               if (diagnosticsWindow != null) diagnosticsWindow.Close();
               if (emailTestCancel != null) emailTestCancel.Cancel();
              // 隐藏可能仍在显示的轻提示，避免验证码短暂浮在锁屏上方。
              HideToast();
              lockOverlay.Visibility = Visibility.Visible;
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
          }

         // ---------- 密钥字段掩码 ----------

         void BuildSecretFields(FrameworkElement root)
         {
             Style passwordStyle = root.Resources["PasswordInput"] as Style;
             Style inputStyle = root.Resources["Input"] as Style;
             Style buttonStyle = root.Resources["SecondaryBtn"] as Style;
             StackPanel tokenHost = Find<StackPanel>(root, "tokenHost");
             StackPanel ntfyHost = Find<StackPanel>(root, "ntfyTokenHost");
             StackPanel emailAuthHost = Find<StackPanel>(root, "emailAuthHost");
             fldToken = new SecretField(passwordStyle, inputStyle, buttonStyle, 300);
              fldNtfyToken = new SecretField(passwordStyle, inputStyle, buttonStyle, 300);
              fldEmailAuth = new SecretField(passwordStyle, inputStyle, buttonStyle, 300);
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
