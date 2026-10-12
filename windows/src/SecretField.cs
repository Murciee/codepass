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
        bool synchronizing;
        Panel host;
        int focusVersion;
        public event Action Changed;

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
            masked.PasswordChanged += delegate { if (!synchronizing && !revealed) { plain.Text = masked.Password; if (Changed != null) Changed(); } };
            plain.TextChanged += delegate { if (!synchronizing && revealed) { masked.Password = plain.Text; if (Changed != null) Changed(); } };
             masked.LostKeyboardFocus += CommitWhenLeaving;
             plain.LostKeyboardFocus += CommitWhenLeaving;
             toggle.LostKeyboardFocus += CommitWhenLeaving;
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
                bool changed = Text != v;
                synchronizing = true;
                try { masked.Password = v; plain.Text = v; } finally { synchronizing = false; }
                if (changed && Changed != null) Changed();
            }
        }

        public void Attach(Panel host)
        {
            this.host = host;
            Grid layout = new Grid { Tag = "FieldActions", HorizontalAlignment = HorizontalAlignment.Stretch };
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            layout.ColumnDefinitions.Add(new ColumnDefinition());
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid input = new Grid();
            input.SetResourceReference(FrameworkElement.WidthProperty, "SettingsInputWidth");
            masked.Width = plain.Width = double.NaN;
            input.Children.Add(masked); input.Children.Add(plain); layout.Children.Add(input);
            toggle.HorizontalAlignment = HorizontalAlignment.Left;
            Grid.SetColumn(toggle, 1); layout.Children.Add(toggle);
            host.Children.Add(layout);
            System.Windows.Automation.AutomationProperties.SetName(masked, host.Name == "emailAuthHost" ? "邮箱授权码" : "访问令牌");
            System.Windows.Automation.AutomationProperties.SetName(plain, host.Name == "emailAuthHost" ? "邮箱授权码" : "访问令牌");
        }
        void CommitWhenLeaving(object sender, KeyboardFocusChangedEventArgs e)
        {
            int version = ++focusVersion;
            masked.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
            {
                if (version == focusVersion && host != null && !host.IsKeyboardFocusWithin && Committed != null) Committed();
            }));
        }

        void Apply()
        {
            synchronizing = true;
            try
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
            finally { synchronizing = false; }
        }
    }
}
