using Microsoft.Extensions.DependencyInjection;
using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.Core.Data;

namespace StarResonanceDpsAnalysis.WPF.Extensions;

public static class DataExtensions
{
    public static IServiceCollection AddPacketAnalyzer(this IServiceCollection services)
    {
#if true
        return services.AddSingleton<IDataStorage, DataStorage>(_ => DataStorage.Instance)
            .AddSingleton<IPacketAnalyzer, PacketAnalyzer>();
#else
        return services.AddSingleton<IDataStorage, DataStorage>()
        //return services.AddSingleton<IDataStorage, DataStorageV2>()
            //.AddSingleton<IPacketAnalyzer,PacketAnalyzerV2>()
             .AddSingleton<IPacketAnalyzer, PacketAnalyzer>()
            .AddSingleton<MessageAnalyzerV2>();
#endif
    }
}