using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using AeroFence.Services.Native;

namespace AeroFence.Views;

/// <summary>
/// 100% genuine Fences 5 desktop action callout.
/// Appears directly at the cursor when mouse drag is released.
/// Features smooth proximity fade-in/fade-out, auto-dismiss when moving away,
/// and zero-stutter click-through dragging.
/// </summary>
public partial class DesktopOverlayWindow : Window
{
    private Rect _currentRect;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private DispatcherTimer? _proximityTimer;
    private double _currentOpacity = 1.0;
    private double _targetOpacity = 1.0;
    private Point _pillCenter;

    public event Action<Rect, string?>? FenceCreateRequested;

    public DesktopOverlayWindow()
    {
        InitializeComponent();

        SourceInitialized += (s, e) =>
        {
            SetClickThrough(true);
        };

        Loaded += (s, e) =>
        {
            RepositionToVirtualScreen();
        };

        _proximityTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _proximityTimer.Tick += ProximityTimer_Tick;
    }

    public Rect CurrentRect => _currentRect;

    private void SetClickThrough(bool enable)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        IntPtr currentExStyle = GetWindowLongPtr64(hwnd, GWL_EXSTYLE);
        long style = currentExStyle.ToInt64() | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        if (enable)
        {
            style |= WS_EX_TRANSPARENT;
        }
        else
        {
            style &= ~WS_EX_TRANSPARENT;
        }
        SetWindowLongPtr64(hwnd, GWL_EXSTYLE, new IntPtr(style));
    }

    public void RepositionToVirtualScreen()
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    /// <summary>
    /// Surfaces the Fences 5 Action Pill directly where the user finished dragging their box.
    /// Uses Windows Explorer's native blue marquee during drag, so zero duplicate boxes are drawn!
    /// </summary>
    public void ShowActionPill(Rect screenRect, NativeMethods.POINT mouseReleasePt)
    {
        _proximityTimer?.Stop();

        RepositionToVirtualScreen();
        SetClickThrough(false);

        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        double scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
        double scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

        double targetLeft = screenRect.Left / scaleX;
        double targetTop = screenRect.Top / scaleY;
        double targetWidth = screenRect.Width / scaleX;
        double targetHeight = screenRect.Height / scaleY;

        if (targetWidth < 50 || targetHeight < 35)
        {
            ResetAndHide();
            return;
        }

        _currentRect = new Rect(targetLeft, targetTop, targetWidth, targetHeight);

        // Position Action Pill directly centered at cursor release position
        double mouseDipX = mouseReleasePt.X / scaleX;
        double mouseDipY = mouseReleasePt.Y / scaleY;

        double pillW = 270;
        double pillH = 44;
        double pillX = (mouseDipX - Left) - pillW / 2;
        double pillY = (mouseDipY - Top) - pillH / 2;

        double maxX = Math.Max(10, ActualWidth - pillW - 10);
        double maxY = Math.Max(10, ActualHeight - pillH - 10);
        pillX = Math.Clamp(pillX, 10, maxX);
        pillY = Math.Clamp(pillY, 10, maxY);

        Canvas.SetLeft(ActionPill, pillX);
        Canvas.SetTop(ActionPill, pillY);

        _pillCenter = new Point(pillX + pillW / 2, pillY + pillH / 2);

        _currentOpacity = 1.0;
        _targetOpacity = 1.0;
        ActionPill.Opacity = 1.0;
        ActionPill.Visibility = Visibility.Visible;
        SelectionBox.Visibility = Visibility.Collapsed;

        Show();
        _proximityTimer?.Start();
    }

    /// <summary>
    /// Fences 5 signature feature: tracks distance from cursor to pill.
    /// Fades out smoothly when moving away, fades back in when returning close,
    /// and auto-cancels if the user moves far away.
    /// </summary>
    private void ProximityTimer_Tick(object? sender, EventArgs e)
    {
        if (ActionPill.Visibility != Visibility.Visible || !IsVisible)
        {
            _proximityTimer?.Stop();
            return;
        }

        NativeMethods.GetCursorPos(out var mousePt);
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        double scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
        double scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

        double mouseDipX = (mousePt.X / scaleX) - Left;
        double mouseDipY = (mousePt.Y / scaleY) - Top;

        double dx = mouseDipX - _pillCenter.X;
        double dy = mouseDipY - _pillCenter.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);

        // Proximity zones:
        // 0 to 85px: close/hovering -> Opacity 1.0
        // 85px to 250px: moving away -> smoothly fade down
        // > 250px: far away -> auto cancel & hide
        if (dist <= 85)
        {
            _targetOpacity = 1.0;
        }
        else if (dist <= 250)
        {
            _targetOpacity = Math.Clamp((250 - dist) / 165.0, 0.0, 1.0);
        }
        else
        {
            _targetOpacity = 0.0;
        }

        // Spring interpolation for fluid smooth fading
        _currentOpacity += (_targetOpacity - _currentOpacity) * 0.25;
        ActionPill.Opacity = _currentOpacity;

        // Auto-cancel and disappear when faded out
        if (_targetOpacity == 0.0 && _currentOpacity < 0.03)
        {
            _proximityTimer?.Stop();
            ResetAndHide();
        }
    }

    private void CreateFenceBtn_Click(object sender, RoutedEventArgs e)
    {
        Rect target = _currentRect;
        ResetAndHide();
        FenceCreateRequested?.Invoke(target, null);
    }

    private void CreatePortalBtn_Click(object sender, RoutedEventArgs e)
    {
        Rect target = _currentRect;
        ResetAndHide();

        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Folder for AeroFence Portal"
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            FenceCreateRequested?.Invoke(target, dialog.FolderName);
        }
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        ResetAndHide();
    }

    private void ActionPill_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Prevent click from propagating to canvas background
        e.Handled = true;
    }

    private void RootCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Clicking anywhere on the transparent desktop background dismisses the selection
        ResetAndHide();
    }

    private void RootCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        ResetAndHide();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ResetAndHide();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Clears selection, stops tracking timer, and hides overlay window cleanly.
    /// </summary>
    public void ResetAndHide()
    {
        _proximityTimer?.Stop();
        ActionPill.Visibility = Visibility.Collapsed;
        SelectionBox.Visibility = Visibility.Collapsed;
        SetClickThrough(true);
        Hide();
        MemoryOptimizerService.NotifyInteraction();
    }
}
