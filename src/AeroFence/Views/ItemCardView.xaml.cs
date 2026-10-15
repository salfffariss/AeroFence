using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AeroFence.Models;
using AeroFence.ViewModels;

namespace AeroFence.Views;

/// <summary>
/// Interaction logic for ItemCardView.xaml.
/// Encapsulates visual selection, execution triggers, context menu, and internal Drag & Drop.
/// </summary>
public partial class ItemCardView : UserControl
{
    private Point _dragStartPoint;
    private bool _isPotentialDrag = false;

    public ItemCardView()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => (Window.GetWindow(this) as MainWindow)?.DataContext as MainViewModel 
                                        ?? Application.Current.MainWindow?.DataContext as MainViewModel;

    private void CardBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // When item name is being edited, let clicks pass directly to the TextBox
        if (DataContext is ItemModel editingItem && editingItem.IsEditing)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            // Double click: Launch target application/game immediately
            _isPotentialDrag = false;
            if (DataContext is ItemModel item)
            {
                ViewModel?.LaunchItemCommand.Execute(item);
            }
            e.Handled = true;
            return;
        }

        Focus();

        // Record start point for potential drag
        _dragStartPoint = e.GetPosition(this);
        _isPotentialDrag = true;
        e.Handled = true;
    }

    private void CardBorder_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is ItemModel editingItem && editingItem.IsEditing)
        {
            _isPotentialDrag = false;
            return;
        }

        if (!_isPotentialDrag || e.LeftButton != MouseButtonState.Pressed)
        {
            _isPotentialDrag = false;
            return;
        }

        Point currentPoint = e.GetPosition(this);
        Vector diff = _dragStartPoint - currentPoint;

        // Verify if drag exceeded Windows standard drag threshold
        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            _isPotentialDrag = false;

            if (DataContext is ItemModel item)
            {
                // Reset internal drop tracking flag
                item.WasDroppedInternally = false;
                bool isDragCancelled = false;

                // Semi-transparent ghost effect while dragging
                Opacity = 0.45;

                var data = new DataObject();
                data.SetData("AeroFence_Item", item);

                string filePath = item.TargetPath;
                if (!string.IsNullOrEmpty(filePath) && (System.IO.File.Exists(filePath) || System.IO.Directory.Exists(filePath)))
                {
                    var fileList = new System.Collections.Specialized.StringCollection { filePath };
                    data.SetFileDropList(fileList);
                }

                QueryContinueDragEventHandler queryHandler = (s, qe) =>
                {
                    if (qe.EscapePressed)
                    {
                        isDragCancelled = true;
                    }
                };

                this.QueryContinueDrag += queryHandler;
                DragDropEffects dropResult = DragDropEffects.None;
                try
                {
                    AeroFence.Services.Native.DesktopSelectionService.CancelCurrentSelectionDrag();
                    AeroFence.Services.Native.DesktopSelectionService.IsItemDragActive = true;
                    dropResult = DragDrop.DoDragDrop(this, data, DragDropEffects.Copy | DragDropEffects.Move);
                }
                finally
                {
                    this.QueryContinueDrag -= queryHandler;
                    // Restore opacity after drop finishes
                    Opacity = 1.0;
                    System.Threading.Tasks.Task.Delay(350).ContinueWith(_ =>
                    {
                        AeroFence.Services.Native.DesktopSelectionService.IsItemDragActive = false;
                    });
                }

                // If not cancelled with Escape, not dropped into any AeroFence tab/card, and accepted by external target (Desktop/Explorer):
                if (!isDragCancelled && !item.WasDroppedInternally && dropResult != DragDropEffects.None)
                {
                    // User dragged item OUT of the fence onto Desktop or Explorer:
                    // Return it to the Desktop and remove from fence!
                    ViewModel?.RemoveItemCommand.Execute(item);
                }
            }
        }
    }

    private void CardBorder_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPotentialDrag)
        {
            _isPotentialDrag = false;
            // Normal click without drag: select this item (supports Ctrl multi-select)
            if (DataContext is ItemModel item)
            {
                ViewModel?.SelectItemCommand.Execute(item);
            }
        }
    }

    private void CardBorder_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        Focus();
        if (DataContext is ItemModel item && !item.IsSelected)
        {
            // Automatically select item on right-click like Windows desktop
            ViewModel?.SelectItemCommand.Execute(item);
        }
    }

    private void CardBorder_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("AeroFence_Item"))
        {
            var draggedItem = e.Data.GetData("AeroFence_Item") as ItemModel;
            if (draggedItem != null && draggedItem != DataContext)
            {
                DropIndicator.Visibility = Visibility.Visible;
            }
        }
    }

    private void CardBorder_DragLeave(object sender, DragEventArgs e)
    {
        DropIndicator.Visibility = Visibility.Collapsed;
    }

    private void CardBorder_DragOver(object sender, DragEventArgs e)
    {
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

    private void CardBorder_Drop(object sender, DragEventArgs e)
    {
        DropIndicator.Visibility = Visibility.Collapsed;

        // 1. Internal item reordering
        if (e.Data.GetDataPresent("AeroFence_Item"))
        {
            var draggedItem = e.Data.GetData("AeroFence_Item") as ItemModel;
            var targetItem = DataContext as ItemModel;

            if (draggedItem != null)
            {
                draggedItem.WasDroppedInternally = true;
                if (targetItem != null && draggedItem != targetItem)
                {
                    ViewModel?.ReorderItems(draggedItem, targetItem);
                }
                e.Handled = true;
            }
        }
        // 2. External files dropped from Explorer / Desktop
        else if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                ViewModel?.AddItemsFromPaths(files);
                e.Handled = true;
            }
        }
    }

    private void OpenMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ItemModel item)
        {
            ViewModel?.LaunchItemCommand.Execute(item);
        }
    }

    private void OpenLocationMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ItemModel item)
        {
            ViewModel?.OpenItemLocationCommand.Execute(item);
        }
    }

    private void OpenInTerminalMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ItemModel item)
        {
            ViewModel?.OpenInTerminalCommand.Execute(item);
        }
    }

    private void RenameMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ItemModel item)
        {
            ViewModel?.StartRenameItem(item);
        }
    }

    private void ItemRenameTextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
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

    private void ItemRenameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is ItemModel item)
        {
            if (e.Key == Key.Enter)
            {
                ViewModel?.CommitRenameItem(item);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                item.IsEditing = false;
                e.Handled = true;
            }
        }
    }

    private void ItemRenameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is ItemModel item && item.IsEditing)
        {
            ViewModel?.CommitRenameItem(item);
        }
    }

    private void RemoveMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ItemModel item)
        {
            ViewModel?.RemoveItemCommand.Execute(item);
        }
    }
}
