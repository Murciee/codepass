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
    /// <summary>密钥框内嵌的显示按钮图标：睁眼 / 闭眼两态，都是矢量细线 + 睫毛。</summary>
    static class RevealIcon
    {
        internal sealed class State
        {
            internal Grid Open;
            internal Grid Closed;
        }
        const string OpenEyeData = "M2,9 C6,3 14,3 18,9 C14,15 6,15 2,9 Z M4.6,4.6 L3.1,2.5 M10,2.4 L10,0.3 M15.4,4.6 L16.9,2.5";
        // 闭眼整体以画布中心（y=9）为准，别让弧线和睫毛把重心拖低。
        const string ClosedEyeData = "M2.4,5.6 C6,10.4 14,10.4 17.6,5.6 M4.8,8.9 L3.3,11 M10,10.3 L10,12.4 M15.2,8.9 L16.7,11";
        internal static State Build(Button toggle)
        {
            Grid open = new Grid();
            open.Tag = "RevealOpen";
            Canvas openCanvas = new Canvas();
            openCanvas.Width = 20; openCanvas.Height = 18;
            openCanvas.HorizontalAlignment = HorizontalAlignment.Center;
            openCanvas.VerticalAlignment = VerticalAlignment.Center;
            System.Windows.Shapes.Path openStroke = EyeStroke(OpenEyeData, toggle);
            openCanvas.Children.Add(openStroke);
            System.Windows.Shapes.Ellipse pupil = new System.Windows.Shapes.Ellipse();
            pupil.Width = 5.2;
            pupil.Height = 5.2;
            Canvas.SetLeft(pupil, 7.4);
            Canvas.SetTop(pupil, 6.4);
            pupil.SetBinding(System.Windows.Shapes.Shape.FillProperty,
                new System.Windows.Data.Binding("Foreground") { Source = toggle });
            openCanvas.Children.Add(pupil);
            CenterInk(openCanvas, openStroke);
            open.Children.Add(openCanvas);

            Grid closed = new Grid();
            closed.Tag = "RevealClosed";
            closed.Visibility = Visibility.Collapsed;
            Canvas closedCanvas = new Canvas();
            closedCanvas.Width = 20; closedCanvas.Height = 18;
            closedCanvas.HorizontalAlignment = HorizontalAlignment.Center;
            closedCanvas.VerticalAlignment = VerticalAlignment.Center;
            System.Windows.Shapes.Path closedStroke = EyeStroke(ClosedEyeData, toggle);
            closedCanvas.Children.Add(closedStroke);
            CenterInk(closedCanvas, closedStroke);
            closed.Children.Add(closedCanvas);

            Grid icon = new Grid();
            icon.Width = 20; icon.Height = 18;
            icon.UseLayoutRounding = false;
            icon.SnapsToDevicePixels = false;
            icon.Children.Add(open);
            icon.Children.Add(closed);
            toggle.Content = icon;
            State state = new State();
            state.Open = open;
            state.Closed = closed;
            return state;
        }
        static void CenterInk(Canvas canvas, System.Windows.Shapes.Path stroke)
        {
            Pen pen = new Pen(Brushes.Black, stroke.StrokeThickness);
            pen.StartLineCap = pen.EndLineCap = PenLineCap.Round;
            pen.LineJoin = PenLineJoin.Round;
            Rect bounds = stroke.Data.GetRenderBounds(pen);
            canvas.RenderTransform = new TranslateTransform(
                canvas.Width / 2 - (bounds.Left + bounds.Right) / 2,
                canvas.Height / 2 - (bounds.Top + bounds.Bottom) / 2);
        }
        static System.Windows.Shapes.Path EyeStroke(string data, Button toggle)
        {
            System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
            path.Data = Geometry.Parse(data);
            path.StrokeThickness = 1.3;
            path.StrokeStartLineCap = PenLineCap.Round;
            path.StrokeEndLineCap = PenLineCap.Round;
            path.StrokeLineJoin = PenLineJoin.Round;
            path.SetBinding(System.Windows.Shapes.Shape.StrokeProperty,
                new System.Windows.Data.Binding("Foreground") { Source = toggle });
            return path;
        }
        internal static void SetRevealed(State state, bool revealed)
        {
            if (state == null) return;
            if (state.Open != null) state.Open.Visibility = revealed ? Visibility.Collapsed : Visibility.Visible;
            if (state.Closed != null) state.Closed.Visibility = revealed ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    static class PasswordInputFocus
    {
        internal static void AtEnd(Control input, Func<bool> stillActive)
        {
            input.Focus();
            MoveToEnd(input);
            input.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(delegate
            {
                if (stillActive() && input.IsKeyboardFocusWithin) MoveToEnd(input);
            }));
        }

        static void MoveToEnd(Control input)
        {
            TextBox text = input as TextBox;
            if (text != null) text.Select(text.Text.Length, 0);
            else System.Windows.Documents.EditingCommands.MoveToDocumentEnd.Execute(null, input);
        }
    }

    /// <summary>默认掩码、可切换显示的密钥输入框（PasswordBox 与 TextBox 叠放切换）。</summary>
    sealed class SecretField
    {
        readonly PasswordBox masked = new PasswordBox();
        RevealIcon.State icon;
        readonly TextBox plain = new TextBox();
        readonly Button toggle = new Button();
        bool revealed;
        bool synchronizing;
        Panel host;
        int focusVersion;
        public event Action Changed;

        public event Action Committed;
        public SecretField(Style passwordStyle, Style textStyle, Style iconStyle)
        {
            masked.Style = passwordStyle;
            masked.VerticalAlignment = VerticalAlignment.Center;
            plain.Style = textStyle;
            plain.VerticalAlignment = VerticalAlignment.Center;
            plain.Visibility = Visibility.Collapsed;
            toggle.Style = iconStyle;
            icon = RevealIcon.Build(toggle);

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
            // 输入框一体化容器：密钥框与右侧内嵌图标共用一条边框，宽度与其他输入框一致。
            Border addon = new Border();
            Style addonStyle = host.TryFindResource("InputAddon") as Style;
            if (addonStyle != null) addon.Style = addonStyle; else addon.BorderThickness = new Thickness(1);
            addon.SetResourceReference(FrameworkElement.WidthProperty, "SettingsInputWidth");
            addon.HorizontalAlignment = HorizontalAlignment.Right;
            addon.VerticalAlignment = VerticalAlignment.Center;

            Grid inner = new Grid();
            inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid input = new Grid();
            masked.Width = plain.Width = double.NaN;
            input.Children.Add(masked); input.Children.Add(plain); inner.Children.Add(input);
            Grid.SetColumn(toggle, 1); inner.Children.Add(toggle);
            addon.Child = inner;
            host.Children.Add(addon);
            System.Windows.Automation.AutomationProperties.SetName(masked, host.Name == "emailAuthHost" ? "邮箱授权码" : "访问令牌");
            System.Windows.Automation.AutomationProperties.SetName(plain, host.Name == "emailAuthHost" ? "邮箱授权码" : "访问令牌");
            System.Windows.Automation.AutomationProperties.SetName(toggle, "显示或隐藏令牌");
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
                if (plain.Text != masked.Password) plain.Text = masked.Password;
                plain.Visibility = Visibility.Visible;
                masked.Visibility = Visibility.Collapsed;
                RevealIcon.SetRevealed(icon, true);
                PasswordInputFocus.AtEnd(plain, delegate { return revealed; });
            }
            else
            {
                if (masked.Password != plain.Text) masked.Password = plain.Text;
                masked.Visibility = Visibility.Visible;
                plain.Visibility = Visibility.Collapsed;
                RevealIcon.SetRevealed(icon, false);
                PasswordInputFocus.AtEnd(masked, delegate { return !revealed; });
            }
            }
            finally { synchronizing = false; }
        }
    }
}
