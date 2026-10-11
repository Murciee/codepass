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
      // 只在交互发生时创建短动画，不运行常驻刷新计时器。
      // 每次读取窗口所在显示器，跨屏或切换刷新率后下一次动画自动适配。
      static class DisplayMotion
      {
          [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
          struct MonitorInfo
          {
              public int Size;
              public int Left, Top, Right, Bottom;
              public int WorkLeft, WorkTop, WorkRight, WorkBottom;
              public int Flags;
              [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
              public string Device;
          }

          [DllImport("user32.dll")]
          static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
          [DllImport("user32.dll", CharSet = CharSet.Unicode)]
          static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
          [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
          static extern IntPtr CreateDC(string driver, string device, string output, IntPtr initData);
          [DllImport("gdi32.dll")]
          static extern int GetDeviceCaps(IntPtr dc, int index);
          [DllImport("gdi32.dll")]
          static extern bool DeleteDC(IntPtr dc);

          internal static int RefreshRate(Window window)
          {
              IntPtr dc = IntPtr.Zero;
              try
              {
                  IntPtr monitor = MonitorFromWindow(new WindowInteropHelper(window).Handle, 2);
                  MonitorInfo info = new MonitorInfo();
                  info.Size = Marshal.SizeOf(typeof(MonitorInfo));
                  if (GetMonitorInfo(monitor, ref info))
                  {
                      dc = CreateDC("DISPLAY", info.Device, null, IntPtr.Zero);
                      int rate = dc == IntPtr.Zero ? 0 : GetDeviceCaps(dc, 116); // VREFRESH
                      if (rate >= 24 && rate <= 1000) return rate;
                  }
              }
              catch { }
              finally { if (dc != IntPtr.Zero) DeleteDC(dc); }
              return 60; // 远程桌面/驱动未提供有效刷新率时回退。
          }

          internal static void Reveal(Window window, FrameworkElement content)
          {
              content.BeginAnimation(UIElement.OpacityProperty, null);
              content.Opacity = 1;
              if (!SystemParameters.ClientAreaAnimation) return;
              DoubleAnimation fade = new DoubleAnimation(0.88, 1, TimeSpan.FromMilliseconds(160));
              fade.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
              fade.FillBehavior = FillBehavior.Stop;
              Timeline.SetDesiredFrameRate(fade, RefreshRate(window));
              content.BeginAnimation(UIElement.OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
          }
      }
}
