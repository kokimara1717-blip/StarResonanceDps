using System.Globalization;
using System.Windows.Data;
using StarResonanceDpsAnalysis.Core.Models;
using StarResonanceDpsAnalysis.WPF.Services;

namespace StarResonanceDpsAnalysis.WPF.Converters;

internal sealed class ClassesColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var brushCache = ClassColorCache.GetCache;
        if (value is not Classes classes) return null;

        return brushCache.TryGetValue(classes, out var cached) ? cached : null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}