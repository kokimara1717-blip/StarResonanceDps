using System.Diagnostics;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.WPF.Services;

namespace StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine.DataSource;

public sealed partial class ActiveUpdateModeDpsDataSource : RealTimeDataSource
{
    private readonly ILogger _logger;
    private readonly DispatcherTimer _timer;

    public ActiveUpdateModeDpsDataSource(DataSourceEngine dataSourceEngine, IDataStorage dataStorage, ILogger logger,
        IDpsDataProcessor processor) : base(dataSourceEngine, dataStorage,
        DataSourceMode.Active, processor)
    {
        _logger = logger;
        _timer = new DispatcherTimer();
        SetUpdateInterval(500);
        _timer.Tick += TimerOnTick;
    }

    [Conditional("DEBUG")]
    private void TickLog()
    {
        var currentSecond = DateTime.Now.Second;
        if (currentSecond % 10 == 0)
        {
            LogTickTrace();
        }
    }

    private void TimerOnTick(object? sender, EventArgs e)
    {
        TickLog();
        Refresh();
    }

    public override void SetEnable(bool enable)
    {
        base.SetEnable(enable);
        _timer.IsEnabled = enable;
        lock (SyncRoot)
        {
            if (!enable)
            {
                ClearCache();
            }
        }
    }

    public void SetUpdateInterval(int updateInterval)
    {
        updateInterval = Math.Clamp(updateInterval, 100, 5000);
        _timer.Interval = TimeSpan.FromMilliseconds(updateInterval);
    }


    [LoggerMessage(LogLevel.Trace, "Timer tick triggered")]
    private partial void LogTickTrace();
}