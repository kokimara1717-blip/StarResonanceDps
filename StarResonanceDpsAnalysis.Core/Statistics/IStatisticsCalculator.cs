using StarResonanceDpsAnalysis.Core.Data.Models;

namespace StarResonanceDpsAnalysis.Core.Statistics;

/// <summary>
/// Interface for calculating statistics from battle logs
/// Following SRP: Each implementation handles one type of statistic
/// </summary>
public interface IStatisticsCalculator
{
    /// <summary>
    /// Calculate statistics from a battle log
    /// </summary>
    void Calculate(BattleLog log, StatisticsContext context);

    /// <summary>
    /// Get the type of statistics this calculator handles
    /// </summary>
    string StatisticTypeName { get; }
}
