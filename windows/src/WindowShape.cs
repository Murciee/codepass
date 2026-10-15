using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Runtime.InteropServices;

namespace Codepass
{
    static class WindowShape
    {
        const int DwmWindowCornerPreference = 33;
        const int DwmCornerRound = 2;
        const int S_OK = 0;

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
