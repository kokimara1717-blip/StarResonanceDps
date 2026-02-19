using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDpsAnalysis.WPF.Config;

public static class BuildInfo
{
    public static string GetBuildTime()
    {
        var attribute = Assembly
            .GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildTime");

        return $"(UTC){attribute?.Value}" ?? "Unknown";
    }

    public static string GetVersion()
    {
        var v = Assembly
            .GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyFileVersionAttribute>()?
            .Version ?? "-.-.-";

        return $"v{v.Split('+')[0]}";
    }
}

