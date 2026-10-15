using System;
using System.IO;
using Microsoft.Win32;

namespace AeroFence.Services.Native;

/// <summary>
/// Manages Windows User Startup via HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
/// Operates with standard user permissions (no admin UAC elevation required).
/// </summary>
public static class StartupService
{
    private const string RUN_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string APP_NAME = "AeroFence";

    public static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RUN_KEY, false);
            return key?.GetValue(APP_NAME) != null;
        }
        catch
        {
            return false;
        }
    }

    public static bool SetStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RUN_KEY, true);
            if (key == null) return false;

            if (enable)
            {
                string exePath = Environment.ProcessPath ?? 
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AeroFence.exe");

                // Launch quietly into desktop mode without opening the Settings configuration window
                key.SetValue(APP_NAME, $"\"{exePath}\" --background");
            }
            else
            {
                key.DeleteValue(APP_NAME, false);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
