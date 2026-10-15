using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using AeroFence.Services.Native;

namespace AeroFence;

/// <summary>
/// Interaction logic for MainWindow.xaml.
/// Implements custom borderless window chrome and Fences-style roll-up behavior.
/// </summary>
public partial class MainWindow : Window
{
    private readonly WindowResizer _windowResizer;
    private readonly DwmService _dwmService = new();
    private readonly DesktopManagerService _desktopManager = new();
    private bool _isPinned = true;
    private bool _isRolledUp = false;
    private double _previousHeight = 380;
    private bool _isContextMenuOpen = false;
    private bool _isManualRollUp = false;
    private readonly System.Windows.Threading.DispatcherTimer _mouseLeaveTimer;
    private readonly System.Windows.Threading.DispatcherTimer _saveMetricsDebounceTimer;
    private uint _wmOpenSettings = 0;
    private bool _isLoaded = false;
    public bool IsDeleting { get; set; } = false;

    private bool _isDraggingWindow = false;
    private NativeMethods.POINT _dragStartCursorScreen;
    private Point _dragStartWindowPos;

    public MainWindow() : this(new ViewModels.MainViewModel())
    {
    }

    public MainWindow(ViewModels.MainViewModel vm)
    {
        DataContext = vm;
        WindowStartupLocation = WindowStartupLocation.Manual;

        if (vm.CurrentFence != null)
        {
            if (vm.CurrentFence.Width >= 180) Width = vm.CurrentFence.Width;
            if (vm.CurrentFence.Height >= 120) Height = vm.CurrentFence.Height;
            if (!double.IsNaN(vm.CurrentFence.X)) Left = vm.CurrentFence.X;
            if (!double.IsNaN(vm.CurrentFence.Y)) Top = vm.CurrentFence.Y;
        }

        InitializeComponent();

        Loaded += (s, e) =>
        {
            _isLoaded = true;
        };

        _mouseLeaveTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(180)
        };
        _mouseLeaveTimer.Tick += OnMouseLeaveTimerTick;

        _saveMetricsDebounceTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _saveMetricsDebounceTimer.Tick += (s, e) =>
        {
            _saveMetricsDebounceTimer.Stop();
            SaveWindowMetrics(immediate: true);
        };

        // Attach native Win32 WM_NCHITTEST edge/corner resizer
        _windowResizer = new WindowResizer(this, borderMargin: 8);

        // Apply native Windows 11 hardware-accelerated Acrylic blur & Desktop attachment
        SourceInitialized += (sender, args) =>
        {
            if (DataContext is ViewModels.MainViewModel currentVm)
            {
                _dwmService.ApplyBackdrop(this, currentVm.Config.EnableAcrylic);

                if (currentVm.CurrentFence != null)
                {
                    if (currentVm.CurrentFence.X >= 0) Left = currentVm.CurrentFence.X;
                    if (currentVm.CurrentFence.Y >= 0) Top = currentVm.CurrentFence.Y;
                    if (currentVm.CurrentFence.Width >= 180) Width = currentVm.CurrentFence.Width;
                    if (currentVm.CurrentFence.Height >= 120) Height = currentVm.CurrentFence.Height;
                }

                if (currentVm.Config.AttachToDesktop)
                {
                    _desktopManager.AttachToDesktop(this);
                }
            }
            else
            {
                _dwmService.ApplyBackdrop(this);
            }

            // Register IPC message for single-instance Settings launch
            _wmOpenSettings = NativeMethods.RegisterWindowMessage("AeroFence_OpenSettings");
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                // Disable Windows 11 Snap Assist & Snap Layouts by removing WS_MAXIMIZEBOX
                const int GWL_STYLE = -16;
                const long WS_MAXIMIZEBOX = 0x00010000L;
                IntPtr currentStyle = DesktopManagerService.GetWindowLongPtr(hwnd, GWL_STYLE);
                DesktopManagerService.SetWindowLongPtr(hwnd, GWL_STYLE, (IntPtr)(currentStyle.ToInt64() & ~WS_MAXIMIZEBOX));

                var src = HwndSource.FromHwnd(hwnd);
                src?.AddHook(WndProcSettingsHook);
            }
        };

        Closing += (sender, args) =>
        {
            if (!IsDeleting && _isLoaded && !_isRolledUp)
            {
                SaveWindowMetrics(immediate: true);
            }
            _desktopManager.DetachFromDesktop(this);
        };

        // When window loses focus, commit any in-progress inline rename
        Deactivated += (sender, args) =>
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                if (vm.IsRenamingTitle)
                {
                    vm.CommitRenameFenceTitle();
                }
                var editingTab = vm.Tabs.FirstOrDefault(t => t.IsEditing);
                if (editingTab != null)
                {
                    vm.CommitRenameTab(editingTab);
                }
                var editingItem = vm.CurrentItems.FirstOrDefault(i => i.IsEditing);
                if (editingItem != null)
                {
                    vm.CommitRenameItem(editingItem);
                }
            }

            // Defer memory trimming until 3 seconds after user leaves the fence
            MemoryOptimizerService.NotifyInteraction();
        };

        // Track globally when any ContextMenu opens or closes to prevent unpinned auto-collapse
        EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent, new RoutedEventHandler(OnAnyContextMenuOpened));
        EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.ClosedEvent, new RoutedEventHandler(OnAnyContextMenuClosed));
    }

    private void OnAnyContextMenuOpened(object sender, RoutedEventArgs e)
    {
        _isContextMenuOpen = true;
    }

    private void OnAnyContextMenuClosed(object sender, RoutedEventArgs e)
    {
        _isContextMenuOpen = false;

        // When context menu closes, check if unpinned and mouse is outside window bounds
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_isPinned && !_isRolledUp && !IsMouseActuallyOverWindow(8))
            {
                ToggleRollUp();
            }
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private bool IsMouseActuallyOverWindow(int marginBuffer = 8)
    {
        if (!IsVisible) return false;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return false;

        if (NativeMethods.GetCursorPos(out var pt) && NativeMethods.GetWindowRect(handle, out var rect))
        {
            return pt.X >= (rect.Left - marginBuffer) &&
                   pt.X <= (rect.Right + marginBuffer) &&
                   pt.Y >= (rect.Top - marginBuffer) &&
                   pt.Y <= (rect.Bottom + marginBuffer);
        }
        return false;
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);

        if (DataContext is not ViewModels.MainViewModel vm) return;

        bool isInsideTextBox = IsClickInsideTextBox(e.OriginalSource as DependencyObject);

        // 1. Commit Fence Title rename if clicking outside the rename textbox
        if (vm.IsRenamingTitle && !isInsideTextBox)
        {
            vm.CommitRenameFenceTitle();
        }

        // 2. Commit Tab rename if clicking outside the tab's rename textbox
        var editingTab = vm.Tabs.FirstOrDefault(t => t.IsEditing);
        if (editingTab != null && !isInsideTextBox)
        {
            vm.CommitRenameTab(editingTab);
        }

        // 3. Commit Item rename if clicking outside the item's rename textbox
        var editingItem = vm.CurrentItems.FirstOrDefault(i => i.IsEditing);
        if (editingItem != null && !isInsideTextBox)
        {
            vm.CommitRenameItem(editingItem);
        }
    }

    private static bool IsClickInsideTextBox(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is TextBox) return true;
            if (source is Visual || source is System.Windows.Media.Media3D.Visual3D)
            {
                source = VisualTreeHelper.GetParent(source);
            }
            else
            {
                source = LogicalTreeHelper.GetParent(source);
            }
        }
        return false;
    }

    protected override void OnPreviewMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseRightButtonDown(e);
        // Ensure fence stays open when user initiates right-click gesture
        _isContextMenuOpen = true;
    }

    protected override void OnPreviewTextInput(TextCompositionEventArgs e)
    {
        base.OnPreviewTextInput(e);

        if (DataContext is not ViewModels.MainViewModel vm) return;
        if (e.OriginalSource is TextBox) return;

        // Type-to-Filter: Any printable character typed while window is active appends to search
        if (!string.IsNullOrEmpty(e.Text) && !char.IsControl(e.Text[0]))
        {
            if (e.Text == " ") return; // Handled directly in PreviewKeyDown to prevent button activation
            vm.SearchQuery += e.Text;
            e.Handled = true;
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (DataContext is not ViewModels.MainViewModel vm) return;

        // If user is editing a text box (like inline rename or search), do not steal keys
        if (e.OriginalSource is TextBox) return;

        // 1. Arrow Navigation across Item Cards in Active Tab
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            if (!vm.IsSearching && vm.CurrentItems.Count > 0)
            {
                double cardWidth = vm.CardWidth + 14;
                double canvasWidth = Math.Max(cardWidth, ActualWidth - 32);
                int columns = Math.Max(1, (int)(canvasWidth / cardWidth));

                vm.NavigateItems(e.Key, columns);
                e.Handled = true;
                return;
            }
        }
        // 2. F2: Inline Rename selected Item, Fence Title, or active Tab
        else if (e.Key == Key.F2)
        {
            if (TitleBar.IsMouseOver || TitleContainerBorder.IsMouseOver)
            {
                vm.StartRenameFenceTitle();
                e.Handled = true;
                return;
            }

            var selectedItem = vm.CurrentItems.FirstOrDefault(i => i.IsSelected);
            if (selectedItem != null)
            {
                vm.StartRenameItem(selectedItem);
                e.Handled = true;
                return;
            }

            if (vm.SelectedTab != null)
            {
                vm.StartRenameTab(vm.SelectedTab);
                e.Handled = true;
                return;
            }
        }
        // 3. Escape: Clear search query or deselect items
        else if (e.Key == Key.Escape)
        {
            if (vm.IsSearching)
            {
                vm.ClearSearch();
                e.Handled = true;
                return;
            }
            else
            {
                vm.DeselectAll();
                e.Handled = true;
                return;
            }
        }
        // 4. Enter: Launch best-matching application (if searching) or currently selected item
        else if (e.Key == Key.Enter)
        {
            if (vm.IsSearching)
            {
                vm.LaunchBestMatch();
                e.Handled = true;
                return;
            }
            else
            {
                var selectedItem = vm.CurrentItems.FirstOrDefault(i => i.IsSelected);
                if (selectedItem != null)
                {
                    vm.LaunchItemCommand.Execute(selectedItem);
                    e.Handled = true;
                    return;
                }
            }
        }
        // 5. Delete: Remove selected item from current tab
        else if (e.Key == Key.Delete)
        {
            if (!vm.IsSearching)
            {
                var selectedItem = vm.CurrentItems.FirstOrDefault(i => i.IsSelected);
                if (selectedItem != null)
                {
                    vm.RemoveItemCommand.Execute(selectedItem);
                    e.Handled = true;
                    return;
                }
            }
        }
        // 6. Backspace: Delete character from search query
        else if (e.Key == Key.Back)
        {
            if (vm.IsSearching && vm.SearchQuery.Length > 0)
            {
                vm.SearchQuery = vm.SearchQuery.Substring(0, vm.SearchQuery.Length - 1);
                e.Handled = true;
                return;
            }
        }
        // 7. Space: Support space in search query
        else if (e.Key == Key.Space)
        {
            if (vm.IsSearching)
            {
                vm.SearchQuery += " ";
                e.Handled = true;
                return;
            }
        }
    }

    private void RenameTabMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi)
        {
            var cm = mi.Parent as ContextMenu;
            if (cm?.PlacementTarget is FrameworkElement fe && fe.DataContext is Models.TabModel tab)
            {
                if (DataContext is ViewModels.MainViewModel vm)
                {
                    vm.StartRenameTab(tab);
                }
            }
        }
    }

    private void ChangeTabIconMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is string icon)
        {
            var parentMenu = mi.Parent as MenuItem;
            var cm = parentMenu?.Parent as ContextMenu ?? mi.Parent as ContextMenu;
            if (cm?.PlacementTarget is FrameworkElement fe && fe.DataContext is Models.TabModel tab)
            {
                if (DataContext is ViewModels.MainViewModel vm)
                {
                    vm.SetTabIcon(tab, icon);
                }
            }
        }
    }

    private void TabButton_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is Models.TabModel tab)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.StartRenameTab(tab);
            }
            e.Handled = true;
        }
    }

    private void RenameTextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.Visibility == Visibility.Visible)
        {
            tb.Dispatcher.BeginInvoke(new Action(() =>
            {
                tb.Focus();
                tb.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void RenameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is Models.TabModel tab)
        {
            if (e.Key == Key.Enter)
            {
                if (DataContext is ViewModels.MainViewModel vm)
                {
                    tab.Name = tb.Text;
                    vm.CommitRenameTab(tab);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                tab.IsEditing = false;
                e.Handled = true;
            }
        }
    }

    private void RenameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is Models.TabModel tab)
        {
            if (tab.IsEditing && DataContext is ViewModels.MainViewModel vm)
            {
                tab.Name = tb.Text;
                vm.CommitRenameTab(tab);
            }
        }
    }

    private void SetPinned(bool pinned)
    {
        _isPinned = pinned;
        if (PinIcon != null) PinIcon.Text = _isPinned ? "📌" : "📍";
        if (PinButton != null)
        {
            PinButton.ToolTip = _isPinned ? "Pinned (Stays Open)" : "Unpinned (Auto-Rollup on Exit)";
            PinButton.Opacity = _isPinned ? 1.0 : 0.6;
        }
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        SetPinned(!_isPinned);
        if (_isPinned && _isRolledUp)
        {
            ToggleRollUp();
        }
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);

        // Cancel any pending auto-collapse timer immediately when mouse enters
        _mouseLeaveTimer.Stop();

        // Focus window on hover so keyboard typing directly triggers Type-to-Filter
        Focus();

        if (_isRolledUp)
        {
            if (!_isPinned && !_isManualRollUp)
            {
                // If unpinned and rolled up, automatically expand on hover
                ToggleRollUp();
            }
            else
            {
                // If pinned or manually rolled up, stay compact but reveal header buttons on hover
                AnimateHeaderControls(1.0);
            }
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);

        // Reset manual roll-up flag once mouse leaves the window
        _isManualRollUp = false;

        // If a context menu or popup is open, or mouse button held, DO NOT collapse!
        if (_isContextMenuOpen) return;
        if (SettingsButton?.ContextMenu?.IsOpen == true) return;
        if (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed) return;

        // Debounce & verify if mouse actually left physical window bounds (including resize borders)
        _mouseLeaveTimer.Stop();
        _mouseLeaveTimer.Start();
    }

    private void OnMouseLeaveTimerTick(object? sender, EventArgs e)
    {
        // If mouse is still on the window (e.g. hovering or dragging resize borders), do not collapse
        if (IsMouseActuallyOverWindow(marginBuffer: 8)) return;
        if (_isContextMenuOpen) return;
        if (SettingsButton?.ContextMenu?.IsOpen == true) return;
        if (Mouse.LeftButton == MouseButtonState.Pressed || Mouse.RightButton == MouseButtonState.Pressed) return;

        _mouseLeaveTimer.Stop();

        if (!_isPinned && !_isRolledUp)
        {
            // If unpinned and expanded, collapse now that mouse has truly exited
            ToggleRollUp();
        }
        else if (_isRolledUp)
        {
            // If rolled up, hide header buttons when mouse leaves
            AnimateHeaderControls(0.0);
        }

        // Notify memory optimizer that interaction with this fence has concluded
        MemoryOptimizerService.NotifyInteraction();
    }

    private void AnimateHeaderControls(double targetOpacity)
    {
        if (LeftHeaderControls == null || RightHeaderControls == null) return;
        var anim = new System.Windows.Media.Animation.DoubleAnimation
        {
            To = targetOpacity,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };
        LeftHeaderControls.BeginAnimation(OpacityProperty, anim);
        RightHeaderControls.BeginAnimation(OpacityProperty, anim);
    }

    private void StartWindowDirectDrag(MouseButtonEventArgs e)
    {
        _isDraggingWindow = true;
        NativeMethods.GetCursorPos(out _dragStartCursorScreen);
        _dragStartWindowPos = new Point(Left, Top);
        TitleBar.CaptureMouse();
        e.Handled = true;
    }

    private void TitleBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingWindow && e.LeftButton == MouseButtonState.Pressed)
        {
            NativeMethods.GetCursorPos(out var currentCursorScreen);

            var source = PresentationSource.FromVisual(this);
            double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

            double dx = (currentCursorScreen.X - _dragStartCursorScreen.X) / dpiX;
            double dy = (currentCursorScreen.Y - _dragStartCursorScreen.Y) / dpiY;

            Left = _dragStartWindowPos.X + dx;
            Top = _dragStartWindowPos.Y + dy;
            e.Handled = true;
        }
    }

    private void TitleBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingWindow)
        {
            _isDraggingWindow = false;
            TitleBar.ReleaseMouseCapture();
            SaveWindowMetrics(immediate: true);
            e.Handled = true;
        }
    }

    private void TitleBar_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_isDraggingWindow)
        {
            _isDraggingWindow = false;
            SaveWindowMetrics(immediate: true);
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            _isDraggingWindow = false;
            TitleBar.ReleaseMouseCapture();

            // Double-click on TitleBar toggles Roll-Up and Pin states:
            // - If Rolled-Up (32px): Expands fence AND turns Pin ON (📌) so it stays open.
            // - If Open: Rolls up fence to 32px AND turns Pin OFF (📍) without immediate auto-reopen loop.
            if (_isRolledUp)
            {
                SetPinned(true);
                ToggleRollUp();
            }
            else
            {
                _isManualRollUp = true;
                SetPinned(false);
                ToggleRollUp();
                AnimateHeaderControls(1.0);
            }
            e.Handled = true;
        }
        else if (e.LeftButton == MouseButtonState.Pressed)
        {
            StartWindowDirectDrag(e);
        }
    }

    private void RollUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRolledUp)
        {
            SetPinned(true);
            ToggleRollUp();
        }
        else
        {
            _isManualRollUp = true;
            SetPinned(false);
            ToggleRollUp();
            AnimateHeaderControls(1.0);
        }
    }

    private void TabScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            sv.ScrollToHorizontalOffset(sv.HorizontalOffset - (e.Delta * 0.7));
            e.Handled = true;
        }
    }

    private void ToggleRollUp()
    {
        _isRolledUp = !_isRolledUp;

        // Temporarily disable scrollbar during roll-up/expand animation to eliminate glitchy flash
        if (ItemsScrollViewer != null)
        {
            ItemsScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        }

        // Smooth cubic easing curve (matching Windows 11 Fluent animations)
        var cubicEase = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
        var animDuration = TimeSpan.FromMilliseconds(350);

        if (_isRolledUp)
        {
            _previousHeight = ActualHeight >= 180 ? ActualHeight : 380;
            MinHeight = 32;
            RollUpIcon.Text = "🔽";

            // Fade out header action controls so only clean title remains
            AnimateHeaderControls(0.0);

            // 1. Fade out body content smoothly
            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = BodyContainer.Opacity,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = cubicEase
            };
            fadeOut.Completed += (s, e) =>
            {
                if (_isRolledUp)
                {
                    BodyContainer.Visibility = Visibility.Collapsed;
                    TitleSeparator.Visibility = Visibility.Collapsed;
                }
            };
            BodyContainer.BeginAnimation(OpacityProperty, fadeOut);

            // 2. Animate height collapsing from bottom up to 32px
            var heightAnim = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = ActualHeight,
                To = 32,
                Duration = animDuration,
                EasingFunction = cubicEase
            };
            heightAnim.Completed += (s, e) =>
            {
                if (_isRolledUp)
                {
                    BeginAnimation(HeightProperty, null);
                    Height = 32;
                }
            };
            BeginAnimation(HeightProperty, heightAnim);
        }
        else
        {
            RollUpIcon.Text = "🔼";
            TitleSeparator.Visibility = Visibility.Visible;
            BodyContainer.Visibility = Visibility.Visible;

            // Restore header action controls when fence is opened
            AnimateHeaderControls(1.0);

            double targetHeight = _previousHeight >= 180 ? _previousHeight : 380;

            // 1. Animate height expanding downwards
            var heightAnim = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = ActualHeight,
                To = targetHeight,
                Duration = animDuration,
                EasingFunction = cubicEase
            };
            heightAnim.Completed += (s, e) =>
            {
                if (!_isRolledUp)
                {
                    MinHeight = 180;
                    BeginAnimation(HeightProperty, null);
                    Height = targetHeight;
                    // Restore vertical scrollbar visibility cleanly once fully expanded
                    if (ItemsScrollViewer != null)
                    {
                        ItemsScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                    }
                }
            };
            BeginAnimation(HeightProperty, heightAnim);

            // 2. Fade in body content smoothly
            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = animDuration,
                EasingFunction = cubicEase
            };
            BodyContainer.BeginAnimation(OpacityProperty, fadeIn);
        }
    }

    private void TitleTextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            _isDraggingWindow = false;
            TitleBar.ReleaseMouseCapture();

            // Double click: Rename Fence Title (Does not roll up!)
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.StartRenameFenceTitle();
            }
            e.Handled = true;
        }
        else if (e.LeftButton == MouseButtonState.Pressed)
        {
            StartWindowDirectDrag(e);
        }
    }

    private void RenameFenceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm)
        {
            vm.StartRenameFenceTitle();
        }
    }

    private void TitleRenameTextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.Visibility == Visibility.Visible)
        {
            tb.Dispatcher.BeginInvoke(new Action(() =>
            {
                tb.Focus();
                tb.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void TitleRenameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.CommitRenameFenceTitle();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.IsRenamingTitle = false;
            }
            e.Handled = true;
        }
    }

    private void TitleRenameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm && vm.IsRenamingTitle)
        {
            vm.CommitRenameFenceTitle();
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _isContextMenuOpen = true;
        if (_isRolledUp)
        {
            ToggleRollUp();
        }

        ContextMenu? menu = null;
        if (sender is Button btn && btn.ContextMenu != null)
        {
            menu = btn.ContextMenu;
            menu.PlacementTarget = btn;
        }
        else if (SettingsButton?.ContextMenu != null)
        {
            menu = SettingsButton.ContextMenu;
            menu.PlacementTarget = SettingsButton;
        }

        if (menu != null)
        {
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    private void AcrylicToggle_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm)
        {
            vm.Config.EnableAcrylic = !vm.Config.EnableAcrylic;
            vm.SaveConfig();
            _dwmService.ApplyBackdrop(this, vm.Config.EnableAcrylic);
        }
    }

    private void DesktopPinToggle_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm)
        {
            vm.Config.AttachToDesktop = !vm.Config.AttachToDesktop;
            vm.SaveConfig();

            if (vm.Config.AttachToDesktop)
            {
                _desktopManager.AttachToDesktop(this);
            }
            else
            {
                _desktopManager.DetachFromDesktop(this);
            }
        }
    }

    public void AttachToDesktopMode()
    {
        _desktopManager.AttachToDesktop(this);
    }

    public void DetachFromDesktopMode()
    {
        _desktopManager.DetachFromDesktop(this);
    }

    public void ApplyBackdropSetting()
    {
        if (DataContext is ViewModels.MainViewModel vm)
        {
            _dwmService.ApplyBackdrop(this, vm.Config.EnableAcrylic);
        }
    }

    public void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm)
        {
            foreach (Window win in Application.Current.Windows)
            {
                if (win is Views.SettingsWindow settingsWin)
                {
                    settingsWin.Activate();
                    if (settingsWin.WindowState == WindowState.Minimized)
                        settingsWin.WindowState = WindowState.Normal;
                    return;
                }
            }

            var newSettings = new Views.SettingsWindow
            {
                DataContext = vm
            };
            newSettings.Show();
        }
    }

    private IntPtr WndProcSettingsHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_wmOpenSettings != 0 && (uint)msg == _wmOpenSettings)
        {
            Dispatcher.Invoke(() => OpenSettings_Click(this, new RoutedEventArgs()));
            handled = true;
        }
        return IntPtr.Zero;
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        if (_isLoaded && !IsDeleting)
        {
            SaveWindowMetrics(immediate: false);
        }
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (_isLoaded && !IsDeleting && !_isRolledUp)
        {
            SaveWindowMetrics(immediate: false);
        }
    }

    private void SaveWindowMetrics(bool immediate = false)
    {
        if (!_isLoaded || _isRolledUp || IsDeleting) return;

        if (DataContext is ViewModels.MainViewModel vm && vm.CurrentFence != null)
        {
            if (!double.IsNaN(Left) && !double.IsInfinity(Left)) vm.CurrentFence.X = Left;
            if (!double.IsNaN(Top) && !double.IsInfinity(Top)) vm.CurrentFence.Y = Top;
            if (ActualWidth >= 180) vm.CurrentFence.Width = ActualWidth;
            if (ActualHeight >= 120) vm.CurrentFence.Height = ActualHeight;

            if (immediate)
            {
                _saveMetricsDebounceTimer?.Stop();
                vm.SaveConfig();
            }
            else
            {
                _saveMetricsDebounceTimer?.Stop();
                _saveMetricsDebounceTimer?.Start();
            }
        }
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm)
        {
            vm.DeselectAllCommand.Execute(null);
        }
    }

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        AeroFence.Services.Native.DesktopSelectionService.CancelCurrentSelectionDrag();
        AeroFence.Services.Native.DesktopSelectionService.IsItemDragActive = true;
        HandleWindowDrag(e);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        AeroFence.Services.Native.DesktopSelectionService.CancelCurrentSelectionDrag();
        AeroFence.Services.Native.DesktopSelectionService.IsItemDragActive = true;
        HandleWindowDrag(e);
    }

    private void Window_DragLeave(object sender, DragEventArgs e)
    {
        AeroFence.Services.Native.DesktopSelectionService.CancelCurrentSelectionDrag();
        System.Threading.Tasks.Task.Delay(350).ContinueWith(_ =>
        {
            AeroFence.Services.Native.DesktopSelectionService.IsItemDragActive = false;
        });
    }

    private void TitleBar_DragEnter(object sender, DragEventArgs e)
    {
        AeroFence.Services.Native.DesktopSelectionService.CancelCurrentSelectionDrag();
        AeroFence.Services.Native.DesktopSelectionService.IsItemDragActive = true;
        HandleWindowDrag(e);
    }

    private void TitleBar_DragOver(object sender, DragEventArgs e)
    {
        AeroFence.Services.Native.DesktopSelectionService.CancelCurrentSelectionDrag();
        AeroFence.Services.Native.DesktopSelectionService.IsItemDragActive = true;
        HandleWindowDrag(e);
    }

    private void HandleWindowDrag(DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent("AeroFence_Item"))
        {
            e.Effects = e.Data.GetDataPresent("AeroFence_Item") ? DragDropEffects.Move : DragDropEffects.Copy;
            e.Handled = true;

            // Fences behavior: if user drags items over collapsed bar, automatically expand!
            if (_isRolledUp)
            {
                ToggleRollUp();
            }
        }
    }

    private void TitleBar_Drop(object sender, DragEventArgs e)
    {
        AeroFence.Services.Native.DesktopSelectionService.CancelCurrentSelectionDrag();
        AeroFence.Services.Native.DesktopSelectionService.IsItemDragActive = true;
        System.Threading.Tasks.Task.Delay(350).ContinueWith(_ =>
        {
            AeroFence.Services.Native.DesktopSelectionService.IsItemDragActive = false;
        });

        if (DataContext is not ViewModels.MainViewModel vm) return;

        if (_isRolledUp)
        {
            ToggleRollUp();
        }

        if (e.Data.GetDataPresent("AeroFence_Item"))
        {
            var draggedItem = e.Data.GetData("AeroFence_Item") as Models.ItemModel;
            if (draggedItem != null)
            {
                draggedItem.WasDroppedInternally = true;
                e.Handled = true;
            }
        }
        else if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                vm.AddItemsFromPaths(files);
                e.Handled = true;
            }
        }
    }

    private void TabButton_DragEnter(object sender, DragEventArgs e)
    {
        AeroFence.Services.Native.DesktopSelectionService.CancelCurrentSelectionDrag();
        AeroFence.Services.Native.DesktopSelectionService.IsItemDragActive = true;

        if (DataContext is not ViewModels.MainViewModel vm) return;
        var targetTab = (sender as FrameworkElement)?.DataContext as Models.TabModel;
        if (targetTab != null && vm.SelectedTab != targetTab)
        {
            // Auto switch tab when dragging items over tab headers
            vm.SelectTab(targetTab);
        }
    }

    private void TabButton_DragOver(object sender, DragEventArgs e)
    {
        AeroFence.Services.Native.DesktopSelectionService.CancelCurrentSelectionDrag();
        AeroFence.Services.Native.DesktopSelectionService.IsItemDragActive = true;

        if (e.Data.GetDataPresent("AeroFence_Item"))
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }
        else if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void TabButton_Drop(object sender, DragEventArgs e)
    {
        AeroFence.Services.Native.DesktopSelectionService.CancelCurrentSelectionDrag();
        AeroFence.Services.Native.DesktopSelectionService.IsItemDragActive = true;
        System.Threading.Tasks.Task.Delay(350).ContinueWith(_ =>
        {
            AeroFence.Services.Native.DesktopSelectionService.IsItemDragActive = false;
        });

        if (DataContext is not ViewModels.MainViewModel vm) return;
        var targetTab = (sender as FrameworkElement)?.DataContext as Models.TabModel;
        if (targetTab == null) return;

        // 1. Internal item transferred to tab
        if (e.Data.GetDataPresent("AeroFence_Item"))
        {
            var draggedItem = e.Data.GetData("AeroFence_Item") as Models.ItemModel;
            if (draggedItem != null)
            {
                draggedItem.WasDroppedInternally = true;
                vm.MoveItemToTab(draggedItem, targetTab);
                e.Handled = true;
            }
        }
        // 2. External files dropped directly onto a tab button
        else if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                vm.AddItemsFromPaths(files, targetTab);
                e.Handled = true;
            }
        }
    }

    private void Canvas_DragEnter(object sender, DragEventArgs e)
    {
        AeroFence.Services.Native.DesktopSelectionService.CancelCurrentSelectionDrag();
        AeroFence.Services.Native.DesktopSelectionService.IsItemDragActive = true;

        if (e.Data.GetDataPresent("AeroFence_Item"))
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }
        else if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void Canvas_DragOver(object sender, DragEventArgs e)
    {
        AeroFence.Services.Native.DesktopSelectionService.CancelCurrentSelectionDrag();
        AeroFence.Services.Native.DesktopSelectionService.IsItemDragActive = true;

        if (e.Data.GetDataPresent("AeroFence_Item"))
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }
        else if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void Canvas_DragLeave(object sender, DragEventArgs e)
    {
        AeroFence.Services.Native.DesktopSelectionService.CancelCurrentSelectionDrag();
        System.Threading.Tasks.Task.Delay(350).ContinueWith(_ =>
        {
            AeroFence.Services.Native.DesktopSelectionService.IsItemDragActive = false;
        });
    }

    private void Canvas_Drop(object sender, DragEventArgs e)
    {
        AeroFence.Services.Native.DesktopSelectionService.CancelCurrentSelectionDrag();
        AeroFence.Services.Native.DesktopSelectionService.IsItemDragActive = true;
        System.Threading.Tasks.Task.Delay(350).ContinueWith(_ =>
        {
            AeroFence.Services.Native.DesktopSelectionService.IsItemDragActive = false;
        });

        if (DataContext is not ViewModels.MainViewModel vm) return;

        // 1. Internal item reordered to the end of the canvas
        if (e.Data.GetDataPresent("AeroFence_Item"))
        {
            var draggedItem = e.Data.GetData("AeroFence_Item") as Models.ItemModel;
            if (draggedItem != null)
            {
                draggedItem.WasDroppedInternally = true;
                vm.MoveItemToEnd(draggedItem);
                e.Handled = true;
            }
        }
        // 2. External files dropped from Windows Explorer / Desktop
        else if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                vm.AddItemsFromPaths(files);
                e.Handled = true;
            }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm)
        {
            vm.DeleteFenceCommand.Execute(null);
        }
        else
        {
            Close();
        }
    }
}