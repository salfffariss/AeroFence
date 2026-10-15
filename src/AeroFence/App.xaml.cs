using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using AeroFence.Models;
using AeroFence.Services.Native;
using AeroFence.Services.Storage;
using AeroFence.ViewModels;
using AeroFence.Views;

namespace AeroFence;

/// <summary>
/// Interaction logic for App.xaml.
/// Orchestrates multi-fence windows, desktop selection service, background startup,
/// and single-instance Settings Window communication.
/// </summary>
public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private static DesktopSelectionService? _desktopSelectionService;
    private static SystemTrayService? _systemTrayService;
    private static readonly IStorageService _storageService = new JsonStorageService();
    private static readonly List<MainWindow> _fenceWindows = new();

    public App()
    {
        DispatcherUnhandledException += (s, e) =>
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                File.AppendAllText(logPath, $"[{DateTime.Now}] Dispatcher Exception: {e.Exception}\n");
            }
            catch { }
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                File.AppendAllText(logPath, $"[{DateTime.Now}] Domain Exception: {e.ExceptionObject}\n");
            }
            catch { }
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                File.AppendAllText(logPath, $"[{DateTime.Now}] Task Exception: {e.Exception}\n");
            }
            catch { }
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        const string mutexName = "Global\\AeroFence_SingleInstance_Mutex_987654";
        _singleInstanceMutex = new Mutex(true, mutexName, out bool isFirstInstance);

        string startupLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");
        try { File.AppendAllText(startupLog, $"[{DateTime.Now}] OnStartup. isFirstInstance: {isFirstInstance}\n"); } catch { }

        bool isBackground = e.Args.Any(a => a.Equals("--background", StringComparison.OrdinalIgnoreCase));
        bool isSettingsOnly = e.Args.Any(a => a.Equals("--settings", StringComparison.OrdinalIgnoreCase));

        if (!isFirstInstance)
        {
            // Another instance is already running!
            // Signal running instance to open Settings Window, then exit quietly.
            try { File.AppendAllText(startupLog, $"[{DateTime.Now}] Not first instance. Broadcasting settings and shutting down.\n"); } catch { }
            NativeMethods.BroadcastOpenSettingsMessage();
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Load or initialize fences configuration
        var config = _storageService.LoadConfig();
        try { File.AppendAllText(startupLog, $"[{DateTime.Now}] Loaded config. Fences count: {config.Fences?.Count}\n"); } catch { }

        if (config.Fences == null || config.Fences.Count == 0)
        {
            var defaultFence = new FenceModel
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = "Workspace",
                X = 140,
                Y = 140,
                Width = 560,
                Height = 380,
                Tabs = new List<TabModel>
                {
                    new TabModel { Name = "Games", Items = new List<ItemModel>() },
                    new TabModel { Name = "Tools", Items = new List<ItemModel>() }
                }
            };
            config.Fences = new List<FenceModel> { defaultFence };
            _storageService.SaveConfig(config);
        }

        // Spawn all configured desktop fences
        foreach (var fence in config.Fences)
        {
            SpawnFenceWindow(fence);
        }

        // Start Desktop Right-Drag Selection Hook ("Create AeroFence here" / "Folder Portal")
        try
        {
            _desktopSelectionService = new DesktopSelectionService();
            _desktopSelectionService.FenceCreateRequested += (rect, folderPath) =>
            {
                if (string.IsNullOrEmpty(folderPath))
                {
                    CreateNewFenceAt(rect.X, rect.Y, rect.Width, rect.Height);
                }
                else
                {
                    CreateNewFolderPortalAt(rect.X, rect.Y, rect.Width, rect.Height, folderPath);
                }
            };
            _desktopSelectionService.Start();
        }
        catch (Exception ex)
        {
            try
            {
                File.AppendAllText("crash.log", $"[{DateTime.Now}] DesktopSelectionService error: {ex}\n");
            }
            catch { }
        }

        // Initialize System Tray Notification Area Icon
        try
        {
            _systemTrayService = new SystemTrayService();
            _systemTrayService.OpenSettingsRequested += OpenSettingsWindow;
            _systemTrayService.CreateFenceRequested += () =>
            {
                CreateNewFenceAt(200, 200, 540, 380);
            };
            _systemTrayService.CreateFolderPortalRequested += () =>
            {
                var dialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "Select Folder for AeroFence Portal"
                };
                if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
                {
                    CreateNewFolderPortalAt(200, 200, 540, 380, dialog.FolderName);
                }
            };
            _systemTrayService.ToggleVisibilityRequested += () =>
            {
                bool anyVisible = _fenceWindows.Any(w => w.IsVisible);
                foreach (var win in _fenceWindows)
                {
                    if (anyVisible) win.Hide();
                    else win.Show();
                }
            };
            _systemTrayService.ExitRequested += () =>
            {
                Current.Shutdown();
            };
            _systemTrayService.Initialize();
        }
        catch (Exception ex)
        {
            try { File.AppendAllText("crash.log", $"[{DateTime.Now}] SystemTray error: {ex}\n"); } catch { }
        }

        // If explicitly requested via command line
        if (isSettingsOnly)
        {
            OpenSettingsWindow();
        }

        // Reclaim startup initialization buffers once UI has settled
        var startupTrimTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        startupTrimTimer.Tick += (s, e) =>
        {
            startupTrimTimer.Stop();
            MemoryOptimizerService.TrimWorkingSet();
        };
        startupTrimTimer.Start();
    }

    private static MainWindow SpawnFenceWindow(FenceModel fence)
    {
        var vm = new MainViewModel(_storageService, fence);
        var win = new MainWindow(vm);

        win.Closed += (s, e) =>
        {
            _fenceWindows.Remove(win);
            MemoryOptimizerService.TrimWorkingSet();
        };

        _fenceWindows.Add(win);
        win.Show();
        return win;
    }

    public static FenceModel CreateNewFenceAt(double x, double y, double width, double height, string? title = null)
    {
        var config = _storageService.LoadConfig();
        var newFence = new FenceModel
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = title ?? $"Fence {config.Fences.Count + 1}",
            X = x,
            Y = y,
            Width = Math.Max(180, width),
            Height = Math.Max(120, height),
            Tabs = new List<TabModel>
            {
                new TabModel { Name = "Items", Items = new List<ItemModel>() }
            }
        };

        config.Fences.Add(newFence);
        _storageService.SaveConfig(config);

        Current?.Dispatcher?.Invoke(() =>
        {
            SpawnFenceWindow(newFence);
        });

        return newFence;
    }

    public static FenceModel CreateNewFolderPortalAt(double x, double y, double width, double height, string folderPath)
    {
        var config = _storageService.LoadConfig();
        string folderName = new DirectoryInfo(folderPath).Name;
        var newFence = new FenceModel
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = folderName,
            X = x,
            Y = y,
            Width = Math.Max(180, width),
            Height = Math.Max(120, height),
            IsFolderPortal = true,
            FolderPath = folderPath,
            Tabs = new List<TabModel>
            {
                new TabModel
                {
                    Name = folderName,
                    IconGlyph = "📁",
                    IsFolderPortal = true,
                    FolderPath = folderPath
                }
            }
        };

        config.Fences.Add(newFence);
        _storageService.SaveConfig(config);

        Current?.Dispatcher?.Invoke(() =>
        {
            SpawnFenceWindow(newFence);
        });

        return newFence;
    }

    public static void DeleteFenceById(string fenceId)
    {
        if (string.IsNullOrWhiteSpace(fenceId)) return;

        var config = _storageService.LoadConfig();
        var fenceToDelete = config.Fences.FirstOrDefault(f => f.Id == fenceId);

        // Collect all items across all non-portal tabs in this fence to restore to Desktop
        var itemsToRestore = new List<ItemModel>();

        if (fenceToDelete != null && !fenceToDelete.IsFolderPortal && fenceToDelete.Tabs != null)
        {
            foreach (var tab in fenceToDelete.Tabs)
            {
                if (!tab.IsFolderPortal && tab.Items != null)
                {
                    itemsToRestore.AddRange(tab.Items);
                }
            }
        }

        // Also check any open in-memory window for this fence
        Current?.Dispatcher?.Invoke(() =>
        {
            var targetWin = _fenceWindows.FirstOrDefault(w => (w.DataContext as MainViewModel)?.CurrentFence?.Id == fenceId);
            if (targetWin?.DataContext is MainViewModel vm && vm.CurrentFence != null && !vm.CurrentFence.IsFolderPortal)
            {
                foreach (var tab in vm.CurrentFence.Tabs)
                {
                    if (!tab.IsFolderPortal && tab.Items != null)
                    {
                        foreach (var it in tab.Items)
                        {
                            if (!itemsToRestore.Any(existing => existing.Id == it.Id))
                            {
                                itemsToRestore.Add(it);
                            }
                        }
                    }
                }
            }
        });

        // Automatically restore all fence items back to Windows Desktop!
        foreach (var item in itemsToRestore)
        {
            MainViewModel.ReturnItemToDesktop(item);
        }

        // 1. Permanently remove from persisted storage
        config.Fences.RemoveAll(f => f.Id == fenceId);
        _storageService.SaveConfig(config);

        // 2. Synchronize all active windows in memory
        Current?.Dispatcher?.Invoke(() =>
        {
            var targetWin = _fenceWindows.FirstOrDefault(w => (w.DataContext as MainViewModel)?.CurrentFence?.Id == fenceId);
            if (targetWin != null)
            {
                targetWin.IsDeleting = true;
                targetWin.Close();
                _fenceWindows.Remove(targetWin);
            }

            foreach (var win in _fenceWindows)
            {
                if (win.DataContext is MainViewModel vm)
                {
                    vm.Config.Fences.RemoveAll(f => f.Id == fenceId);
                }
            }
        });
    }

    public static void OpenSettingsWindow()
    {
        Current?.Dispatcher?.Invoke(() =>
        {
            foreach (Window win in Current.Windows)
            {
                if (win is SettingsWindow settingsWin)
                {
                    settingsWin.Activate();
                    if (settingsWin.WindowState == WindowState.Minimized)
                        settingsWin.WindowState = WindowState.Normal;
                    return;
                }
            }

            var vm = _fenceWindows.FirstOrDefault()?.DataContext as MainViewModel ?? new MainViewModel(_storageService);
            var newSettings = new SettingsWindow
            {
                DataContext = vm
            };
            newSettings.Show();
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _systemTrayService?.Dispose();
        _desktopSelectionService?.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
