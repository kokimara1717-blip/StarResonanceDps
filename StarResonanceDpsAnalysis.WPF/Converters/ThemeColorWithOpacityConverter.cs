using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StarResonanceDpsAnalysis.WPF.Converters;

/// <summary>
/// 将主题颜色字符串转换为带透明度的Color
/// 输入: [0] ThemeColor (string), [1] Opacity (0-100), [2] MouseThroughEnabled (bool)
/// 输出: Color with adjusted opacity
/// </summary>
public sealed class ThemeColorWithOpacityConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        // 默认颜色
        var defaultColor = Color.FromRgb(186, 186, 186); // #BABABA

        // 解析主题颜色
        Color themeColor = defaultColor;
        if (values.Length > 0 && values[0] is string colorString && !string.IsNullOrEmpty(colorString))
        {
            try
            {
                themeColor = (Color)ColorConverter.ConvertFromString(colorString);
            }
            catch
            {
                themeColor = defaultColor;
            }
        }

        // 解析透明度 (0-100 -> 0-255)
        double opacity = 100;
        if (values.Length > 1 && values[1] is double opacityValue)
        {
            opacity = opacityValue;
        }

        // 计算最终的Alpha值
        // 正常模式，根据滑块值计算透明度
        // opacity: 5-95 -> alpha: 13-242 (约 5%-95%)
        var normalizedOpacity = Math.Clamp(opacity, 5, 95) / 100.0;
        var alpha = (byte)(normalizedOpacity * 255);

        // 返回带透明度的颜色
        return Color.FromArgb(alpha, themeColor.R, themeColor.G, themeColor.B);
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
