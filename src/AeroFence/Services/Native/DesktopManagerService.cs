using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AeroFence.Services.Native;

/// <summary>
/// Manages anchoring the AeroFence window directly to the Windows Desktop layer.
/// Operates as a permanent desktop-anchored container:
/// 1. Sits at HWND_BOTTOM (behind normal active application windows).
/// 2. Survives Win + D (Show Desktop) and Win + M without minimizing.
/// 3. Retains 100% native WPF transparency and DWM Acrylic blur without disappearing.
/// </summary>
public class DesktopManagerService
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private const int WM_WINDOWPOSCHANGING = 0x0046;
    private const int WM_SYSCOMMAND = 0x0112;
    private const int SC_MINIMIZE = 0xF020;
    private const int WM_SHOWWINDOW = 0x0018;
    public const int WM_NCLBUTTONDOWN = 0x00A1;
    public const int HTCAPTION = 2;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_HIDEWINDOW = 0x0080;

    private static readonly IntPtr HWND_BOTTOM = (IntPtr)1;
    private static readonly IntPtr HWND_NOTOPMOST = (IntPtr)(-2);

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPOS
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public uint flags;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    private Window? _targetWindow;
    private HwndSource? _hwndSource;
    private IntPtr _hwnd = IntPtr.Zero;
    private bool _isClosing = false;

    public bool IsAttached { get; private set; } = false;

    /// <summary>
    /// Attaches the WPF window to the desktop layer (HWND_BOTTOM) with Win+D persistence.
    /// </summary>
    public bool AttachToDesktop(Window window)
    {
        _targetWindow = window;
        _hwnd = new WindowInteropHelper(window).Handle;
        if (_hwnd == IntPtr.Zero) return false;

        try
        {
            _isClosing = false;

            // 1. Ensure window has WS_EX_TOOLWINDOW style (hidden from Alt-Tab & discreet desktop appearance)
            IntPtr currentExStyle = GetWindowLongPtr(_hwnd, GWL_EXSTYLE);
            IntPtr newExStyle = new IntPtr(currentExStyle.ToInt64() | WS_EX_TOOLWINDOW);
            SetWindowLongPtr(_hwnd, GWL_EXSTYLE, newExStyle);

            // 2. Position window directly at HWND_BOTTOM
            SetWindowPos(_hwnd, HWND_BOTTOM, 0, 0, 0, 0,
                         SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);

            // 3. Attach WndProc hook to enforce Z-Order and Win+D resistance
            if (_hwndSource == null)
            {
                _hwndSource = HwndSource.FromHwnd(_hwnd);
                _hwndSource?.AddHook(WndProc);
            }

            IsAttached = true;
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DesktopManager] Attach failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Detaches the window from the desktop layer, restoring standard floating behavior.
    /// </summary>
    public bool DetachFromDesktop(Window? window = null)
    {
        _isClosing = true;
        IsAttached = false;

        if (_hwndSource != null)
        {
            try
            {
                _hwndSource.RemoveHook(WndProc);
            }
            catch { }
            _hwndSource = null;
        }

        if (_hwnd != IntPtr.Zero)
        {
            try
            {
                SetWindowPos(_hwnd, HWND_NOTOPMOST, 0, 0, 0, 0,
                             SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED);
            }
            catch { }
        }

        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (!IsAttached || _isClosing) return IntPtr.Zero;

        switch (msg)
        {
            // Win + D / Win + M resistance: prevent minimizing when Show Desktop is triggered
            case WM_SYSCOMMAND:
                if ((wParam.ToInt32() & 0xFFF0) == SC_MINIMIZE)
                {
                    handled = true;
                    return IntPtr.Zero;
                }
                break;

            // Z-Order pinning: lock to HWND_BOTTOM and strip SWP_HIDEWINDOW
            case WM_WINDOWPOSCHANGING:
                if (lParam != IntPtr.Zero)
                {
                    var wp = Marshal.PtrToStructure<WINDOWPOS>(lParam);

                    // Prevent external commands from hiding the desktop fence
                    if ((wp.flags & SWP_HIDEWINDOW) != 0)
                    {
                        wp.flags &= ~SWP_HIDEWINDOW;
                    }

                    // Enforce HWND_BOTTOM whenever Z-order is evaluated (except during pure dragging)
                    if ((wp.flags & SWP_NOZORDER) == 0)
                    {
                        wp.hwndInsertAfter = HWND_BOTTOM;
                    }

                    Marshal.StructureToPtr(wp, lParam, true);
                }
                break;

            // Safety net: if external event triggers window hide, restore visibility seamlessly
            case WM_SHOWWINDOW:
                if (wParam == IntPtr.Zero && !_isClosing)
                {
                    _targetWindow?.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (IsAttached && !_isClosing && _targetWindow != null && !_targetWindow.IsVisible)
                        {
                            _targetWindow.Show();
                            SetWindowPos(_hwnd, HWND_BOTTOM, 0, 0, 0, 0,
                                         SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                break;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Native window drag move using Win32 WM_NCLBUTTONDOWN.
    /// Operates smoothly without fighting Z-order constraints.
    /// </summary>
    public static void StartWindowDrag(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        ReleaseCapture();
        SendMessage(hwnd, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
    }
}
