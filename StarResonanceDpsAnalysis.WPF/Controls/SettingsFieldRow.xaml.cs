using System.Windows;
using System.Windows.Controls;

namespace StarResonanceDpsAnalysis.WPF.Controls;

public partial class SettingsFieldRow : UserControl
{
    public SettingsFieldRow()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label),
        typeof(string),
        typeof(SettingsFieldRow),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty LabelToolTipProperty = DependencyProperty.Register(
        nameof(LabelToolTip),
        typeof(object),
        typeof(SettingsFieldRow),
        new PropertyMetadata(null));

    public static readonly DependencyProperty FieldContentProperty = DependencyProperty.Register(
        nameof(FieldContent),
        typeof(object),
        typeof(SettingsFieldRow),
        new PropertyMetadata(null));

    public static readonly DependencyProperty IsLastProperty = DependencyProperty.Register(
        nameof(IsLast),
        typeof(bool),
        typeof(SettingsFieldRow),
        new PropertyMetadata(false));

    public static readonly DependencyProperty LabelVerticalAlignmentProperty = DependencyProperty.Register(
        nameof(LabelVerticalAlignment),
        typeof(VerticalAlignment),
        typeof(SettingsFieldRow),
        new PropertyMetadata(VerticalAlignment.Center));

    public static readonly DependencyProperty LabelMarginProperty = DependencyProperty.Register(
        nameof(LabelMargin),
        typeof(Thickness),
        typeof(SettingsFieldRow),
        new PropertyMetadata(default(Thickness)));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public object? LabelToolTip
    {
        get => GetValue(LabelToolTipProperty);
        set => SetValue(LabelToolTipProperty, value);
    }

    public object? FieldContent
    {
        get => GetValue(FieldContentProperty);
        set => SetValue(FieldContentProperty, value);
    }

    public bool IsLast
    {
        get => (bool)GetValue(IsLastProperty);
        set => SetValue(IsLastProperty, value);
    }

    public VerticalAlignment LabelVerticalAlignment
    {
        get => (VerticalAlignment)GetValue(LabelVerticalAlignmentProperty);
        set => SetValue(LabelVerticalAlignmentProperty, value);
    }

    public Thickness LabelMargin
    {
        get => (Thickness)GetValue(LabelMarginProperty);
        set => SetValue(LabelMarginProperty, value);
    }
}
