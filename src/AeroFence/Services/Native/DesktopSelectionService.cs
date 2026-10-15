using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using AeroFence.Views;

namespace AeroFence.Services.Native;

/// <summary>
/// Ultra-lightweight, zero-stutter desktop selection service.
/// Lets Windows Explorer natively draw its built-in blue selection rectangle during drag.
/// Upon mouse release, surfaces the Fences 5 Action Pill ("➕ Create AeroFence here")
/// centered right where the cursor was released.
/// </summary>
public class DesktopSelectionService : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_RBUTTONUP = 0x0205;

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public NativeMethods.POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private static DesktopSelectionService? _instance;
    private readonly LowLevelMouseProc _hookProc;
    private IntPtr _hookId = IntPtr.Zero;
    private DesktopOverlayWindow? _overlayWindow;

    private bool _isMouseDownOnDesktop = false;
    private bool _isDragging = false;
    private NativeMethods.POINT _startPt;

    public static bool IsItemDragActive { get; set; } = false;

    public static void CancelCurrentSelectionDrag()
    {
        if (_instance != null)
        {
            _instance._isMouseDownOnDesktop = false;
            _instance._isDragging = false;
        }
    }

    public event Action<Rect, string?>? FenceCreateRequested;

    public DesktopSelectionService()
    {
        _instance = this;
        _hookProc = HookCallback;
    }

    public void Start()
    {
        if (_hookId != IntPtr.Zero) return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            _overlayWindow = new DesktopOverlayWindow();
            _overlayWindow.FenceCreateRequested += (rect, folder) =>
            {
                FenceCreateRequested?.Invoke(rect, folder);
            };
        });

        IntPtr modHandle = GetModuleHandle(null);
        _hookId = SetWindowsHookEx(WH_MOUSE_LL, _hookProc, modHandle, 0);
    }

    public void Stop()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }

        if (_overlayWindow != null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _overlayWindow.Close();
                _overlayWindow = null;
            });
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

            switch (msg)
            {
                case WM_LBUTTONDOWN:
                case WM_RBUTTONDOWN:
                    if (!IsItemDragActive && IsPointOnDesktop(hookStruct.pt))
                    {
                        _isMouseDownOnDesktop = true;
                        _isDragging = false;
                        _startPt = hookStruct.pt;
                    }
                    else
                    {
                        _isMouseDownOnDesktop = false;
                        _isDragging = false;
                    }
                    break;

                case WM_MOUSEMOVE:
                    if (!IsItemDragActive && _isMouseDownOnDesktop)
                    {
                        int dx = hookStruct.pt.X - _startPt.X;
                        int dy = hookStruct.pt.Y - _startPt.Y;

                        if (dx * dx + dy * dy >= 25 * 25)
                        {
                            _isDragging = true;
                            // Windows Explorer natively renders the blue marquee selection box!
                            // AeroFence does not render any overlay during drag, completely eliminating double boxes.
                        }
                    }
                    break;

                case WM_LBUTTONUP:
                case WM_RBUTTONUP:
                    bool wasDragging = _isDragging;
                    bool wasDown = _isMouseDownOnDesktop;
                    NativeMethods.POINT start = _startPt;
                    NativeMethods.POINT end = hookStruct.pt;

                    _isMouseDownOnDesktop = false;
                    _isDragging = false;

                    // Only trigger if:
                    // 1. Not during an item drag & drop operation
                    // 2. Both start and release points are genuinely on the empty desktop area (outside all fences)
                    // 3. Distance is large enough to be a deliberate selection rectangle
                    if (!IsItemDragActive && wasDown && wasDragging && IsPointOnDesktop(end))
                    {
                        int sx = Math.Min(start.X, end.X);
                        int sy = Math.Min(start.Y, end.Y);
                        int sw = Math.Abs(end.X - start.X);
                        int sh = Math.Abs(end.Y - start.Y);

                        if (sw >= 60 && sh >= 40)
                        {
                            _overlayWindow?.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                            {
                                if (_overlayWindow != null && !IsItemDragActive)
                                {
                                    _overlayWindow.ShowActionPill(new Rect(sx, sy, sw, sh), end);
                                }
                            }));
                        }
                    }
                    break;
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public static bool IsPointOnDesktop(NativeMethods.POINT pt)
    {
        // 1. Ensure cursor is not within the physical bounding rectangle of any active AeroFence window
        try
        {
            if (Application.Current != null)
            {
                foreach (Window win in Application.Current.Windows)
                {
                    if (win is DesktopOverlayWindow || !win.IsVisible) continue;
                    var handle = new WindowInteropHelper(win).Handle;
                    if (handle != IntPtr.Zero && NativeMethods.GetWindowRect(handle, out var rect))
                    {
                        if (pt.X >= rect.Left && pt.X <= rect.Right && pt.Y >= rect.Top && pt.Y <= rect.Bottom)
                        {
                            return false;
                        }
                    }
                }
            }
        }
        catch { }

        IntPtr hwnd = NativeMethods.WindowFromPoint(pt);
        if (hwnd == IntPtr.Zero) return false;

        // Ensure cursor is not over any existing AeroFence window handle or its children
        if (Application.Current != null)
        {
            foreach (Window win in Application.Current.Windows)
            {
                if (win is DesktopOverlayWindow) continue;
                var handle = new WindowInteropHelper(win).Handle;
                if (handle != IntPtr.Zero)
                {
                    if (handle == hwnd || NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT) == handle)
                    {
                        return false;
                    }
                }
            }
        }

        IntPtr rootHwnd = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
        var rootClassName = new StringBuilder(256);
        NativeMethods.GetClassName(rootHwnd, rootClassName, 256);
        string rootCls = rootClassName.ToString();

        // Must be on the Windows Shell desktop (Progman or WorkerW).
        // This instantly excludes all application windows (Chrome, Notepad, Taskbar, Explorer windows, etc.)
        if (rootCls != "Progman" && rootCls != "WorkerW")
        {
            return false;
        }

        return true;
    }

    public void Dispose()
    {
        Stop();
    }
}
