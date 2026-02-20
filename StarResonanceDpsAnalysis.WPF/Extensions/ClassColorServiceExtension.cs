using Microsoft.Extensions.DependencyInjection;
using StarResonanceDpsAnalysis.WPF.Services;

namespace StarResonanceDpsAnalysis.WPF.Extensions;

public static class ClassColorServiceExtension
{
    public static IServiceCollection AddClassColorService(this IServiceCollection services)
    {
        return services.AddSingleton<IClassColorService, ClassColorService>();
    }
}