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
    /// <summary>默认掩码、可切换显示的密钥输入框（PasswordBox 与 TextBox 叠放切换）。</summary>
    sealed class SecretField
    {
        readonly PasswordBox masked = new PasswordBox();
        readonly TextBox plain = new TextBox();
        readonly Button toggle = new Button();
        bool revealed;

        public event Action Committed;
        public SecretField(Style passwordStyle, Style textStyle, Style buttonStyle, double width)
        {
            masked.Style = passwordStyle;
            masked.Width = width;
            masked.VerticalAlignment = VerticalAlignment.Center;
            plain.Style = textStyle;
            plain.Width = width;
            plain.VerticalAlignment = VerticalAlignment.Center;
            plain.Visibility = Visibility.Collapsed;
            toggle.Style = buttonStyle;
            toggle.Content = "显示";
            toggle.Width = 68;
            toggle.Height = 32;
            toggle.Margin = new Thickness(8, 0, 0, 0);
            toggle.VerticalAlignment = VerticalAlignment.Center;

            toggle.Click += delegate { revealed = !revealed; Apply(); };
            masked.PasswordChanged += delegate { if (!revealed) plain.Text = masked.Password; };
            plain.TextChanged += delegate { if (revealed) masked.Password = plain.Text; };
             masked.LostFocus += delegate { if (Committed != null) Committed(); };
             plain.LostFocus += delegate { if (Committed != null) Committed(); };
             KeyEventHandler onEnter = delegate(object s, KeyEventArgs e) { if (e.Key == Key.Return && Committed != null) Committed(); };
             masked.KeyDown += onEnter;
             plain.KeyDown += onEnter;
        }

        public string Text
        {
            get { return revealed ? plain.Text : masked.Password; }
            set
            {
                string v = value ?? "";
                masked.Password = v;
                plain.Text = v;
            }
        }

        public void Attach(Panel host)
        {
            host.Children.Add(masked);
            host.Children.Add(plain);
            host.Children.Add(toggle);
        }

        void Apply()
        {
            if (revealed)
            {
                plain.Text = masked.Password;
                plain.Visibility = Visibility.Visible;
                masked.Visibility = Visibility.Collapsed;
                toggle.Content = "隐藏";
                plain.Focus();
                plain.CaretIndex = plain.Text.Length;
            }
            else
            {
                masked.Password = plain.Text;
                masked.Visibility = Visibility.Visible;
                plain.Visibility = Visibility.Collapsed;
                toggle.Content = "显示";
                masked.Focus();
            }
        }
    }
}
