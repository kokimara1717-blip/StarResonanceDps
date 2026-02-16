using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;

namespace StarResonanceDpsAnalysis.WPF.Behaviors;

public class OpenContextMenuBehavior : Behavior<Button>
{
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.Click += OnButtonClick;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.Click -= OnButtonClick;

        if (AssociatedObject.ContextMenu == null) return;
        AssociatedObject.ContextMenu.PreviewKeyDown -= OnContextMenuPreviewKeyDown;
        AssociatedObject.ContextMenu.Closed -= OnContextMenuClosed;
    }

    private void OnButtonClick(object sender, RoutedEventArgs e)
    {
        var contextMenu = AssociatedObject.ContextMenu;
        if (contextMenu == null) return;
        e.Handled = true;

        contextMenu.PreviewKeyDown -= OnContextMenuPreviewKeyDown;
        contextMenu.PreviewKeyDown += OnContextMenuPreviewKeyDown;

        contextMenu.Closed -= OnContextMenuClosed;
        contextMenu.Closed += OnContextMenuClosed;

        // Open context menu via dispatcher to handle Alt key interference.
        // When Alt is held down, the menu system might close the popup immediately if opened synchronously.
        // A delay ensures the menu opens and stays open after the Alt key event is processed.
        AssociatedObject.Dispatcher.BeginInvoke((Action)(() =>
        {
            contextMenu.PlacementTarget = AssociatedObject;
            contextMenu.IsOpen = true;
        }));
    }

    private static void OnContextMenuClosed(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu contextMenu) return;
        contextMenu.PreviewKeyDown -= OnContextMenuPreviewKeyDown;
        contextMenu.Closed -= OnContextMenuClosed;
    }

    private static void OnContextMenuPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.System && e.SystemKey != Key.LeftAlt && e.SystemKey != Key.RightAlt) return;
        e.Handled = true;
    }
}