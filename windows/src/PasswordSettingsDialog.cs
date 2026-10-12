using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
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
            Background = SystemColors.WindowBrush; Foreground = SystemColors.WindowTextBrush; FontFamily = owner.FontFamily; FontSize = 14;
            StackPanel panel = new StackPanel { Margin = new Thickness(24) };
            panel.Children.Add(new TextBlock { Text = Title, FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
            oldPassword = Field(panel, "当前密码", config.LockEnabled);
            newPassword = Field(panel, "新密码（至少 8 个字符）", !disable);
            confirmation = Field(panel, "再次输入新密码", !disable);
            error = new TextBlock { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) }; panel.Children.Add(error);
            WrapPanel actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            Button cancel = new Button { Content = "取消", MinWidth = 80, Height = 32, IsCancel = true, Margin = new Thickness(0, 0, 8, 0) };
            Button save = new Button { Content = disable ? "关闭密码锁" : "保存密码", MinWidth = 100, Height = 32, IsDefault = true };
            save.Click += delegate { Confirm(); }; actions.Children.Add(cancel); actions.Children.Add(save); panel.Children.Add(actions);
            Content = panel;
            Loaded += delegate { if (config.LockEnabled) oldPassword.Focus(); else newPassword.Focus(); };
            Closed += delegate { oldPassword.Clear(); newPassword.Clear(); confirmation.Clear(); };
        }
        static PasswordBox Field(Panel panel, string label, bool visible)
        {
            TextBlock name = new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 5), Visibility = visible ? Visibility.Visible : Visibility.Collapsed };
            PasswordBox field = new PasswordBox { Height = 32, Padding = new Thickness(6), Visibility = name.Visibility };
            AutomationProperties.SetName(field, label); panel.Children.Add(name); panel.Children.Add(field); return field;
        }
        internal static string Validate(bool locked, bool oldValid, bool disable, string password, string confirm)
        {
            if (locked && !oldValid) return "当前密码不正确";
            if (!disable && (password ?? "").Length < 8) return "新密码至少需要 8 个字符";
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
}
