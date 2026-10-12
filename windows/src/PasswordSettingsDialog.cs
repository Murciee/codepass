using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;

namespace Codepass
{
    sealed class PasswordSettingsDialog : Window
    {
        readonly PasswordBox oldPassword, newPassword, confirmation;
        readonly TextBlock error;
        readonly Config config;
        readonly string path;
        readonly bool disable;
        internal PasswordSettingsDialog(Window owner, Config config, string path, bool disable)
        {
            this.config = config; this.path = path; this.disable = disable;
            Owner = owner; Title = disable ? "关闭密码锁" : config.LockEnabled ? "修改密码" : "设置密码锁";
            Width = 420; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
            // 和主窗口/其它弹窗一套：无边框 + 8px 圆角 + #F3F3F3 底色 + 外圈阴影。
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            Foreground = SystemColors.WindowTextBrush; FontFamily = owner.FontFamily; FontSize = 14;
            Border shadow = new Border { Margin = new Thickness(12), Background = DialogChrome.Brush(0xF3, 0xF3, 0xF3), CornerRadius = new CornerRadius(8), IsHitTestVisible = false };
            shadow.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 0, Opacity = 0.12, Color = Color.FromRgb(0x1A, 0x1A, 0x1A) };
            Border shell = new Border { Margin = new Thickness(12), Background = DialogChrome.Brush(0xF3, 0xF3, 0xF3), BorderBrush = DialogChrome.Brush(0xEB, 0xEB, 0xEB), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) };
            Grid shellLayout = new Grid();
            shellLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(46) });
            shellLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid titleBar = new Grid { Margin = new Thickness(18, 0, 8, 0) };
            titleBar.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { } }
            };
            Button closeButton = DialogChrome.CloseButton();
            closeButton.HorizontalAlignment = HorizontalAlignment.Right;
            closeButton.VerticalAlignment = VerticalAlignment.Center;
            closeButton.Click += delegate { DialogResult = false; };
            titleBar.Children.Add(closeButton);
            Grid.SetRow(titleBar, 0);
            shellLayout.Children.Add(titleBar);
            StackPanel panel = new StackPanel { Margin = new Thickness(24, 0, 24, 22) };
            panel.Children.Add(new TextBlock { Text = Title, FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
            oldPassword = Field(panel, "当前密码", config.LockEnabled);
            newPassword = Field(panel, "新密码（至少 4 个字符）", !disable);
            confirmation = Field(panel, "再次输入新密码", !disable);
            error = new TextBlock { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) }; panel.Children.Add(error);
            WrapPanel actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            Button cancel = new Button { Content = "取消", MinWidth = 92, Height = 32, IsCancel = true, Margin = new Thickness(0, 0, 8, 0) };
            Button save = new Button { Content = disable ? "关闭密码锁" : "保存密码", MinWidth = 108, Height = 32, IsDefault = true };
            Style secondary = Res("SecondaryBtn");
            if (secondary != null) cancel.Style = secondary;
            Style primary = Res("PrimaryBtn");
            if (primary != null) save.Style = primary;
            save.Click += delegate { Confirm(); }; actions.Children.Add(cancel); actions.Children.Add(save); panel.Children.Add(actions);
            Grid.SetRow(panel, 1);
            shellLayout.Children.Add(panel);
            shell.Child = shellLayout;
            Grid host = new Grid();
            host.Children.Add(shadow);
            host.Children.Add(shell);
            Content = host;
            Loaded += delegate { DisplayMotion.Reveal(this, shellLayout); if (config.LockEnabled) oldPassword.Focus(); else newPassword.Focus(); };
            Closed += delegate { oldPassword.Clear(); newPassword.Clear(); confirmation.Clear(); foreach (PasswordRevealer revealer in revealers) revealer.Clear(); };
        }
        readonly List<PasswordRevealer> revealers = new List<PasswordRevealer>();
        Style Res(string key)
        {
            FrameworkElement root = Owner == null ? null : Owner.Content as FrameworkElement;
            return root == null ? null : root.TryFindResource(key) as Style;
        }
        /// <summary>和主界面同一套控件：一体化边框里的密钥框 + 内嵌显示按钮（眼睛/闭眼）。</summary>
        PasswordBox Field(Panel panel, string label, bool visible)
        {
            TextBlock name = new TextBlock { Text = label, Margin = new Thickness(0, 12, 0, 6), Visibility = visible ? Visibility.Visible : Visibility.Collapsed };
            panel.Children.Add(name);
            PasswordBox masked = new PasswordBox();
            AutomationProperties.SetName(masked, label);
            Style addonStyle = Res("InputAddon");
            if (addonStyle == null)
            {
                masked.Height = 32;
                masked.Padding = new Thickness(6);
                masked.Visibility = name.Visibility;
                panel.Children.Add(masked);
                return masked;
            }
            masked.Style = Res("InnerPassword");
            TextBox plain = new TextBox();
            plain.Style = Res("InnerInput");
            plain.Visibility = Visibility.Collapsed;
            Button toggle = new Button();
            toggle.Style = Res("InlineIconBtn");
            Border addon = new Border();
            addon.Style = addonStyle;
            addon.HorizontalAlignment = HorizontalAlignment.Stretch;
            addon.Visibility = name.Visibility;
            Grid inner = new Grid();
            inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid holder = new Grid();
            holder.Children.Add(masked);
            holder.Children.Add(plain);
            inner.Children.Add(holder);
            Grid.SetColumn(toggle, 1);
            inner.Children.Add(toggle);
            addon.Child = inner;
            panel.Children.Add(addon);
            revealers.Add(new PasswordRevealer(masked, plain, toggle));
            return masked;
        }
        internal static string Validate(bool locked, bool oldValid, bool disable, string password, string confirm)
        {
            if (locked && !oldValid) return "当前密码不正确";
            if (!disable && (password ?? "").Length < 4) return "新密码至少需要 4 个字符";
            if (!disable && password != confirm) return "两次输入的新密码不一致";
            return null;
        }
        void Confirm()
        {
            string problem = Validate(config.LockEnabled, !config.LockEnabled || config.VerifyPassword(oldPassword.Password), disable, newPassword.Password, confirmation.Password);
            if (problem != null) { error.Text = problem; return; }
            try
            {
                lock (Config.SaveGate)
                {
                    Config next = new Config(); next.CopyFrom(config);
                    if (disable) next.ClearPassword(); else next.SetPassword(newPassword.Password);
                    next.Save(path); config.CopyFrom(next);
                }
                DialogResult = true;
            }
            catch { error.Text = "保存失败，原密码锁保持不变。请检查目录权限后重试。"; }
        }
    }

    /// <summary>弹窗外壳：和主窗口一致的底色、8px 圆角与关闭按钮。</summary>
    static class DialogChrome
    {
        internal static SolidColorBrush Brush(byte r, byte g, byte b)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
        internal static Button CloseButton()
        {
            Button button = new Button();
            button.Width = 34;
            button.Height = 30;
            button.Padding = new Thickness(0);
            button.Background = Brushes.Transparent;
            button.BorderThickness = new Thickness(0);
            button.FocusVisualStyle = null;
            button.Cursor = Cursors.Hand;
            button.Template = CodepassDialog.CreateBareButtonTemplate();
            Border border = new Border { CornerRadius = new CornerRadius(4) };
            border.Child = new TextBlock { Text = "×", FontSize = 18, Foreground = Brush(0x1C, 0x1C, 0x1C), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            button.Content = border;
            return button;
        }
    }
}
