using System.Windows.Media;
using StarResonanceDpsAnalysis.Core.Models;
using StarResonanceDpsAnalysis.WPF.Config;

namespace StarResonanceDpsAnalysis.WPF.Services;

public sealed class ClassColorService(IConfigManager config) : IClassColorService
{
    public void Init()
    {
        ClassColorCache.InitDefaultColors();
        ClassColorCache.ResetAllCache();

        var colors = config.CurrentConfig.CustomClassColors;
        ClassColorCache.UpdateColors(colors);
    }

    public Color GetDefaultColor(Classes cls)
    {
        var d = ClassColorCache.GetDefault[cls];
        if (d is SolidColorBrush sc) return sc.Color;
        return Colors.White;
    }

    public Color GetColor(Classes cls)
    {
        var d = ClassColorCache.GetCache[cls];
        if (d is SolidColorBrush sc) return sc.Color;
        return Colors.White;
    }

    public void UpdateColor(Classes classes, Color color)
    {
        ClassColorCache.UpdateColor(classes, color);
    }

    public void UpdateColors(IDictionary<Classes, string> colors)
    {
        ClassColorCache.UpdateColors(colors);
    }
}