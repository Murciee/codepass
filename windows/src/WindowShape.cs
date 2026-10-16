using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Codepass
{
    static class WindowShape
    {
        const int DwmWindowCornerPreference = 33;
        const int DwmBorderColor = 34;
        const int DwmUseImmersiveDarkMode = 20;
        const int DwmSystemBackdropType = 38;
        const int DwmCornerRound = 2;
        const int DwmBackdropMica = 2;                        // DWMSBT_MAINWINDOW
        const int DwmColorNone = unchecked((int)0xFFFFFFFE);  // DWMWA_COLOR_NONE
        const int S_OK = 0;

        static int micaState; // 0 未判定 / 1 已启用 / 2 本机不支持（决定过就不再试）
        internal static bool MicaEnabled { get { return micaState == 1; } }

        // 云母需要 Win11 22H2 起（21H2 与 Win10 调用会失败）；高对比度或系统关了
        // 透明效果时也保持不透明。启用后窗口和根元素不再画不透明底色，让系统把
        // 桌面壁纸的模糊色垫在下面——这不是分层窗口（AllowsTransparency 仍为
        // false），渲染照走硬件合成，不影响滚动与隐藏页释放。
        internal static void ApplyMica(Window window, Panel root)
        {
            if (window == null || root == null || micaState == 2) return;
            if (SystemParameters.HighContrast) { micaState = 2; return; }
            if (!SystemTransparencyEnabled()) { micaState = 2; return; }
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return; // 句柄未建好，下次再试
            try
            {
                int backdrop = DwmBackdropMica;
                if (DwmSetWindowAttribute(hwnd, DwmSystemBackdropType, ref backdrop, sizeof(int)) != S_OK)
                { micaState = 2; return; }
                HwndSource source = HwndSource.FromHwnd(hwnd);
                if (source != null && source.CompositionTarget != null)
                    source.CompositionTarget.BackgroundColor = Colors.Transparent;
                window.Background = Brushes.Transparent;
                root.Background = Brushes.Transparent;
                // 外壳和左侧导航各有一层不透明底色，只清窗口与根元素的话云母只剩外圈
                // 可见；外壳自画的 1px 描边也一起去掉，贴近系统无边框观感。
                Border shell = Find<Border>(root, "shell");
                if (shell != null)
                {
                    shell.Background = Brushes.Transparent;
                    shell.BorderBrush = Brushes.Transparent;
                }
                Border navRail = Find<Border>(root, "navRail");
                if (navRail != null) navRail.Background = Brushes.Transparent;
                micaState = 1;
                Log.Write("mica backdrop on");
            }
            catch { micaState = 2; }
        }

        // Win11 的无边框观感：去掉系统画的 1px 边框线，并强制浅色窗框/云母
        // 色调（本程序没有深色模式，系统处于深色时也保持浅色）。两项都在旧
        // 系统上静默失败。
        internal static void ApplyModernFrame(Window window)
        {
            if (window == null) return;
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            try
            {
                int border = DwmColorNone;
                DwmSetWindowAttribute(hwnd, DwmBorderColor, ref border, sizeof(int));
                int light = 0;
                DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkMode, ref light, sizeof(int));
            }
            catch { }
        }

        static bool SystemTransparencyEnabled()
        {
            try
            {
                object value = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "EnableTransparency", 1);
                return !(value is int) || (int)value != 0;
            }
            catch { return true; }
        }

        static T Find<T>(FrameworkElement root, string name) where T : class
        {
            object found = root.FindName(name);
            if (found == null) found = LogicalTreeHelper.FindLogicalNode(root, name);
            return found as T;
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("gdi32.dll")]
        static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);

        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr objectHandle);

        [DllImport("user32.dll")]
        static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, bool redraw);

        internal static void Apply(Window window)
        {
            if (window == null) return;
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            if (window.WindowState == WindowState.Maximized)
            {
                SetWindowRgn(hwnd, IntPtr.Zero, true);
                return;
            }

            int preference = DwmCornerRound;
            try
            {
                if (DwmSetWindowAttribute(hwnd, DwmWindowCornerPreference, ref preference, sizeof(int)) == S_OK)
                {
                    SetWindowRgn(hwnd, IntPtr.Zero, true);
                    return;
                }
            }
            catch { }

            double scale = 1;
            try
            {
                PresentationSource source = PresentationSource.FromVisual(window);
                if (source != null && source.CompositionTarget != null)
                    scale = source.CompositionTarget.TransformToDevice.M11;
            }
            catch { }
            if (scale <= 0) scale = 1;
            int width = (int)Math.Round(window.ActualWidth * scale);
            int height = (int)Math.Round(window.ActualHeight * scale);
            int radius = Math.Max(1, (int)Math.Round(8 * scale));
            if (width <= 0 || height <= 0) return;

            IntPtr region = CreateRoundRectRgn(0, 0, width + 1, height + 1, radius * 2, radius * 2);
            if (region == IntPtr.Zero) return;
            if (SetWindowRgn(hwnd, region, true) == 0) DeleteObject(region);
        }
    }
}
