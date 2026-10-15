using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using AeroFence.Services.Native;
using AeroFence.ViewModels;

namespace AeroFence.Views;

/// <summary>
/// Interaction logic for SettingsWindow.xaml.
/// Dashboard for global preferences, startup, multi-fences, and Chameleon mode.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly DwmService _dwmService = new();

    public SettingsWindow()
    {
        InitializeComponent();

        SourceInitialized += (s, e) =>
        {
            _dwmService.ApplyBackdrop(this, enableAcrylic: true);
        };

        Closed += (s, e) =>
        {
            MemoryOptimizerService.TrimWorkingSet();
        };
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void StartWithWindows_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            bool isChecked = ViewModel.Config.StartWithWindows;
            StartupService.SetStartup(isChecked);
            ViewModel.SaveConfig();
        }
    }

    private void AttachToDesktop_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.SaveConfig();
            if (Application.Current.MainWindow is MainWindow mainWin)
            {
                if (ViewModel.Config.AttachToDesktop)
                {
                    mainWin.AttachToDesktopMode();
                }
                else
                {
                    mainWin.DetachFromDesktopMode();
                }
            }
        }
    }

    private void AcrylicToggle_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.SaveConfig();
            if (Application.Current.MainWindow is MainWindow mainWin)
            {
                mainWin.ApplyBackdropSetting();
            }
        }
    }

    private void ChameleonToggle_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.SaveConfig();
    }

    private void ChameleonOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        ViewModel?.SaveConfig();
    }

    private void CreateFence_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            var newFence = ViewModel.CreateNewFence(500, 250, 420, 320);
            RefreshFencesList();
            MessageBox.Show($"New fence '{newFence.Title}' created on your desktop!", "AeroFence", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void CreateFolderPortal_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Folder for AeroFence Portal"
            };

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
            {
                var newPortal = ViewModel.CreateNewFolderPortal(450, 200, 460, 320, dialog.FolderName);
                RefreshFencesList();
                MessageBox.Show($"New Folder Portal '{newPortal.Title}' linked to '{dialog.FolderName}' created on your desktop!", "AeroFence", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private void FocusFence_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is Models.FenceModel fence)
        {
            foreach (Window win in Application.Current.Windows)
            {
                if (win is MainWindow mainWin && (mainWin.DataContext as MainViewModel)?.CurrentFence?.Id == fence.Id)
                {
                    mainWin.Activate();
                    if (!mainWin.IsVisible) mainWin.Show();
                    return;
                }
            }
        }
    }

    private void DeleteFence_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is Models.FenceModel fence)
        {
            var result = MessageBox.Show(
                $"Are you sure you want to delete fence '{fence.Title}'?\n\nNote: Your files and shortcuts will NOT be deleted from your computer.",
                "Delete AeroFence",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                App.DeleteFenceById(fence.Id);
                RefreshFencesList();
            }
        }
    }

    private void RefreshFencesList()
    {
        var config = new Services.Storage.JsonStorageService().LoadConfig();
        if (ViewModel != null)
        {
            ViewModel.Config = config;
            FencesListItemsControl.ItemsSource = null;
            FencesListItemsControl.ItemsSource = ViewModel.Config.Fences;
        }
    }
}
