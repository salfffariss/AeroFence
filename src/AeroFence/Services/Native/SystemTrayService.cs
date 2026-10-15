using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace AeroFence.Services.Native;

/// <summary>
/// Robust System Tray Service using native Windows Forms NotifyIcon.
/// Displays genuine high-DPI icon with tooltip and dark glass WPF ContextMenu.
/// </summary>
public class SystemTrayService : IDisposable
{
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private ContextMenu? _trayMenu;

    public event Action? OpenSettingsRequested;
    public event Action? CreateFenceRequested;
    public event Action? CreateFolderPortalRequested;
    public event Action? ToggleVisibilityRequested;
    public event Action? ExitRequested;

    public void Initialize()
    {
        if (_notifyIcon != null) return;

        _notifyIcon = new System.Windows.Forms.NotifyIcon();

        // 1. Load genuine high-DPI application icon
        try
        {
            string? exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
            {
                _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exe);
            }
        }
        catch { }

        if (_notifyIcon.Icon == null)
        {
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
                if (File.Exists(iconPath))
                {
                    _notifyIcon.Icon = new System.Drawing.Icon(iconPath);
                }
            }
            catch { }
        }

        _notifyIcon.Text = "AeroFence - Desktop Organization";
        _notifyIcon.Visible = true;

        // Mouse interactions
        _notifyIcon.MouseDoubleClick += (s, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                OpenSettingsRequested?.Invoke();
            }
        };

        _notifyIcon.MouseUp += (s, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                ShowContextMenu();
            }
            else if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                // Single left-click also surfaces settings or toggle
                // Optional: keep clean
            }
        };

        BuildContextMenu();
    }

    private void BuildContextMenu()
    {
        _trayMenu = new ContextMenu();

        // 1. Create AeroFence
        var itemCreate = new MenuItem
        {
            Header = "Create New Fence",
            Icon = new TextBlock { Text = "➕", FontSize = 12 }
        };
        itemCreate.Click += (s, e) => CreateFenceRequested?.Invoke();
        _trayMenu.Items.Add(itemCreate);

        // 2. Create Folder Portal
        var itemPortal = new MenuItem
        {
            Header = "Create Folder Portal...",
            Icon = new TextBlock { Text = "📁", FontSize = 12 }
        };
        itemPortal.Click += (s, e) => CreateFolderPortalRequested?.Invoke();
        _trayMenu.Items.Add(itemPortal);

        // 3. Toggle Show/Hide
        var itemToggle = new MenuItem
        {
            Header = "Show / Hide All Fences",
            Icon = new TextBlock { Text = "👁️", FontSize = 12 }
        };
        itemToggle.Click += (s, e) => ToggleVisibilityRequested?.Invoke();
        _trayMenu.Items.Add(itemToggle);

        _trayMenu.Items.Add(new Separator());

        // 4. Start with Windows
        var itemStartup = new MenuItem
        {
            Header = "Start with Windows",
            IsCheckable = true,
            IsChecked = StartupService.IsStartupEnabled()
        };
        itemStartup.Click += (s, e) =>
        {
            bool newState = !StartupService.IsStartupEnabled();
            StartupService.SetStartup(newState);
            itemStartup.IsChecked = newState;
        };
        _trayMenu.Items.Add(itemStartup);

        // 5. Settings
        var itemSettings = new MenuItem
        {
            Header = "AeroFence Settings...",
            FontWeight = FontWeights.SemiBold,
            Icon = new TextBlock { Text = "⚙️", FontSize = 12 }
        };
        itemSettings.Click += (s, e) => OpenSettingsRequested?.Invoke();
        _trayMenu.Items.Add(itemSettings);

        _trayMenu.Items.Add(new Separator());

        // 6. Exit
        var itemExit = new MenuItem
        {
            Header = "Exit AeroFence",
            Icon = new TextBlock { Text = "✕", FontSize = 12 }
        };
        itemExit.Click += (s, e) => ExitRequested?.Invoke();
        _trayMenu.Items.Add(itemExit);
    }

    private void ShowContextMenu()
    {
        if (_trayMenu == null) return;

        // Update Startup check state
        foreach (var it in _trayMenu.Items)
        {
            if (it is MenuItem mi && mi.Header?.ToString() == "Start with Windows")
            {
                mi.IsChecked = StartupService.IsStartupEnabled();
                break;
            }
        }

        _trayMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        _trayMenu.IsOpen = true;
    }

    public void Dispose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}
