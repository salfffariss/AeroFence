using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AeroFence.Services.Native;

/// <summary>
/// Provides native, flicker-free edge and corner resizing for borderless WPF windows.
/// Solves the standard borderless limitation by hooking WM_NCHITTEST.
/// </summary>
public class WindowResizer
{
    private readonly Window _window;
    private readonly int _borderMargin;

    public WindowResizer(Window window, int borderMargin = 8)
    {
        _window = window;
        _borderMargin = borderMargin;

        _window.SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(_window).Handle;
        var source = HwndSource.FromHwnd(handle);
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case NativeMethods.WM_GETMINMAXINFO:
                try
                {
                    var mmi = (NativeMethods.MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(NativeMethods.MINMAXINFO))!;
                    var source = (HwndSource)HwndSource.FromHwnd(hwnd);
                    double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                    double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

                    double minW = _window.MinWidth > 0 ? _window.MinWidth : 180;
                    double minH = _window.ActualHeight <= 45 ? 32 : (_window.MinHeight > 0 ? _window.MinHeight : 120);

                    mmi.ptMinTrackSize.X = (int)(minW * dpiX);
                    mmi.ptMinTrackSize.Y = (int)(minH * dpiY);
                    Marshal.StructureToPtr(mmi, lParam, true);
                    handled = true;
                }
                catch { }
                break;

            case NativeMethods.WM_NCHITTEST:
                // Extract screen coordinates (handles multi-monitor signed short coordinates)
                int x = (short)(lParam.ToInt32() & 0xFFFF);
                int y = (short)((lParam.ToInt32() >> 16) & 0xFFFF);

                // Convert screen point to WPF device-independent coordinates (respecting 125% DPI scale)
                Point pt;
                try
                {
                    pt = _window.PointFromScreen(new Point(x, y));
                }
                catch
                {
                    return IntPtr.Zero;
                }

                double width = _window.ActualWidth;
                double height = _window.ActualHeight;

                bool onLeft = pt.X <= _borderMargin;
                bool onRight = pt.X >= width - _borderMargin;
                bool onTop = pt.Y <= _borderMargin;
                bool onBottom = pt.Y >= height - _borderMargin;

                // When rolled up (32px), only allow horizontal resizing to preserve compact height
                if (_window.ActualHeight <= 45)
                {
                    if (onLeft) { handled = true; return (IntPtr)NativeMethods.HTLEFT; }
                    if (onRight) { handled = true; return (IntPtr)NativeMethods.HTRIGHT; }
                    return IntPtr.Zero;
                }

                // Priority 1: Corners
                if (onTop && onLeft)
                {
                    handled = true;
                    return (IntPtr)NativeMethods.HTTOPLEFT;
                }
                if (onTop && onRight)
                {
                    handled = true;
                    return (IntPtr)NativeMethods.HTTOPRIGHT;
                }
                if (onBottom && onLeft)
                {
                    handled = true;
                    return (IntPtr)NativeMethods.HTBOTTOMLEFT;
                }
                if (onBottom && onRight)
                {
                    handled = true;
                    return (IntPtr)NativeMethods.HTBOTTOMRIGHT;
                }

                // Priority 2: Straight edges
                if (onLeft)
                {
                    handled = true;
                    return (IntPtr)NativeMethods.HTLEFT;
                }
                if (onRight)
                {
                    handled = true;
                    return (IntPtr)NativeMethods.HTRIGHT;
                }
                if (onTop)
                {
                    handled = true;
                    return (IntPtr)NativeMethods.HTTOP;
                }
                if (onBottom)
                {
                    handled = true;
                    return (IntPtr)NativeMethods.HTBOTTOM;
                }

                break;
        }

        return IntPtr.Zero;
    }
}
