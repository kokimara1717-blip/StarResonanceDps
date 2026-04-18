using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace StarResonanceDpsAnalysis.WPF.Controls;

/// <summary>
/// 专业的HSV颜色选择器控件
/// </summary>
public class SimpleColorPicker : Control
{
    static SimpleColorPicker()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SimpleColorPicker), 
            new FrameworkPropertyMetadata(typeof(SimpleColorPicker)));
    }

    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(nameof(SelectedColor), typeof(Color), typeof(SimpleColorPicker),
            new FrameworkPropertyMetadata(Colors.Gray, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorChanged));

    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SimpleColorPicker picker && e.NewValue is Color color && !picker._isUpdatingFromControls)
        {
            picker.UpdateFromColor(color);
        }
    }

    private Grid? _svCanvas;  // Changed from Canvas to Grid
    private Ellipse? _svThumb;
    private Rectangle? _hueBar;
    private Border? _hueThumb;
    private Rectangle? _svBackground; // New: for updating hue
    private bool _isUpdatingFromControls;
    private bool _isDraggingSV;
    private bool _isDraggingHue;

    // HSV values
    private double _hue = 0;        // 0-360
    private double _saturation = 0.5; // 0-1
    private double _value = 0.7;      // 0-1

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // 取消订阅旧控件
        UnsubscribeEvents();

        // 获取模板元素
        _svBackground = GetTemplateChild("PART_SV_Canvas") as Rectangle;
        _svCanvas = _svBackground?.Parent as Grid;
        var svThumbCanvas = _svCanvas?.Children.OfType<Canvas>().FirstOrDefault();
        _svThumb = svThumbCanvas?.Children.OfType<Ellipse>().FirstOrDefault();
        _hueBar = GetTemplateChild("PART_Hue_Bar") as Rectangle;
        _hueThumb = GetTemplateChild("PART_Hue_Thumb") as Border;

        // 订阅事件
        SubscribeEvents();

        // 初始化颜色
        UpdateFromColor(SelectedColor);
    }

    private void UnsubscribeEvents()
    {
        if (_svCanvas != null)
        {
            _svCanvas.MouseLeftButtonDown -= OnSVMouseDown;
            _svCanvas.MouseMove -= OnSVMouseMove;
            _svCanvas.MouseLeftButtonUp -= OnSVMouseUp;
        }

        if (_hueBar != null)
        {
            _hueBar.MouseLeftButtonDown -= OnHueMouseDown;
            _hueBar.MouseMove -= OnHueMouseMove;
            _hueBar.MouseLeftButtonUp -= OnHueMouseUp;
        }
    }

    private void SubscribeEvents()
    {
        if (_svCanvas != null)
        {
            _svCanvas.MouseLeftButtonDown += OnSVMouseDown;
            _svCanvas.MouseMove += OnSVMouseMove;
            _svCanvas.MouseLeftButtonUp += OnSVMouseUp;
        }

        if (_hueBar != null)
        {
            _hueBar.MouseLeftButtonDown += OnHueMouseDown;
            _hueBar.MouseMove += OnHueMouseMove;
            _hueBar.MouseLeftButtonUp += OnHueMouseUp;
        }
    }

    #region SV Canvas 事件处理

    private void OnSVMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingSV = true;
        _svCanvas?.CaptureMouse();
        UpdateSVFromMouse(e.GetPosition(_svCanvas));
    }

    private void OnSVMouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingSV && _svCanvas != null)
        {
            UpdateSVFromMouse(e.GetPosition(_svCanvas));
        }
    }

    private void OnSVMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingSV = false;
        _svCanvas?.ReleaseMouseCapture();
    }

    private void UpdateSVFromMouse(Point position)
    {
        if (_svCanvas == null) return;

        var x = Math.Max(0, Math.Min(position.X, _svCanvas.ActualWidth));
        var y = Math.Max(0, Math.Min(position.Y, _svCanvas.ActualHeight));

        _saturation = x / _svCanvas.ActualWidth;
        _value = 1 - (y / _svCanvas.ActualHeight);

        UpdateColorFromHSV();
        UpdateSVThumbPosition();
    }

    #endregion

    #region Hue Bar 事件处理

    private void OnHueMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingHue = true;
        _hueBar?.CaptureMouse();
        UpdateHueFromMouse(e.GetPosition(_hueBar));
    }

    private void OnHueMouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingHue && _hueBar != null)
        {
            UpdateHueFromMouse(e.GetPosition(_hueBar));
        }
    }

    private void OnHueMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingHue = false;
        _hueBar?.ReleaseMouseCapture();
    }

    private void UpdateHueFromMouse(Point position)
    {
        if (_hueBar == null) return;

        var y = Math.Max(0, Math.Min(position.Y, _hueBar.ActualHeight));
        _hue = (y / _hueBar.ActualHeight) * 360;

        UpdateColorFromHSV();
        UpdateHueThumbPosition();
        UpdateSVCanvasBackground();
    }

    #endregion

    #region 颜色转换

    private void UpdateFromColor(Color color)
    {
        // RGB to HSV
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        // Hue
        if (delta == 0)
            _hue = 0;
        else if (max == r)
            _hue = 60 * (((g - b) / delta) % 6);
        else if (max == g)
            _hue = 60 * (((b - r) / delta) + 2);
        else
            _hue = 60 * (((r - g) / delta) + 4);

        if (_hue < 0) _hue += 360;

        // Saturation
        _saturation = max == 0 ? 0 : delta / max;

        // Value
        _value = max;

        UpdateSVCanvasBackground();
        UpdateSVThumbPosition();
        UpdateHueThumbPosition();
    }

    private void UpdateColorFromHSV()
    {
        _isUpdatingFromControls = true;
        try
        {
            var color = HSVToRGB(_hue, _saturation, _value);
            SelectedColor = color;
        }
        finally
        {
            _isUpdatingFromControls = false;
        }
    }

    private static Color HSVToRGB(double h, double s, double v)
    {
        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
        double m = v - c;

        double r, g, b;
        if (h < 60)
        {
            r = c; g = x; b = 0;
        }
        else if (h < 120)
        {
            r = x; g = c; b = 0;
        }
        else if (h < 180)
        {
            r = 0; g = c; b = x;
        }
        else if (h < 240)
        {
            r = 0; g = x; b = c;
        }
        else if (h < 300)
        {
            r = x; g = 0; b = c;
        }
        else
        {
            r = c; g = 0; b = x;
        }

        return Color.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }

    #endregion

    #region UI 更新

    private void UpdateSVCanvasBackground()
    {
        if (_svBackground == null) return;

        var baseColor = HSVToRGB(_hue, 1, 1);
        _svBackground.Fill = new SolidColorBrush(baseColor);
    }

    private void UpdateSVThumbPosition()
    {
        if (_svThumb == null || _svCanvas == null) return;

        var x = _saturation * _svCanvas.ActualWidth;
        var y = (1 - _value) * _svCanvas.ActualHeight;

        Canvas.SetLeft(_svThumb, x - _svThumb.Width / 2);
        Canvas.SetTop(_svThumb, y - _svThumb.Height / 2);
    }

    private void UpdateHueThumbPosition()
    {
        if (_hueThumb == null || _hueBar == null) return;

        var y = (_hue / 360) * _hueBar.ActualHeight;
        Canvas.SetTop(_hueThumb, y - _hueThumb.Height / 2);
    }

    #endregion
}
