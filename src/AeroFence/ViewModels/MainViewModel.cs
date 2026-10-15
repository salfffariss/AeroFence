using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AeroFence.Models;
using AeroFence.Services.Native;
using AeroFence.Services.Storage;
using AeroFence.Services.Tracking;

namespace AeroFence.ViewModels;

/// <summary>
/// MainViewModel controls presentation logic for fences, tabs, and items.
/// Decoupled from the View (XAML) using MVVM data binding.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IStorageService _storageService;
    private readonly PlaytimeTrackerService _trackerService = new();

    [ObservableProperty]
    private string _title = "AeroFence";

    [ObservableProperty]
    private bool _isRenamingTitle = false;

    [ObservableProperty]
    private AppConfig _config = new();

    [ObservableProperty]
    private FenceModel? _currentFence;

    [ObservableProperty]
    private TabModel? _selectedTab;

    [ObservableProperty]
    private ObservableCollection<TabModel> _tabs = new();

    [ObservableProperty]
    private ObservableCollection<ItemModel> _currentItems = new();

    [ObservableProperty]
    private IconSizeOption _currentIconSize = IconSizeOption.Medium;

    [ObservableProperty]
    private bool _isFolderPortal = false;

    [ObservableProperty]
    private string? _currentFolderPath;

    [ObservableProperty]
    private bool _canNavigateUp = false;

    private System.IO.FileSystemWatcher? _folderWatcher;
    private readonly System.Windows.Threading.DispatcherTimer _folderReloadDebounceTimer;

    public double CardWidth => CurrentIconSize switch
    {
        IconSizeOption.Small => 64,
        IconSizeOption.Large => 98,
        _ => 80
    };

    public double IconPixelSize => CurrentIconSize switch
    {
        IconSizeOption.Small => 24,
        IconSizeOption.Large => 48,
        _ => 36
    };

    public double IconContainerSize => CurrentIconSize switch
    {
        IconSizeOption.Small => 28,
        IconSizeOption.Large => 52,
        _ => 40
    };

    public double TextFontSize => CurrentIconSize switch
    {
        IconSizeOption.Small => 10,
        IconSizeOption.Large => 12,
        _ => 11
    };

    public double TextMaxWidth => CurrentIconSize switch
    {
        IconSizeOption.Small => 58,
        IconSizeOption.Large => 90,
        _ => 72
    };

    public bool IsSmallIconSize => CurrentIconSize == IconSizeOption.Small;
    public bool IsMediumIconSize => CurrentIconSize == IconSizeOption.Medium;
    public bool IsLargeIconSize => CurrentIconSize == IconSizeOption.Large;

    partial void OnCurrentIconSizeChanged(IconSizeOption value)
    {
        OnPropertyChanged(nameof(CardWidth));
        OnPropertyChanged(nameof(IconPixelSize));
        OnPropertyChanged(nameof(IconContainerSize));
        OnPropertyChanged(nameof(TextFontSize));
        OnPropertyChanged(nameof(TextMaxWidth));
        OnPropertyChanged(nameof(IsSmallIconSize));
        OnPropertyChanged(nameof(IsMediumIconSize));
        OnPropertyChanged(nameof(IsLargeIconSize));

        if (CurrentFence != null)
        {
            CurrentFence.IconSize = value;
            _storageService.SaveConfig(Config);
        }
    }

    public MainViewModel() : this(new JsonStorageService())
    {
    }

    public MainViewModel(IStorageService storageService, FenceModel? targetFence = null)
    {
        _storageService = storageService;

        _folderReloadDebounceTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _folderReloadDebounceTimer.Tick += (s, e) =>
        {
            _folderReloadDebounceTimer.Stop();
            if (IsFolderPortal && !string.IsNullOrEmpty(CurrentFolderPath))
            {
                LoadFolderContents();
            }
        };

        InitializeData(targetFence);
    }

    private void InitializeData(FenceModel? targetFence = null)
    {
        Config = _storageService.LoadConfig();

        if (targetFence != null)
        {
            CurrentFence = Config.Fences.FirstOrDefault(f => f.Id == targetFence.Id) ?? targetFence;
        }
        else
        {
            CurrentFence = Config.Fences.FirstOrDefault();
        }

        if (CurrentFence == null)
        {
            CurrentFence = new FenceModel
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = "Workspace",
                Tabs = new List<TabModel>
                {
                    new TabModel { Name = "Games", Items = new List<ItemModel>() },
                    new TabModel { Name = "Tools", Items = new List<ItemModel>() }
                }
            };
            Config.Fences.Add(CurrentFence);
            _storageService.SaveConfig(Config);
        }

        // Ensure fence has at least one tab, migrating legacy folder portals if needed
        if (CurrentFence.Tabs == null || CurrentFence.Tabs.Count == 0)
        {
            if (CurrentFence.IsFolderPortal && !string.IsNullOrWhiteSpace(CurrentFence.FolderPath))
            {
                var portalTab = new TabModel
                {
                    Name = CurrentFence.Title,
                    IconGlyph = "📁",
                    IsFolderPortal = true,
                    FolderPath = CurrentFence.FolderPath
                };
                CurrentFence.Tabs = new List<TabModel> { portalTab };
            }
            else
            {
                CurrentFence.Tabs = new List<TabModel>
                {
                    new TabModel { Name = "Items", IconGlyph = "📁" }
                };
            }
            _storageService.SaveConfig(Config);
        }
        else if (CurrentFence.IsFolderPortal && CurrentFence.Tabs.Count > 0)
        {
            // Sync legacy fence folder properties onto first tab if not already set
            if (!CurrentFence.Tabs[0].IsFolderPortal && !string.IsNullOrWhiteSpace(CurrentFence.FolderPath))
            {
                CurrentFence.Tabs[0].IsFolderPortal = true;
                CurrentFence.Tabs[0].FolderPath = CurrentFence.FolderPath;
            }
        }

        Title = CurrentFence.Title;
        CurrentIconSize = CurrentFence.IconSize;
        Tabs = new ObservableCollection<TabModel>(CurrentFence.Tabs);

        int activeIdx = CurrentFence.ActiveTabIndex;
        if (activeIdx >= 0 && activeIdx < Tabs.Count)
        {
            SelectedTab = Tabs[activeIdx];
        }
        else
        {
            SelectedTab = Tabs.FirstOrDefault();
        }

        if (SelectedTab != null && SelectedTab.IsFolderPortal && !string.IsNullOrWhiteSpace(SelectedTab.FolderPath) && System.IO.Directory.Exists(SelectedTab.FolderPath))
        {
            IsFolderPortal = true;
            CurrentFolderPath = SelectedTab.FolderPath;
            LoadFolderContents();
        }
        else if (CurrentFence.IsFolderPortal && !string.IsNullOrWhiteSpace(CurrentFence.FolderPath) && System.IO.Directory.Exists(CurrentFence.FolderPath))
        {
            IsFolderPortal = true;
            CurrentFolderPath = CurrentFence.FolderPath;
            LoadFolderContents();
        }
        else
        {
            IsFolderPortal = false;
            CurrentFolderPath = null;
            RefreshCurrentItems();
        }

        OnPropertyChanged(nameof(ShowTabBar));
    }

    public bool ShowTabBar => Tabs.Count > 1 || (SelectedTab != null && !SelectedTab.IsFolderPortal);

    partial void OnSelectedTabChanged(TabModel? value)
    {
        if (value != null && CurrentFence != null)
        {
            CurrentFence.ActiveTabIndex = Tabs.IndexOf(value);
            _storageService.SaveConfig(Config);
        }

        if (value != null && value.IsFolderPortal && !string.IsNullOrWhiteSpace(value.FolderPath) && System.IO.Directory.Exists(value.FolderPath))
        {
            IsFolderPortal = true;
            CurrentFolderPath = value.FolderPath;
            LoadFolderContents();
        }
        else
        {
            IsFolderPortal = false;
            CurrentFolderPath = null;
            _folderWatcher?.Dispose();
            _folderWatcher = null;
            RefreshCurrentItems();
        }

        OnPropertyChanged(nameof(ShowTabBar));
        OnPropertyChanged(nameof(CanNavigateUp));
    }

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchQuery);

    public string MatchCountText
    {
        get
        {
            if (!IsSearching) return string.Empty;
            int matches = CurrentItems.Count(i => !i.IsDimmed);
            return matches == 1 ? "1 match" : $"{matches} matches";
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        OnPropertyChanged(nameof(IsSearching));
        ApplySearchFilter();
        OnPropertyChanged(nameof(MatchCountText));
    }

    public void ApplySearchFilter()
    {
        string query = SearchQuery.Trim();

        if (string.IsNullOrEmpty(query))
        {
            foreach (var item in CurrentItems)
            {
                item.IsDimmed = false;
                item.IsBestMatch = false;
            }
            return;
        }

        string[] searchWords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        bool foundFirst = false;

        foreach (var item in CurrentItems)
        {
            string name = item.DisplayName;
            string fileName = System.IO.Path.GetFileNameWithoutExtension(item.TargetPath);

            bool matches = searchWords.Length == 0 || searchWords.All(word =>
                name.Contains(word, StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains(word, StringComparison.OrdinalIgnoreCase));

            item.IsDimmed = !matches;

            if (matches && !foundFirst)
            {
                item.IsBestMatch = true;
                foundFirst = true;
            }
            else
            {
                item.IsBestMatch = false;
            }
        }
    }

    [RelayCommand]
    public void ClearSearch()
    {
        SearchQuery = string.Empty;
    }

    [RelayCommand]
    public void LaunchBestMatch()
    {
        if (!IsSearching) return;
        var best = CurrentItems.FirstOrDefault(i => i.IsBestMatch) ?? CurrentItems.FirstOrDefault(i => !i.IsDimmed);
        if (best != null)
        {
            LaunchItem(best);
            ClearSearch();
        }
    }

    private void RefreshCurrentItems()
    {
        if (SelectedTab != null)
        {
            // Self-heal: ensure existing .lnk items have clean target icons without shortcut badges
            foreach (var item in SelectedTab.Items)
            {
                if (string.IsNullOrEmpty(item.CustomIconPath) && !string.IsNullOrEmpty(item.TargetPath) &&
                    string.Equals(System.IO.Path.GetExtension(item.TargetPath), ".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    string target = Services.Native.ShellIconService.ResolveCleanTarget(item.TargetPath);
                    if (!string.Equals(target, item.TargetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        item.CustomIconPath = target;
                    }
                }
            }

            CurrentItems = new ObservableCollection<ItemModel>(SelectedTab.Items);
            if (IsSearching)
            {
                ApplySearchFilter();
            }
        }
        else
        {
            CurrentItems.Clear();
        }
    }

    [RelayCommand]
    public void SelectTab(TabModel tab)
    {
        if (tab != null)
        {
            SelectedTab = tab;
        }
    }

    [RelayCommand]
    public void AddNewTab()
    {
        if (CurrentFence == null) return;

        int newIndex = Tabs.Count + 1;
        var newTab = new TabModel
        {
            Name = $"Tab {newIndex}",
            IconGlyph = "📁",
            LayoutMode = TabLayoutMode.FreeForm,
            Items = new List<ItemModel>()
        };

        CurrentFence.Tabs.Add(newTab);
        Tabs.Add(newTab);
        SelectedTab = newTab;

        OnPropertyChanged(nameof(ShowTabBar));
        _storageService.SaveConfig(Config);
    }

    [RelayCommand]
    public void AddFolderPortalTab()
    {
        if (CurrentFence == null) return;

        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Folder for Portal Tab"
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            string folderName = new System.IO.DirectoryInfo(dialog.FolderName).Name;
            var newTab = new TabModel
            {
                Name = folderName,
                IconGlyph = "📁",
                IsFolderPortal = true,
                FolderPath = dialog.FolderName,
                Items = new List<ItemModel>()
            };

            CurrentFence.Tabs.Add(newTab);
            Tabs.Add(newTab);
            SelectedTab = newTab;

            OnPropertyChanged(nameof(ShowTabBar));
            _storageService.SaveConfig(Config);
        }
    }

    [RelayCommand]
    public void DeleteTab(TabModel tab)
    {
        if (tab == null || Tabs.Count <= 1 || CurrentFence == null) return;

        // Restore items from deleted tab back to desktop
        if (!tab.IsFolderPortal && tab.Items != null)
        {
            foreach (var item in tab.Items)
            {
                ReturnItemToDesktop(item);
            }
        }

        CurrentFence.Tabs.Remove(tab);
        Tabs.Remove(tab);
        SelectedTab = Tabs.FirstOrDefault();

        OnPropertyChanged(nameof(ShowTabBar));
        SaveConfig();
    }

    [RelayCommand]
    public void StartRenameTab(TabModel? tab)
    {
        if (tab == null) tab = SelectedTab;
        if (tab == null) return;

        foreach (var t in Tabs)
        {
            t.IsEditing = false;
        }

        tab.IsEditing = true;
    }

    [RelayCommand]
    public void CommitRenameTab(TabModel? tab)
    {
        if (tab == null) return;
        string trimmed = tab.Name?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(trimmed))
        {
            tab.Name = trimmed;
            tab.IconGlyph = TabModel.DetectSmartIcon(trimmed);
            SaveConfig();
        }
        else
        {
            tab.Name = "New Tab";
            tab.IconGlyph = TabModel.DetectSmartIcon("New Tab");
            SaveConfig();
        }
        tab.IsEditing = false;
    }

    [RelayCommand]
    public void StartRenameFenceTitle()
    {
        IsRenamingTitle = true;
    }

    [RelayCommand]
    public void CommitRenameFenceTitle()
    {
        string trimmed = Title?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(trimmed))
        {
            Title = trimmed;
            if (CurrentFence != null)
            {
                CurrentFence.Title = trimmed;
                SaveConfig();
            }
        }
        else
        {
            Title = CurrentFence?.Title ?? "AeroFence";
        }
        IsRenamingTitle = false;
    }

    [RelayCommand]
    public void StartRenameItem(ItemModel? item)
    {
        if (item == null) return;
        foreach (var i in CurrentItems) i.IsEditing = false;
        foreach (var t in Tabs) t.IsEditing = false;
        IsRenamingTitle = false;

        item.IsEditing = true;
    }

    [RelayCommand]
    public void CommitRenameItem(ItemModel? item)
    {
        if (item == null) return;
        string trimmed = item.DisplayName?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(trimmed))
        {
            item.DisplayName = trimmed;
            SaveConfig();
        }
        else
        {
            string fallback = System.IO.Path.GetFileNameWithoutExtension(item.TargetPath);
            item.DisplayName = !string.IsNullOrEmpty(fallback) ? fallback : "Shortcut";
            SaveConfig();
        }
        item.IsEditing = false;
    }

    public void SetTabIcon(TabModel? tab, string icon)
    {
        if (tab == null || string.IsNullOrEmpty(icon)) return;
        tab.IconGlyph = icon;
        SaveConfig();
    }

    public void NavigateItems(System.Windows.Input.Key direction, int columns)
    {
        if (CurrentItems.Count == 0) return;

        int currentIndex = -1;
        for (int i = 0; i < CurrentItems.Count; i++)
        {
            if (CurrentItems[i].IsSelected)
            {
                currentIndex = i;
                break;
            }
        }

        if (currentIndex == -1)
        {
            SelectItem(CurrentItems[0]);
            return;
        }

        int targetIndex = currentIndex;
        switch (direction)
        {
            case System.Windows.Input.Key.Right:
                targetIndex = Math.Min(CurrentItems.Count - 1, currentIndex + 1);
                break;
            case System.Windows.Input.Key.Left:
                targetIndex = Math.Max(0, currentIndex - 1);
                break;
            case System.Windows.Input.Key.Down:
                targetIndex = Math.Min(CurrentItems.Count - 1, currentIndex + columns);
                break;
            case System.Windows.Input.Key.Up:
                targetIndex = Math.Max(0, currentIndex - columns);
                break;
        }

        if (targetIndex != currentIndex)
        {
            SelectItem(CurrentItems[targetIndex]);
        }
    }

    [RelayCommand]
    public void SelectItem(ItemModel? item)
    {
        if (item == null) return;
        bool isCtrl = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control;

        if (isCtrl)
        {
            item.IsSelected = !item.IsSelected;
        }
        else
        {
            foreach (var it in CurrentItems)
            {
                it.IsSelected = (it == item);
            }
        }
    }

    [RelayCommand]
    public void DeselectAll()
    {
        foreach (var it in CurrentItems)
        {
            it.IsSelected = false;
        }
    }

    [RelayCommand]
    public void LaunchItem(ItemModel? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.TargetPath)) return;
        try
        {
            string targetPath = item.TargetPath;

            // 1. Directory / Folder launch: open in Explorer or navigate inside portal
            if (System.IO.Directory.Exists(targetPath))
            {
                if (IsFolderPortal)
                {
                    NavigateToFolder(targetPath);
                    return;
                }

                System.Diagnostics.Process.Start("explorer.exe", $"\"{targetPath}\"");
                _trackerService.TrackFolder(item, targetPath, () => _storageService.SaveConfig(Config));
                return;
            }

            string? workingDir = item.WorkingDirectory;

            // 2. Shortcut (.lnk) resolution
            if (string.Equals(System.IO.Path.GetExtension(targetPath), ".lnk", StringComparison.OrdinalIgnoreCase))
            {
                string resolved = ResolveShortcutTarget(targetPath);
                if (System.IO.Directory.Exists(resolved))
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"\"{resolved}\"");
                    _trackerService.TrackFolder(item, resolved, () => _storageService.SaveConfig(Config));
                    return;
                }
                else if (System.IO.File.Exists(resolved))
                {
                    targetPath = resolved;
                    if (string.IsNullOrWhiteSpace(workingDir))
                    {
                        workingDir = System.IO.Path.GetDirectoryName(resolved);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(workingDir) || !System.IO.Directory.Exists(workingDir))
            {
                if (System.IO.File.Exists(targetPath))
                {
                    workingDir = System.IO.Path.GetDirectoryName(targetPath);
                }
            }

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = targetPath,
                UseShellExecute = true
            };
            if (!string.IsNullOrWhiteSpace(item.Arguments))
            {
                psi.Arguments = item.Arguments;
            }
            if (!string.IsNullOrWhiteSpace(workingDir) && System.IO.Directory.Exists(workingDir))
            {
                psi.WorkingDirectory = workingDir;
            }

            var proc = System.Diagnostics.Process.Start(psi);
            if (proc != null)
            {
                string targetHint = System.IO.Path.GetFileNameWithoutExtension(targetPath);
                _trackerService.TrackProcess(item, proc, targetHint, () => _storageService.SaveConfig(Config));
            }
            else
            {
                item.Stats.LaunchCount++;
                item.Stats.LastPlayed = DateTime.Now;
                _storageService.SaveConfig(Config);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LaunchItem] Failed to launch {item.TargetPath}: {ex.Message}");
        }
    }

    [RelayCommand]
    public void OpenItemLocation(ItemModel? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.TargetPath)) return;
        try
        {
            string path = item.TargetPath;
            if (System.IO.File.Exists(path) || System.IO.Directory.Exists(path))
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OpenItemLocation] Error: {ex.Message}");
        }
    }

    [RelayCommand]
    public void OpenInTerminal(ItemModel? item)
    {
        string? targetDir = null;

        if (item != null && !string.IsNullOrWhiteSpace(item.TargetPath))
        {
            string path = item.TargetPath;
            if (System.IO.Directory.Exists(path))
            {
                targetDir = path;
            }
            else if (System.IO.File.Exists(path))
            {
                if (string.Equals(System.IO.Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    string resolved = ResolveShortcutTarget(path);
                    if (System.IO.Directory.Exists(resolved))
                    {
                        targetDir = resolved;
                    }
                    else if (System.IO.File.Exists(resolved))
                    {
                        targetDir = System.IO.Path.GetDirectoryName(resolved);
                    }
                }

                if (string.IsNullOrWhiteSpace(targetDir))
                {
                    targetDir = System.IO.Path.GetDirectoryName(path);
                }
            }
        }

        if (string.IsNullOrWhiteSpace(targetDir) || !System.IO.Directory.Exists(targetDir))
        {
            targetDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "wt.exe",
                Arguments = $"-d \"{targetDir}\"",
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    WorkingDirectory = targetDir,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OpenInTerminal] Failed: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    public void RemoveItem(ItemModel? item)
    {
        if (item == null || SelectedTab == null) return;

        // When removing an item from the fence, automatically return it to the Windows Desktop!
        ReturnItemToDesktop(item);

        SelectedTab.Items.Remove(item);
        CurrentItems.Remove(item);
        SaveConfig();
    }

    /// <summary>
    /// Restores a shortcut or file back onto the Windows Desktop and flushes shell icon state.
    /// </summary>
    public static void ReturnItemToDesktop(ItemModel? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.TargetPath)) return;

        try
        {
            string userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string srcPath = item.TargetPath;
            string shortcutsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Shortcuts");

            if (System.IO.File.Exists(srcPath))
            {
                string fileName = System.IO.Path.GetFileName(srcPath);
                string destPath = System.IO.Path.Combine(userDesktop, fileName);

                // If already on desktop (same path), do nothing
                if (string.Equals(srcPath, destPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (System.IO.File.Exists(destPath))
                {
                    // File already on desktop (e.g. from Drag & Drop FileDrop copy). Clean up storage copy!
                    if (srcPath.StartsWith(shortcutsDir, StringComparison.OrdinalIgnoreCase))
                    {
                        try { System.IO.File.Delete(srcPath); } catch { }
                    }
                }
                else
                {
                    // Move from Data\Shortcuts back to Desktop!
                    System.IO.File.Move(srcPath, destPath);
                }

                // Notify Windows Shell that file is now on desktop
                NativeMethods.SHChangeNotify(
                    NativeMethods.SHCNE_CREATE,
                    NativeMethods.SHCNF_PATHW | NativeMethods.SHCNF_FLUSH,
                    destPath,
                    null);

                NativeMethods.SHChangeNotify(
                    NativeMethods.SHCNE_UPDATEDIR,
                    NativeMethods.SHCNF_PATHW | NativeMethods.SHCNF_FLUSH,
                    userDesktop,
                    null);
            }
            else
            {
                // Fallback: If physical shortcut was removed or external, recreate desktop shortcut so it's never lost
                string targetExe = !string.IsNullOrWhiteSpace(item.CustomIconPath) ? item.CustomIconPath : item.TargetPath;
                if (System.IO.File.Exists(targetExe))
                {
                    string shortcutName = item.DisplayName;
                    if (!shortcutName.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                    {
                        shortcutName += ".lnk";
                    }
                    string destPath = System.IO.Path.Combine(userDesktop, shortcutName);
                    CreateShortcut(destPath, targetExe, item.WorkingDirectory, item.Arguments);

                    NativeMethods.SHChangeNotify(
                        NativeMethods.SHCNE_CREATE,
                        NativeMethods.SHCNF_PATHW | NativeMethods.SHCNF_FLUSH,
                        destPath,
                        null);

                    NativeMethods.SHChangeNotify(
                        NativeMethods.SHCNE_UPDATEDIR,
                        NativeMethods.SHCNF_PATHW | NativeMethods.SHCNF_FLUSH,
                        userDesktop,
                        null);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ReturnItemToDesktop] Error: {ex.Message}");
        }
    }

    public static void CreateShortcut(string shortcutPath, string targetPath, string? workingDir = null, string? arguments = null)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell != null)
                {
                    dynamic sc = shell.CreateShortcut(shortcutPath);
                    sc.TargetPath = targetPath;
                    if (!string.IsNullOrWhiteSpace(arguments)) sc.Arguments = arguments;
                    if (!string.IsNullOrWhiteSpace(workingDir)) sc.WorkingDirectory = workingDir;
                    else sc.WorkingDirectory = System.IO.Path.GetDirectoryName(targetPath);
                    sc.Save();
                }
            }
        }
        catch { }
    }

    [RelayCommand]
    public void SetIconSize(IconSizeOption size)
    {
        CurrentIconSize = size;
    }

    [RelayCommand]
    public void SortByName()
    {
        if (SelectedTab == null) return;
        var sorted = SelectedTab.Items.OrderBy(i => i.DisplayName).ToList();
        SelectedTab.Items = sorted;
        CurrentItems = new ObservableCollection<ItemModel>(sorted);
        _storageService.SaveConfig(Config);
    }

    [RelayCommand]
    public void SortByPlaytime()
    {
        if (SelectedTab == null) return;
        var sorted = SelectedTab.Items.OrderByDescending(i => i.Stats.TotalPlaytimeSeconds).ToList();
        SelectedTab.Items = sorted;
        CurrentItems = new ObservableCollection<ItemModel>(sorted);
        _storageService.SaveConfig(Config);
    }

    [RelayCommand]
    public void SortByLaunchCount()
    {
        if (SelectedTab == null) return;
        var sorted = SelectedTab.Items.OrderByDescending(i => i.Stats.LaunchCount).ToList();
        SelectedTab.Items = sorted;
        CurrentItems = new ObservableCollection<ItemModel>(sorted);
        _storageService.SaveConfig(Config);
    }

    public void ReorderItems(ItemModel sourceItem, ItemModel targetItem)
    {
        if (sourceItem == null || targetItem == null || sourceItem == targetItem || SelectedTab == null) return;

        int oldIndex = CurrentItems.IndexOf(sourceItem);
        int newIndex = CurrentItems.IndexOf(targetItem);

        if (oldIndex >= 0 && newIndex >= 0)
        {
            CurrentItems.Move(oldIndex, newIndex);

            // Synchronize internal tab items list
            SelectedTab.Items.Remove(sourceItem);
            SelectedTab.Items.Insert(newIndex, sourceItem);

            _storageService.SaveConfig(Config);
        }
    }

    public void MoveItemToEnd(ItemModel sourceItem)
    {
        if (sourceItem == null || SelectedTab == null) return;

        int oldIndex = CurrentItems.IndexOf(sourceItem);
        int lastIndex = CurrentItems.Count - 1;

        if (oldIndex >= 0 && oldIndex != lastIndex)
        {
            CurrentItems.Move(oldIndex, lastIndex);

            SelectedTab.Items.Remove(sourceItem);
            SelectedTab.Items.Add(sourceItem);

            _storageService.SaveConfig(Config);
        }
    }

    public void MoveItemToTab(ItemModel item, TabModel targetTab)
    {
        if (item == null || targetTab == null || SelectedTab == null || SelectedTab == targetTab) return;

        SelectedTab.Items.Remove(item);
        CurrentItems.Remove(item);

        targetTab.Items.Add(item);

        _storageService.SaveConfig(Config);
    }

    public void AddItemsFromPaths(IEnumerable<string> filePaths, TabModel? targetTab = null)
    {
        if (filePaths == null) return;

        var tab = targetTab ?? SelectedTab;
        if (tab == null) return;

        // 1. If target tab is in Folder Portal mode, move/copy dropped items into that folder
        if (tab.IsFolderPortal && !string.IsNullOrEmpty(tab.FolderPath) && System.IO.Directory.Exists(tab.FolderPath))
        {
            foreach (var path in filePaths)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                string destName = System.IO.Path.GetFileName(path);
                string destPath = System.IO.Path.Combine(tab.FolderPath, destName);
                if (!string.Equals(path, destPath, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        if (System.IO.File.Exists(path))
                        {
                            if (IsOnDesktop(path))
                            {
                                if (System.IO.File.Exists(destPath)) System.IO.File.Delete(destPath);
                                System.IO.File.Move(path, destPath);
                                NotifyDesktopItemChanged(path);
                            }
                            else
                            {
                                System.IO.File.Copy(path, destPath, overwrite: true);
                            }
                        }
                        else if (System.IO.Directory.Exists(path))
                        {
                            if (IsOnDesktop(path))
                            {
                                System.IO.Directory.Move(path, destPath);
                                NotifyDesktopItemChanged(path);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FolderPortal Drop] Move failed: {ex.Message}");
                    }
                }
            }
            if (tab == SelectedTab)
            {
                LoadFolderContents();
            }
            return;
        }

        // 2. Standard Fence Tab: Move desktop shortcuts into Data\Shortcuts storage
        bool addedAny = false;
        string shortcutsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Shortcuts");
        try
        {
            System.IO.Directory.CreateDirectory(shortcutsDir);
        }
        catch { }

        foreach (var path in filePaths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;

            string fullPath = System.IO.Path.GetFullPath(path);
            bool isDesktopItem = IsOnDesktop(fullPath);

            string storedPath = fullPath;
            string displayName;
            string? workingDir = null;
            string? arguments = null;
            string? customIcon = null;

            if (System.IO.Directory.Exists(fullPath))
            {
                displayName = new System.IO.DirectoryInfo(fullPath).Name;
                workingDir = fullPath;
            }
            else
            {
                string ext = System.IO.Path.GetExtension(fullPath).ToLowerInvariant();
                displayName = (ext is ".lnk" or ".url" or ".exe" or ".bat" or ".cmd")
                    ? System.IO.Path.GetFileNameWithoutExtension(fullPath)
                    : System.IO.Path.GetFileName(fullPath);

                if (ext == ".lnk")
                {
                    var details = ResolveShortcutDetails(fullPath);
                    if (details != null)
                    {
                        if (!string.IsNullOrWhiteSpace(details.TargetPath))
                        {
                            customIcon = details.TargetPath;
                        }
                        arguments = details.Arguments;
                        workingDir = details.WorkingDirectory;
                    }
                }

                if (string.IsNullOrWhiteSpace(workingDir))
                {
                    workingDir = System.IO.Path.GetDirectoryName(fullPath);
                }

                // If dropped from Windows Desktop, MOVE the shortcut/file into AeroFence's storage folder
                if (isDesktopItem)
                {
                    try
                    {
                        string fileName = System.IO.Path.GetFileName(fullPath);
                        string destPath = System.IO.Path.Combine(shortcutsDir, fileName);

                        if (System.IO.File.Exists(destPath))
                        {
                            System.IO.File.Delete(destPath);
                        }

                        System.IO.File.Move(fullPath, destPath);
                        NotifyDesktopItemChanged(fullPath);
                        storedPath = destPath;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AddItemsFromPaths] Move shortcut from desktop error: {ex.Message}");
                    }
                }
            }

            // Avoid duplicate items within the same tab
            if (tab.Items.Any(i => string.Equals(i.TargetPath, storedPath, StringComparison.OrdinalIgnoreCase)
                                || (!string.IsNullOrEmpty(customIcon) && string.Equals(i.CustomIconPath, customIcon, StringComparison.OrdinalIgnoreCase))))
            {
                continue;
            }

            var newItem = new ItemModel
            {
                DisplayName = displayName,
                TargetPath = storedPath,
                CustomIconPath = customIcon,
                WorkingDirectory = workingDir,
                Arguments = arguments,
                Stats = new PlaytimeStats()
            };

            tab.Items.Add(newItem);
            if (tab == SelectedTab)
            {
                CurrentItems.Add(newItem);
            }
            addedAny = true;
        }

        if (addedAny)
        {
            SaveConfig();
        }
    }

    public void SaveConfig()
    {
        if (CurrentFence != null)
        {
            var diskConfig = _storageService.LoadConfig();
            var existingIndex = diskConfig.Fences.FindIndex(f => f.Id == CurrentFence.Id);
            if (existingIndex >= 0)
            {
                diskConfig.Fences[existingIndex] = CurrentFence;
                _storageService.SaveConfig(diskConfig);
                Config = diskConfig;
                return;
            }
            else
            {
                // Fence was removed/deleted from configuration! Do not resurrect it!
                return;
            }
        }
        _storageService.SaveConfig(Config);
    }

    public FenceModel CreateNewFence(double x, double y, double width, double height, string? title = null)
    {
        var newFence = new FenceModel
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = title ?? $"Fence {Config.Fences.Count + 1}",
            X = x,
            Y = y,
            Width = Math.Max(280, width),
            Height = Math.Max(180, height),
            Tabs = new List<TabModel>
            {
                new TabModel { Name = "Items", Items = new List<ItemModel>() }
            }
        };

        Config.Fences.Add(newFence);
        _storageService.SaveConfig(Config);

        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            var vm = new MainViewModel(_storageService, newFence);
            var win = new MainWindow(vm);
            win.Show();
        });

        return newFence;
    }

    [RelayCommand]
    public void DeleteFence()
    {
        if (CurrentFence == null) return;
        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to delete fence '{CurrentFence.Title}'?\n\nNote: Your files and shortcuts will NOT be deleted from your computer.",
            "Delete AeroFence",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            App.DeleteFenceById(CurrentFence.Id);
        }
    }

    [RelayCommand]
    public void NavigateUp()
    {
        if (!IsFolderPortal || string.IsNullOrEmpty(CurrentFolderPath)) return;
        var parent = System.IO.Directory.GetParent(CurrentFolderPath);
        if (parent != null && parent.Exists)
        {
            NavigateToFolder(parent.FullName);
        }
    }

    public void NavigateToFolder(string path)
    {
        if (!System.IO.Directory.Exists(path)) return;
        CurrentFolderPath = path;
        string folderName = new System.IO.DirectoryInfo(path).Name;

        if (SelectedTab != null && SelectedTab.IsFolderPortal)
        {
            SelectedTab.FolderPath = CurrentFolderPath;
        }

        if (CurrentFence != null)
        {
            CurrentFence.FolderPath = CurrentFolderPath;
            if (Tabs.Count <= 1)
            {
                CurrentFence.Title = folderName;
                Title = folderName;
            }
            SaveConfig();
        }
        LoadFolderContents();
    }

    public void LoadFolderContents()
    {
        if (string.IsNullOrEmpty(CurrentFolderPath) || !System.IO.Directory.Exists(CurrentFolderPath))
        {
            CurrentItems.Clear();
            CanNavigateUp = false;
            return;
        }

        SetupFolderWatcher(CurrentFolderPath);
        CanNavigateUp = System.IO.Directory.GetParent(CurrentFolderPath) != null;

        var list = new List<ItemModel>();
        try
        {
            var dirInfo = new System.IO.DirectoryInfo(CurrentFolderPath);

            // 1. Folders first (alphabetical)
            foreach (var dir in dirInfo.GetDirectories().OrderBy(d => d.Name))
            {
                if ((dir.Attributes & System.IO.FileAttributes.Hidden) != 0) continue;
                list.Add(new ItemModel
                {
                    DisplayName = dir.Name,
                    TargetPath = dir.FullName,
                    WorkingDirectory = dir.FullName,
                    Stats = new PlaytimeStats()
                });
            }

            // 2. Files (alphabetical)
            foreach (var file in dirInfo.GetFiles().OrderBy(f => f.Name))
            {
                if ((file.Attributes & System.IO.FileAttributes.Hidden) != 0) continue;
                list.Add(new ItemModel
                {
                    DisplayName = file.Name,
                    TargetPath = file.FullName,
                    WorkingDirectory = file.DirectoryName,
                    Stats = new PlaytimeStats()
                });
            }
        }
        catch { }

        CurrentItems = new ObservableCollection<ItemModel>(list);
        if (IsSearching)
        {
            ApplySearchFilter();
        }
    }

    private void SetupFolderWatcher(string path)
    {
        try
        {
            _folderWatcher?.Dispose();
            _folderWatcher = null;

            if (!System.IO.Directory.Exists(path)) return;

            _folderWatcher = new System.IO.FileSystemWatcher(path)
            {
                NotifyFilter = System.IO.NotifyFilters.FileName | System.IO.NotifyFilters.DirectoryName | System.IO.NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            _folderWatcher.Created += (s, e) => RequestFolderReload();
            _folderWatcher.Deleted += (s, e) => RequestFolderReload();
            _folderWatcher.Renamed += (s, e) => RequestFolderReload();
        }
        catch { }
    }

    private void RequestFolderReload()
    {
        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            _folderReloadDebounceTimer.Stop();
            _folderReloadDebounceTimer.Start();
        });
    }

    [RelayCommand]
    public void ConvertToFolderPortal()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Folder for AeroFence Portal"
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            SetAsFolderPortal(dialog.FolderName);
        }
    }

    public void SetAsFolderPortal(string folderPath)
    {
        if (!System.IO.Directory.Exists(folderPath)) return;

        IsFolderPortal = true;
        CurrentFolderPath = folderPath;
        Title = new System.IO.DirectoryInfo(folderPath).Name;

        if (CurrentFence != null)
        {
            CurrentFence.IsFolderPortal = true;
            CurrentFence.FolderPath = folderPath;
            CurrentFence.Title = Title;
            SaveConfig();
        }

        LoadFolderContents();
    }

    [RelayCommand]
    public void ConvertToStandardFence()
    {
        IsFolderPortal = false;
        CurrentFolderPath = null;
        _folderWatcher?.Dispose();
        _folderWatcher = null;

        if (CurrentFence != null)
        {
            CurrentFence.IsFolderPortal = false;
            CurrentFence.FolderPath = null;
            Title = CurrentFence.Title;
            SaveConfig();
        }

        RefreshCurrentItems();
    }

    [RelayCommand]
    public void OpenFolderInExplorer()
    {
        if (IsFolderPortal && !string.IsNullOrEmpty(CurrentFolderPath) && System.IO.Directory.Exists(CurrentFolderPath))
        {
            System.Diagnostics.Process.Start("explorer.exe", $"\"{CurrentFolderPath}\"");
        }
    }

    public FenceModel CreateNewFolderPortal(double x, double y, double width, double height, string folderPath)
    {
        string folderName = new System.IO.DirectoryInfo(folderPath).Name;
        var newFence = new FenceModel
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = folderName,
            X = x,
            Y = y,
            Width = Math.Max(280, width),
            Height = Math.Max(180, height),
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

        Config.Fences.Add(newFence);
        _storageService.SaveConfig(Config);

        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            var vm = new MainViewModel(_storageService, newFence);
            var win = new MainWindow(vm);
            win.Show();
        });

        return newFence;
    }

    public static bool IsOnDesktop(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            string fullPath = System.IO.Path.GetFullPath(path);
            string? parentDir = System.IO.Path.GetDirectoryName(fullPath);
            if (parentDir == null) return false;

            string userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

            return string.Equals(parentDir, userDesktop, StringComparison.OrdinalIgnoreCase)
                || string.Equals(parentDir, commonDesktop, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static void NotifyDesktopItemChanged(string filePath)
    {
        try
        {
            NativeMethods.SHChangeNotify(
                NativeMethods.SHCNE_DELETE,
                NativeMethods.SHCNF_PATHW | NativeMethods.SHCNF_FLUSH,
                filePath,
                null);

            string? parentDir = System.IO.Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(parentDir))
            {
                NativeMethods.SHChangeNotify(
                    NativeMethods.SHCNE_UPDATEDIR,
                    NativeMethods.SHCNF_PATHW | NativeMethods.SHCNF_FLUSH,
                    parentDir,
                    null);
            }
        }
        catch { }
    }

    public record ShortcutDetails(string TargetPath, string Arguments, string WorkingDirectory, string IconLocation);

    public static ShortcutDetails? ResolveShortcutDetails(string shortcutPath)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell != null)
                {
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);
                    return new ShortcutDetails(
                        shortcut.TargetPath ?? string.Empty,
                        shortcut.Arguments ?? string.Empty,
                        shortcut.WorkingDirectory ?? string.Empty,
                        shortcut.IconLocation ?? string.Empty
                    );
                }
            }
        }
        catch { }
        return null;
    }

    private static string ResolveShortcutTarget(string shortcutPath)
    {
        var details = ResolveShortcutDetails(shortcutPath);
        if (details != null && !string.IsNullOrWhiteSpace(details.TargetPath))
        {
            return details.TargetPath;
        }
        return shortcutPath;
    }
}
