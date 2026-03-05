namespace StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine;

public sealed class DataSourceEngineParam
{
    public DataSourceMode? Mode { get; set; }
    public int? ActiveUpdateInterval { get; set; }

    public string? BattleHistoryFilePath { get; set; }
    public DummyTargetType? DummyTarget { get; set; }
    public long? PlayerUid { get; set; }
    public TimeSpan? TrainingTimeLimit { get; set; }
}