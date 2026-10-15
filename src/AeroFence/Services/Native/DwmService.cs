using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AeroFence.Services.Native;

/// <summary>
/// Service providing genuine Windows Acrylic and Frosted Glass Blur.
/// Uses SetWindowCompositionAttribute which is compatible with WPF transparent windows.
/// This matches the exact blur mechanism used by Stardock Fences on Windows 10/11.
/// </summary>
public class DwmService
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public WindowCompositionAttribute Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    private enum WindowCompositionAttribute
    {
        WCA_ACCENT_POLICY = 19
    }

    private enum AccentState
    {
        ACCENT_DISABLED = 0,
        ACCENT_ENABLE_GRADIENT = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        ACCENT_ENABLE_BLURBEHIND = 3,           // Aero glass blur
        ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,    // Windows 11 Acrylic blur
        ACCENT_ENABLE_HOSTBACKDROP = 5
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public int AccentFlags;
        public int GradientColor;               // AABBGGRR in hex
        public int AnimationId;
    }

    /// <summary>
    /// Applies authentic frosted glass / acrylic blur behind the window.
    /// </summary>
    public bool ApplyBackdrop(Window window)
    {
        return ApplyBackdrop(window, true);
    }

    /// <summary>
    /// Applies or disables authentic frosted glass / acrylic blur behind the window.
    /// </summary>
    public bool ApplyBackdrop(Window window, bool enableAcrylic)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return false;

        try
        {
            // 1. Force Immersive Dark Mode for DWM
            int darkMode = 1;
            DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

            // 2. Set rounded corner preference
            int cornerPref = 2; // DWMWCP_ROUND
            DwmSetWindowAttribute(handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));

            // 3. Apply Acrylic Blur Behind using SetWindowCompositionAttribute
            var policy = new AccentPolicy
            {
                AccentState = enableAcrylic ? AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND : AccentState.ACCENT_DISABLED,
                AccentFlags = enableAcrylic ? 2 : 0,
                GradientColor = enableAcrylic ? unchecked((int)0x99181412) : 0,
                AnimationId = 0
            };

            int size = Marshal.SizeOf(policy);
            IntPtr policyPtr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(policy, policyPtr, false);

            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                Data = policyPtr,
                SizeOfData = size
            };

            int result = SetWindowCompositionAttribute(handle, ref data);
            Marshal.FreeHGlobal(policyPtr);

            return result != 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DwmService] Error applying acrylic blur: {ex.Message}");
            return false;
        }
    }
}
