using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace AeroFence.Installer;

public partial class MainWindow : Window
{
    private string _targetDirectory;

    public MainWindow()
    {
        InitializeComponent();

        _targetDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Programs\AeroFence");

        PathTextBox.Text = _targetDirectory;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Destination Folder",
            InitialDirectory = _targetDirectory
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            _targetDirectory = dialog.FolderName;
            PathTextBox.Text = _targetDirectory;
        }
    }

    private async void InstallBtn_Click(object sender, RoutedEventArgs e)
    {
        InstallBtn.IsEnabled = false;
        CancelBtn.IsEnabled = false;
        PathTextBox.IsEnabled = false;

        try
        {
            StatusTextBlock.Text = "Closing running instances...";
            InstallProgressBar.Value = 15;
            await Task.Run(() =>
            {
                foreach (var proc in Process.GetProcessesByName("AeroFence"))
                {
                    try { proc.Kill(); proc.WaitForExit(2000); } catch { }
                }
            });

            StatusTextBlock.Text = "Extracting AeroFence files...";
            InstallProgressBar.Value = 40;

            await Task.Run(() =>
            {
                if (!Directory.Exists(_targetDirectory))
                {
                    Directory.CreateDirectory(_targetDirectory);
                }

                // Extract embedded zip payload
                var assembly = Assembly.GetExecutingAssembly();
                string resourceName = "AeroFence.Installer.Resources.AeroFence_Payload.zip";

                using Stream? stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    throw new FileNotFoundException("Embedded installation payload not found.");
                }

                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        // Directory entry
                        string dirPath = Path.Combine(_targetDirectory, entry.FullName);
                        Directory.CreateDirectory(dirPath);
                        continue;
                    }

                    string destFile = Path.Combine(_targetDirectory, entry.FullName);
                    string? dir = Path.GetDirectoryName(destFile);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    if (entry.FullName.Replace('/', '\\').EndsWith(@"Data\fences.json", StringComparison.OrdinalIgnoreCase) && File.Exists(destFile))
                    {
                        // Preserve existing fences configuration when updating
                        continue;
                    }

                    entry.ExtractToFile(destFile, overwrite: true);
                }

                // Copy installer as Uninstall.exe
                string? currentExe = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(currentExe) && File.Exists(currentExe))
                {
                    string uninstallerPath = Path.Combine(_targetDirectory, "Uninstall.exe");
                    try
                    {
                        File.Copy(currentExe, uninstallerPath, overwrite: true);
                    }
                    catch { }
                }
            });

            StatusTextBlock.Text = "Creating shortcuts & registry entries...";
            InstallProgressBar.Value = 75;

            string exePath = Path.Combine(_targetDirectory, "AeroFence.exe");
            string uninstallerPath = Path.Combine(_targetDirectory, "Uninstall.exe");

            bool createStartMenu = StartMenuCheckBox.IsChecked == true;
            bool createDesktop = DesktopCheckBox.IsChecked == true;
            bool startWithWindows = StartupCheckBox.IsChecked == true;

            await Task.Run(() =>
            {
                // 1. Start Menu Shortcut
                if (createStartMenu)
                {
                    string startMenuDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        @"Microsoft\Windows\Start Menu\Programs");
                    CreateShortcut(exePath, Path.Combine(startMenuDir, "AeroFence.lnk"), "AeroFence Desktop Organizer");
                }

                // 2. Desktop Shortcut
                if (createDesktop)
                {
                    string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    CreateShortcut(exePath, Path.Combine(desktopDir, "AeroFence.lnk"), "AeroFence Desktop Organizer");
                }

                // 3. Windows Startup Registry
                using (var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (startWithWindows)
                    {
                        runKey?.SetValue("AeroFence", $"\"{exePath}\" --background");
                    }
                    else
                    {
                        runKey?.DeleteValue("AeroFence", false);
                    }
                }

                // 4. Register Windows Uninstall Entry
                using (var uninstallKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\AeroFence"))
                {
                    uninstallKey.SetValue("DisplayName", "AeroFence");
                    uninstallKey.SetValue("DisplayVersion", "1.0.0");
                    uninstallKey.SetValue("Publisher", "AeroFence Team");
                    uninstallKey.SetValue("DisplayIcon", exePath);
                    uninstallKey.SetValue("InstallLocation", _targetDirectory);
                    uninstallKey.SetValue("UninstallString", $"\"{uninstallerPath}\" --uninstall");
                    uninstallKey.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    uninstallKey.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            });

            InstallProgressBar.Value = 100;
            StatusTextBlock.Text = "Installation complete! Launching AeroFence...";

            // Launch AeroFence immediately
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = _targetDirectory,
                    UseShellExecute = true
                });
            }
            catch { }

            await Task.Delay(1200);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Installation failed: {ex.Message}", "AeroFence Setup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            InstallBtn.IsEnabled = true;
            CancelBtn.IsEnabled = true;
            PathTextBox.IsEnabled = true;
            StatusTextBlock.Text = "Installation failed.";
        }
    }

    private static void CreateShortcut(string targetPath, string shortcutPath, string description)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;

            object? shell = Activator.CreateInstance(shellType);
            if (shell == null) return;

            object? shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                new object[] { shortcutPath });

            if (shortcut == null) return;

            shortcut.GetType().InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
            shortcut.GetType().InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(targetPath) ?? "" });
            shortcut.GetType().InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { description });
            shortcut.GetType().InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath + ",0" });
            shortcut.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }
        catch { }
    }
}
