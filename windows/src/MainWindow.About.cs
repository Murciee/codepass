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
        Window CreateDiagnosticsWindow()
        {
            string report = Diagnostics.CreateReport(cfg);
            Window dialog = new Window { Title = "脱敏诊断预览", Width = 760, Height = 600, MinWidth = 480, MinHeight = 360,
                Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false,
                Background = MakeBrush(0xF3, 0xF3, 0xF3), FontFamily = FontFamily, Icon = Icon };
            Grid layout = new Grid { Margin = new Thickness(20) };
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.Children.Add(new TextBlock { Text = "报告只包含下方预览内容。确认后保存为文本，再自行分享；不会自动上传。",
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12), Foreground = FgBody });
            TextBox preview = new TextBox { Name = "txtDiagnosticsPreview", Text = report, IsReadOnly = true,
                AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(12), Background = Brushes.White, Foreground = FgBody };
            Grid.SetRow(preview, 1); layout.Children.Add(preview);
            WrapPanel actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            Button save = new Button { Name = "btnDiagnosticsSave", Content = "保存此预览", MinWidth = 110 };
            Button close = new Button { Content = "关闭", MinWidth = 84, Margin = new Thickness(8, 0, 0, 0) };
            FrameworkElement root = Content as FrameworkElement;
            if (root != null) { save.Style = root.Resources["PrimaryBtn"] as Style; close.Style = root.Resources["SecondaryBtn"] as Style; }
            save.Click += delegate
            {
                SaveFileDialog picker = new SaveFileDialog { Filter = "文本报告 (*.txt)|*.txt", DefaultExt = ".txt",
                    FileName = "codepass-diagnostics-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt" };
                if (picker.ShowDialog(dialog) != true) return;
                try { Diagnostics.SavePreview(picker.FileName, report, cfgPath); ShowToast("脱敏诊断已保存"); }
                catch { MessageBox.Show(dialog, "报告保存失败，请选择其他文件，检查目录权限与磁盘空间。", "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning); }
            };
            close.Click += delegate { dialog.Close(); };
            actions.Children.Add(save); actions.Children.Add(close);
            Grid.SetRow(actions, 2); layout.Children.Add(actions);
            dialog.Content = layout;
            dialog.PreviewKeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { dialog.Close(); e.Handled = true; } };
            diagnosticsWindow = dialog;
            dialog.Closed += delegate { if (diagnosticsWindow == dialog) diagnosticsWindow = null; };
            dialog.StateChanged += delegate
            {
                if (dialog.WindowState == WindowState.Minimized && cfg.ShouldAutoLock) LockWindow();
            };
            return dialog;
        }

        void ShowDiagnostics()
        {
            if (lockOverlay != null && lockOverlay.Visibility == Visibility.Visible) return;
            CreateDiagnosticsWindow().ShowDialog();
        }

        // ---------- 关于 / 检查更新 ----------

        void OpenRepoPage()
        {
            if (Updater.IsPlaceholderRepo)
            {
                MessageBox.Show(this, "尚未配置 GitHub 仓库地址，发布前请在源码中替换占位地址。",
                    "codepass", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo(Updater.RepoUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法打开浏览器：" + ex.Message, "codepass",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

         void CheckUpdate()
         {
             if (updateBusy) return;
             updateBusy = true;
              if (btnUpdate != null) btnUpdate.IsEnabled = false;
             SetUpdateStatus("正在检查更新…");
             Updater.CheckForUpdate(
                 delegate(string s) { SetUpdateStatus(s); },
                 delegate
                {
                    // 未更新或更新失败：恢复按钮
                     updateBusy = false;
                     Dispatcher.BeginInvoke(new Action(delegate
                     {
                          if (btnUpdate != null) btnUpdate.IsEnabled = true;
                     }));
                 });
         }

         void SetUpdateStatus(string text)
         {
             if (!Dispatcher.CheckAccess())
             {
                 Dispatcher.BeginInvoke(new Action(delegate { SetUpdateStatus(text); }));
                 return;
             }
             if (lblUpdateStatus != null) lblUpdateStatus.Text = text;
         }
    }
}
