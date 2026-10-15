using System;
using System.Runtime.InteropServices;

namespace AeroFence.Services.Native;

/// <summary>
/// Win32 API constants and signatures for window chrome, hit-testing, and composition.
/// Directly inspired by Stardock Fences window management architecture.
/// </summary>
public static class NativeMethods
{
    // Window Messages
    public const int WM_NCHITTEST = 0x0084;
    public const int WM_GETMINMAXINFO = 0x0024;
    public const int WM_MOUSEMOVE = 0x0200;
    public const int WM_MOUSELEAVE = 0x02A3;

    // Hit-Test Codes (WM_NCHITTEST return values)
    public const int HTCLIENT = 1;
    public const int HTCAPTION = 2;
    public const int HTLEFT = 10;
    public const int HTRIGHT = 11;
    public const int HTTOP = 12;
    public const int HTTOPLEFT = 13;
    public const int HTTOPRIGHT = 14;
    public const int HTBOTTOM = 15;
    public const int HTBOTTOMLEFT = 16;
    public const int HTBOTTOMRIGHT = 17;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);
    public const uint GA_ROOT = 2;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT Point);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", ExactSpelling = true)]
    public static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

    // Shell Change Notification Events
    public const uint SHCNE_DELETE = 0x00000004;
    public const uint SHCNE_CREATE = 0x00000002;
    public const uint SHCNE_UPDATEDIR = 0x00001000;
    public const uint SHCNE_ASSOCCHANGED = 0x08000000;
    public const uint SHCNF_PATHW = 0x0005;
    public const uint SHCNF_FLUSH = 0x1000;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern void SHChangeNotify(uint wEventId, uint uFlags, [MarshalAs(UnmanagedType.LPWStr)] string? dwItem1, [MarshalAs(UnmanagedType.LPWStr)] string? dwItem2);

    public static void BroadcastOpenSettingsMessage()
    {
        uint msg = RegisterWindowMessage("AeroFence_OpenSettings");
        if (msg != 0)
        {
            PostMessage(HWND_BROADCAST, msg, IntPtr.Zero, IntPtr.Zero);
        }
    }
}
