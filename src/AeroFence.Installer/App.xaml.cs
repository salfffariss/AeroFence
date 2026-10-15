using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace AeroFence.Installer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string currentExe = Environment.ProcessPath ?? "";
        bool isUninstall = e.Args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase))
                           || currentExe.EndsWith("Uninstall.exe", StringComparison.OrdinalIgnoreCase);

        if (isUninstall)
        {
            PerformUninstall();
            Shutdown();
            return;
        }
    }

    private void PerformUninstall()
    {
        var result = MessageBox.Show(
            "Are you sure you want to completely uninstall AeroFence from this computer?",
            "Uninstall AeroFence",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            // 1. Terminate running processes
            foreach (var proc in Process.GetProcessesByName("AeroFence"))
            {
                try { proc.Kill(); proc.WaitForExit(2000); } catch { }
            }

            // 2. Remove Startup Registry
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                key?.DeleteValue("AeroFence", false);
            }

            // 3. Remove Windows Uninstall Entry
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true))
            {
                key?.DeleteSubKeyTree("AeroFence", false);
            }

            // 4. Remove Start Menu Shortcut
            string startMenuDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Start Menu\Programs");
            string startMenuLnk = Path.Combine(startMenuDir, "AeroFence.lnk");
            if (File.Exists(startMenuLnk)) File.Delete(startMenuLnk);

            // 5. Remove Desktop Shortcut
            string desktopLnk = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "AeroFence.lnk");
            if (File.Exists(desktopLnk)) File.Delete(desktopLnk);

            // 6. Delete install folder via detached cmd process
            string installDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Programs\AeroFence");

            if (Directory.Exists(installDir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c timeout /t 2 & rmdir /s /q \"{installDir}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }

            MessageBox.Show(
                "AeroFence has been successfully uninstalled from your computer.",
                "Uninstall Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Uninstall error: {ex.Message}", "Uninstall Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
