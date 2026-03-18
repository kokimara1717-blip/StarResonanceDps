using System.Windows;
using System.Windows.Input;
using StarResonanceDpsAnalysis.WPF.ViewModels;

namespace StarResonanceDpsAnalysis.WPF.Views;

/// <summary>
/// Interaction logic for BuffMonitorView.xaml
/// </summary>
public partial class BuffMonitorView : Window
{
    public BuffMonitorView(BuffMonitorViewModel vm)
    {
        InitializeComponent();
        ViewModel = vm;
        DataContext = ViewModel;
    }

    public BuffMonitorViewModel ViewModel { get; }

    /// <summary>
    /// Handle title bar mouse down - allows dragging the window
    /// </summary>
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    /// <summary>
    /// Handle minimize button click
    /// </summary>
    private void ButtonMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }
}