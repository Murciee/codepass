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
      /// <summary>密码框的显示/隐藏按钮：同位置切换明文框和掩码框，两边内容始终同步。</summary>
      sealed class PasswordRevealer
      {
          readonly PasswordBox masked;
          readonly TextBox plain;
          readonly Button toggle;
          bool revealed;
          bool syncing;

            public PasswordRevealer(PasswordBox masked, TextBox plain, Button toggle)
          {
              this.masked = masked;
              this.plain = plain;
              this.toggle = toggle;
               plain.Visibility = Visibility.Collapsed;
               plain.Text = masked.Password;
               UpdateToggleVisual();
              masked.PasswordChanged += delegate
              {
                  if (syncing || revealed) return;
                  syncing = true;
                  plain.Text = masked.Password;
                  syncing = false;
              };
              plain.TextChanged += delegate
              {
                  if (syncing || !revealed) return;
                  syncing = true;
                  masked.Password = plain.Text;
                  syncing = false;
              };
                toggle.Click += delegate { revealed = !revealed; Apply(); };
           }

           public void Clear()
           {
               syncing = true;
               masked.Password = "";
               plain.Text = "";
               syncing = false;
               revealed = false;
               plain.Visibility = Visibility.Collapsed;
               masked.Visibility = Visibility.Visible;
                UpdateToggleVisual();
           }

          void Apply()
          {
              if (revealed)
              {
                  plain.Text = masked.Password;
                  masked.Visibility = Visibility.Collapsed;
                  plain.Visibility = Visibility.Visible;
                  plain.Focus();
                  plain.CaretIndex = plain.Text.Length;
              }
              else
              {
                  masked.Password = plain.Text;
                  plain.Visibility = Visibility.Collapsed;
                  masked.Visibility = Visibility.Visible;
                  masked.Focus();
              }
                UpdateToggleVisual();
            }

            void UpdateToggleVisual()
            {
                toggle.Content = revealed ? "隐藏" : "显示";
                toggle.ToolTip = revealed ? "隐藏密码" : "显示密码";
            }
      }
}
