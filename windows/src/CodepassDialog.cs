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
      sealed class CodepassDialog : Window
      {
          readonly MessageBoxButton buttonSet;
          MessageBoxResult result = MessageBoxResult.Cancel;

          public static MessageBoxResult Show(Window owner, string message, string title,
              MessageBoxButton buttons, MessageBoxImage image)
          {
              CodepassDialog dialog = new CodepassDialog(owner, message, title, buttons, image);
              dialog.ShowDialog();
              return dialog.result;
          }

          CodepassDialog(Window owner, string message, string title,
              MessageBoxButton buttons, MessageBoxImage image)
          {
              buttonSet = buttons;
              Owner = owner;
              Title = title;
              Width = 470;
              SizeToContent = SizeToContent.Height;
               WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
               ResizeMode = ResizeMode.NoResize;
              WindowStyle = WindowStyle.None;
              AllowsTransparency = true;
              Background = Brushes.Transparent;
              ShowInTaskbar = false;
              FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI");
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
              close.Click += delegate { Finish(MessageBoxResult.Cancel); };
              titleBar.Children.Add(close);
              Grid.SetRow(titleBar, 0);
              layout.Children.Add(titleBar);

              StackPanel body = new StackPanel();
              body.Orientation = Orientation.Horizontal;
              body.Margin = new Thickness(24, 8, 24, 20);
              Border badge = MakeBadge(image);
              body.Children.Add(badge);
              TextBlock messageText = new TextBlock();
              messageText.Text = message ?? "";
              messageText.TextWrapping = TextWrapping.Wrap;
              messageText.Foreground = MakeBrush(0x1C, 0x1C, 0x1C);
              messageText.VerticalAlignment = VerticalAlignment.Center;
              messageText.Margin = new Thickness(14, 0, 0, 0);
              messageText.MaxWidth = 352;
              body.Children.Add(messageText);
              Grid.SetRow(body, 1);
              layout.Children.Add(body);

              StackPanel buttonsPanel = new StackPanel();
              buttonsPanel.Orientation = Orientation.Horizontal;
              buttonsPanel.HorizontalAlignment = HorizontalAlignment.Right;
              buttonsPanel.Margin = new Thickness(24, 0, 24, 22);
              AddButtons(buttonsPanel, buttons);
              Grid.SetRow(buttonsPanel, 2);
              layout.Children.Add(buttonsPanel);

              shell.Child = layout;
              Grid host = new Grid();
              host.Children.Add(shellShadow);
              host.Children.Add(shell);
              Content = host;
              Loaded += delegate { DisplayMotion.Reveal(this, layout); };
              PreviewKeyDown += OnPreviewKeyDown;
          }

          static SolidColorBrush MakeBrush(byte r, byte g, byte b)
          {
              SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(r, g, b));
              brush.Freeze();
              return brush;
          }

          Border MakeBadge(MessageBoxImage image)
          {
              string glyph = "i";
              Brush background = MakeBrush(0x00, 0x78, 0xD4);
              if (image == MessageBoxImage.Warning)
              {
                  glyph = "!";
                  background = MakeBrush(0xC8, 0x6A, 0x00);
              }
              else if (image == MessageBoxImage.Error)
              {
                  glyph = "×";
                  background = MakeBrush(0xC4, 0x2B, 0x1C);
              }
              else if (image == MessageBoxImage.Question)
              {
                  glyph = "?";
              }

              Border badge = new Border();
              badge.Width = 30;
              badge.Height = 30;
              badge.CornerRadius = new CornerRadius(15);
              badge.Background = background;
              badge.VerticalAlignment = VerticalAlignment.Center;
              TextBlock text = new TextBlock();
              text.Text = glyph;
              text.Foreground = Brushes.White;
              text.FontSize = 18;
              text.FontWeight = FontWeights.Bold;
              text.HorizontalAlignment = HorizontalAlignment.Center;
              text.VerticalAlignment = VerticalAlignment.Center;
              text.TextAlignment = TextAlignment.Center;
              badge.Child = text;
              return badge;
          }

          void AddButtons(StackPanel panel, MessageBoxButton buttons)
          {
              if (buttons == MessageBoxButton.OK || buttons == MessageBoxButton.OKCancel)
              {
                  AddButton(panel, "确定", MessageBoxResult.OK, true);
              }
              else
              {
                  AddButton(panel, "是", MessageBoxResult.Yes, true);
                  AddButton(panel, "否", MessageBoxResult.No, false);
              }
              if (buttons == MessageBoxButton.OKCancel || buttons == MessageBoxButton.YesNoCancel)
                  AddButton(panel, "取消", MessageBoxResult.Cancel, false);
          }

          void AddButton(StackPanel panel, string text, MessageBoxResult value, bool primary)
          {
              Button button = MakeRoundedButton(text, primary);
              button.IsDefault = primary;
              button.IsCancel = value == MessageBoxResult.Cancel;
              button.Margin = new Thickness(8, 0, 0, 0);
              button.Click += delegate { Finish(value); };
              panel.Children.Add(button);
          }

          void Finish(MessageBoxResult value)
          {
              result = value;
              DialogResult = true;
          }

          void OnPreviewKeyDown(object sender, KeyEventArgs e)
          {
              if (e.Key == Key.Escape)
              {
                  Finish(MessageBoxResult.Cancel);
                  e.Handled = true;
              }
              else if (e.Key == Key.Enter)
              {
                  Button focused = Keyboard.FocusedElement as Button;
                  if (focused != null)
                  {
                      focused.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, focused));
                      e.Handled = true;
                      return;
                  }
                  MessageBoxResult value = buttonSet == MessageBoxButton.OK || buttonSet == MessageBoxButton.OKCancel
                      ? MessageBoxResult.OK : MessageBoxResult.Yes;
                  Finish(value);
                  e.Handled = true;
              }
          }

          internal static Button MakeRoundedButton(string text, bool primary)
          {
              Button button = new Button();
              button.Width = primary ? 88 : 78;
              button.Height = 32;
              button.Padding = new Thickness(0);
              button.Background = Brushes.Transparent;
              button.BorderThickness = new Thickness(0);
              button.FocusVisualStyle = null;
              button.Cursor = Cursors.Hand;
              button.Template = CreateBareButtonTemplate();
              Border border = new Border();
              border.CornerRadius = new CornerRadius(4);
              border.Padding = new Thickness(18, 0, 18, 0);
              Brush normalBackground = primary ? MakeBrush(0x00, 0x78, 0xD4) : Brushes.White;
              Brush hoverBackground = primary ? MakeBrush(0x1A, 0x84, 0xD5) : MakeBrush(0xF6, 0xF6, 0xF6);
              Brush pressedBackground = primary ? MakeBrush(0x33, 0x93, 0xDA) : MakeBrush(0xED, 0xED, 0xED);
              Brush normalBorder = primary ? Brushes.Transparent : MakeBrush(0xEB, 0xEB, 0xEB);
              Brush hoverBorder = primary ? Brushes.Transparent : MakeBrush(0xEB, 0xEB, 0xEB);
              Brush pressedBorder = primary ? Brushes.Transparent : MakeBrush(0xEB, 0xEB, 0xEB);
              border.Background = normalBackground;
              border.BorderBrush = normalBorder;
              border.BorderThickness = primary ? new Thickness(0) : new Thickness(1);
              TextBlock label = new TextBlock();
              label.Text = text;
              label.FontSize = 13.5;
              label.Foreground = primary ? Brushes.White : MakeBrush(0x1C, 0x1C, 0x1C);
              label.HorizontalAlignment = HorizontalAlignment.Center;
              label.VerticalAlignment = VerticalAlignment.Center;
              border.Child = label;
              button.Content = border;
              button.MouseEnter += delegate
              {
                  border.Background = hoverBackground;
                  border.BorderBrush = hoverBorder;
              };
              button.MouseLeave += delegate
              {
                  border.Background = normalBackground;
                  border.BorderBrush = normalBorder;
              };
              button.PreviewMouseLeftButtonDown += delegate
              {
                  border.Background = pressedBackground;
                  border.BorderBrush = pressedBorder;
              };
              button.PreviewMouseLeftButtonUp += delegate
              {
                  border.Background = button.IsMouseOver ? hoverBackground : normalBackground;
                  border.BorderBrush = button.IsMouseOver ? hoverBorder : normalBorder;
              };
              return button;
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
              button.Template = CreateBareButtonTemplate();
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

           internal static ControlTemplate CreateBareButtonTemplate()
          {
              ControlTemplate template = new ControlTemplate(typeof(Button));
              FrameworkElementFactory root = new FrameworkElementFactory(typeof(Border));
              root.SetValue(Border.BackgroundProperty, Brushes.Transparent);
              root.SetValue(Border.BorderThicknessProperty, new Thickness(0));
              FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
              content.SetValue(ContentPresenter.ContentSourceProperty, "Content");
              content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
              content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Stretch);
              root.AppendChild(content);
              template.VisualTree = root;
              return template;
          }

          internal static ControlTemplate CreatePasswordTemplate()
           {
               return CreateFieldTemplate(typeof(PasswordBox));
           }

            // 密码输入框的基础模板
            internal static ControlTemplate CreateFieldTemplate(Type targetType)
           {
              ControlTemplate template = new ControlTemplate(targetType);
              FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
              border.SetValue(Border.BackgroundProperty, MakeBrush(0xFB, 0xFB, 0xFB));
              border.SetValue(Border.BorderBrushProperty, MakeBrush(0xEB, 0xEB, 0xEB));
              border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
              border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
              FrameworkElementFactory host = new FrameworkElementFactory(typeof(ScrollViewer));
              host.Name = "PART_ContentHost";
              host.SetValue(ScrollViewer.FocusableProperty, false);
                host.SetValue(FrameworkElement.MarginProperty, new Thickness(6, 0, 7, 0));
              host.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
              border.AppendChild(host);
              template.VisualTree = border;
              return template;
          }
      }
}
