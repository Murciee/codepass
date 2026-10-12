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
      sealed class PasswordDialog : Window
      {
          readonly PasswordBox box;
          readonly PasswordRevealer revealer;
          readonly Func<string, string> validator;

          public string Password { get { return box == null ? "" : box.Password; } }

          public PasswordDialog(Window owner, string title, string hint, Func<string, string> validator)
          {
              this.validator = validator;
              Owner = owner;
              Title = title;
              Width = 390;
              SizeToContent = SizeToContent.Height;
              WindowStartupLocation = WindowStartupLocation.CenterOwner;
              ResizeMode = ResizeMode.NoResize;
              WindowStyle = WindowStyle.None;
              AllowsTransparency = true;
              Background = Brushes.Transparent;
              ShowInTaskbar = false;
              FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI, Segoe UI");
              FontSize = 13.5;
              if (owner != null) Icon = owner.Icon;

              // 阴影单独一层, 内容层不套 Effect(否则整窗文字被栅格化变糊)
              Border shellShadow = new Border();
              shellShadow.Margin = new Thickness(12);
              shellShadow.Background = MakeBrush(0xF3, 0xF3, 0xF3);
              shellShadow.CornerRadius = new CornerRadius(8);
              shellShadow.IsHitTestVisible = false;
              shellShadow.Effect = new System.Windows.Media.Effects.DropShadowEffect
              {
                  BlurRadius = 18,
                  ShadowDepth = 0,
                  Opacity = 0.12,
                  Color = Color.FromRgb(0x1A, 0x1A, 0x1A)
              };

              Border shell = new Border();
              shell.Margin = new Thickness(12);
              shell.Background = MakeBrush(0xF3, 0xF3, 0xF3);
              shell.BorderBrush = MakeBrush(0xEB, 0xEB, 0xEB);
              shell.BorderThickness = new Thickness(1);
              shell.CornerRadius = new CornerRadius(8);

              Grid layout = new Grid();
              layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(46) });
              layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
              layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

              Grid titleBar = new Grid();
              titleBar.Margin = new Thickness(18, 0, 8, 0);
              titleBar.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
              {
                  if (e.ButtonState == MouseButtonState.Pressed)
                  {
                      try { DragMove(); } catch { }
                  }
              };
              TextBlock titleText = new TextBlock();
              titleText.Text = title;
              titleText.FontSize = 14.5;
              titleText.FontWeight = FontWeights.SemiBold;
              titleText.Foreground = MakeBrush(0x1C, 0x1C, 0x1C);
              titleText.VerticalAlignment = VerticalAlignment.Center;
              titleBar.Children.Add(titleText);
              Button close = MakeCloseButton();
              close.HorizontalAlignment = HorizontalAlignment.Right;
              close.VerticalAlignment = VerticalAlignment.Center;
              close.Click += delegate { DialogResult = false; };
              titleBar.Children.Add(close);
              Grid.SetRow(titleBar, 0);
              layout.Children.Add(titleBar);

              StackPanel panel = new StackPanel();
              panel.Margin = new Thickness(24, 8, 24, 0);

              TextBlock label = new TextBlock();
              label.Text = hint;
              label.Foreground = MakeBrush(0x61, 0x61, 0x61);
              panel.Children.Add(label);

               FrameworkElement ownerRoot = owner == null ? null : owner.Content as FrameworkElement;
               Style addonStyle = ownerRoot == null ? null : ownerRoot.TryFindResource("InputAddon") as Style;
               StackPanel field = new StackPanel();
               field.Orientation = Orientation.Horizontal;
               field.Margin = new Thickness(0, 14, 0, 0);

               box = new PasswordBox();
               TextBox plain = new TextBox();
               Button toggle = new Button();
               if (addonStyle != null)
               {
                   // 和主界面同一套控件：内嵌显示按钮（眼睛）的密钥框。
                   box.Style = ownerRoot.TryFindResource("InnerPassword") as Style;
                   plain.Style = ownerRoot.TryFindResource("InnerInput") as Style;
                   toggle.Style = ownerRoot.TryFindResource("InlineIconBtn") as Style;
                   Border addon = new Border();
                   addon.Style = addonStyle;
                   addon.Width = 300;
                   addon.HorizontalAlignment = HorizontalAlignment.Left;
                   Grid inner = new Grid();
                   inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                   inner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                   Grid holder = new Grid();
                   holder.Children.Add(box);
                   holder.Children.Add(plain);
                   inner.Children.Add(holder);
                   Grid.SetColumn(toggle, 1);
                   inner.Children.Add(toggle);
                   addon.Child = inner;
                   field.Children.Add(addon);
               }
               else
               {
                   box.Template = CodepassDialog.CreatePasswordTemplate();
                   box.Height = 32;
                   box.Width = 240;
                   box.VerticalContentAlignment = VerticalAlignment.Center;
                   plain.Template = CodepassDialog.CreateFieldTemplate(typeof(TextBox));
                   plain.Height = 32;
                   plain.Width = 240;
                   plain.VerticalContentAlignment = VerticalAlignment.Center;
                   toggle.Style = ownerRoot == null ? null : ownerRoot.TryFindResource("SecondaryBtn") as Style;
                   toggle.Width = 68;
                   toggle.Margin = new Thickness(8, 0, 0, 0);
                   toggle.VerticalAlignment = VerticalAlignment.Center;
                   field.Children.Add(box);
                   field.Children.Add(plain);
                   field.Children.Add(toggle);
               }
               plain.Visibility = Visibility.Collapsed;

                revealer = new PasswordRevealer(box, plain, toggle);

              panel.Children.Add(field);
              Grid.SetRow(panel, 1);
              layout.Children.Add(panel);

              StackPanel buttons = new StackPanel();
              buttons.Orientation = Orientation.Horizontal;
              buttons.HorizontalAlignment = HorizontalAlignment.Right;
              buttons.Margin = new Thickness(24, 14, 24, 22);
              Button cancel = CodepassDialog.MakeRoundedButton("取消", false);
              cancel.Margin = new Thickness(8, 0, 0, 0);
              cancel.Click += delegate { DialogResult = false; };
              buttons.Children.Add(cancel);
              Button ok = CodepassDialog.MakeRoundedButton("确定", true);
              ok.Margin = new Thickness(8, 0, 0, 0);
              ok.Click += delegate { TryConfirm(); };
              buttons.Children.Add(ok);
              Grid.SetRow(buttons, 2);
              layout.Children.Add(buttons);

              KeyEventHandler onKey = delegate(object sender, KeyEventArgs e)
              {
                  if (e.Key == Key.Enter) { TryConfirm(); e.Handled = true; }
                  else if (e.Key == Key.Escape) { DialogResult = false; e.Handled = true; }
              };
              box.KeyDown += onKey;
              plain.KeyDown += onKey;
              shell.Child = layout;
              Grid host = new Grid();
              host.Children.Add(shellShadow);
              host.Children.Add(shell);
              Content = host;
              Loaded += delegate { DisplayMotion.Reveal(this, layout); box.Focus(); };
          }

           void TryConfirm()
          {
              string error = validator == null ? null : validator(box.Password);
               if (!String.IsNullOrEmpty(error))
               {
                   revealer.Clear();
                   MessageBox.Show(this, error, "安全性", MessageBoxButton.OK, MessageBoxImage.Warning);
                   box.Focus();
                  return;
              }
               DialogResult = true;
           }

           internal void ClearInput()
           {
               if (revealer != null) revealer.Clear();
           }

          static SolidColorBrush MakeBrush(byte r, byte g, byte b)
          {
              SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(r, g, b));
              brush.Freeze();
              return brush;
          }

          static Button MakeCloseButton()
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
              Border border = new Border();
              border.CornerRadius = new CornerRadius(4);
              TextBlock text = new TextBlock();
              text.Text = "×";
              text.FontSize = 18;
              text.Foreground = MakeBrush(0x1C, 0x1C, 0x1C);
              text.HorizontalAlignment = HorizontalAlignment.Center;
              text.VerticalAlignment = VerticalAlignment.Center;
              border.Child = text;
              button.Content = border;
              return button;
          }
      }
}
