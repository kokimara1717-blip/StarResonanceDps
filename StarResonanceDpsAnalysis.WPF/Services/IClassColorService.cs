using System.Windows.Media;
using StarResonanceDpsAnalysis.Core.Models;

namespace StarResonanceDpsAnalysis.WPF.Services;

public interface IClassColorService
{
    void Init();
    Color GetDefaultColor(Classes cls);
    void UpdateColor(Classes classes, Color color);
    void UpdateColors(IDictionary<Classes, string> colors);
    Color GetColor(Classes cls);
}