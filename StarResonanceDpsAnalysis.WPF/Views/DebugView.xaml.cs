using System.Windows;
using StarResonanceDpsAnalysis.WPF.ViewModels;

namespace StarResonanceDpsAnalysis.WPF.Views;

/// <summary>
/// Interaction logic for DebugView.xaml
/// </summary>
public partial class DebugView : Window
{
    public DebugView(DebugFunctions debugFunctions)
    {
        InitializeComponent();
        DataContext = debugFunctions;
    }
}