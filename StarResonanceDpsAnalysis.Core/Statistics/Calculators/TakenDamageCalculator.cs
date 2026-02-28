using StarResonanceDpsAnalysis.Core.Data.Models;

namespace StarResonanceDpsAnalysis.Core.Statistics.Calculators;

/// <summary>
/// Calculates taken damage statistics
/// Following SRP: Only handles damage taken calculation
/// </summary>
public sealed class TakenDamageCalculator : IStatisticsCalculator
{
    public string StatisticTypeName => "TakenDamage";

    public void Calculate(BattleLog log, StatisticsContext context)
    {
        // Process damage taken by players
        if (log.IsHeal)
            return;

        context.CombatStarted = true;

        if (log.IsTargetPlayer)
        {
            var fullStats = context.GetOrCreateFullStats(log.TargetUuid);
            var sectionStats = context.GetOrCreateSectionStats(log.TargetUuid);

            UpdateStatistics(log, fullStats);
            UpdateStatistics(log, sectionStats);
        }
        else
        {
            var npcFull = context.GetOrCreateFullStats(log.TargetUuid);
            var npcSection = context.GetOrCreateSectionStats(log.TargetUuid);
            npcFull.IsNpc = true;
            npcSection.IsNpc = true;

            UpdateNpcTakenDamageStats(log, npcFull);
            UpdateNpcTakenDamageStats(log, npcSection);
        }
    }

    public void ResetSection(StatisticsContext context)
    {
        // Section reset is handled by context
    }

    private void UpdateStatistics(BattleLog log, PlayerStatistics stats)
    {
        stats.StartTick ??= log.TimeTicks;
        stats.LastTick = log.TimeTicks;
        var ticks = stats.ElapsedTicks();

        var values = stats.TakenDamage;
        values.Total += log.Value;
        values.ValuePerSecond = ticks > 0 ? (double)values.Total * TimeSpan.TicksPerSecond / ticks : 0;

        // Update skill breakdown
        var skill = stats.GetOrCreateTakenSkill(log.SkillID);
        skill.TotalValue += log.Value;
        skill.UseTimes++;

        // Handle different hit types (aligned with PlayerStat.cs logic)
        if (log.IsCritical && log.IsLucky)
        {
            // Both crit and lucky: increment both counters and add to CritValue
            values.CritAndLuckyCount++;
            values.CritAndLuckyValue += log.Value;
            skill.CritAndLuckyTimes++;
            skill.CritAndLuckyValue += log.Value;
        }
        else if (log.IsCritical)
        {
            // Only crit
            values.CritCount++;
            values.CritValue += log.Value;
            skill.CritTimes++;
            skill.CritValue += log.Value;
        }
        else if (log.IsLucky)
        {
            // Only lucky
            values.LuckyCount++;
            values.LuckyValue += log.Value;
            skill.LuckyTimes++;
            skill.LuckValue += log.Value;
        }
        else
        {
            // Normal hit
            values.NormalValue += log.Value;
        }

        if (!log.IsLucky)
        {
            values.HitCount++;
        }
    }

    private void UpdateNpcTakenDamageStats(BattleLog log, PlayerStatistics stats)
    {
        // Update NPC's attack damage output
        stats.StartTick ??= log.TimeTicks;
        stats.LastTick = log.TimeTicks;

        var values = stats.TakenDamage;
        values.Total += log.Value;

        // Update skill breakdown
        var skill = stats.GetOrCreateTakenSkill(log.SkillID);
        skill.TotalValue += log.Value;
        skill.UseTimes++;

        // Handle different hit types (aligned with PlayerStat.cs logic)
        if (log.IsCritical && log.IsLucky)
        {
            // Both crit and lucky: increment both counters and add to CritValue
            values.CritAndLuckyCount++;
            values.CritAndLuckyValue += log.Value;
            skill.CritAndLuckyTimes++;
            skill.CritAndLuckyValue += log.Value;
        }
        else if (log.IsCritical)
        {
            // Only crit
            values.CritCount++;
            values.CritValue += log.Value;
            skill.CritTimes++;
            skill.CritValue += log.Value;
        }
        else if (log.IsLucky)
        {
            // Only lucky
            values.LuckyCount++;
            values.LuckyValue += log.Value;
            skill.LuckyTimes++;
            skill.LuckValue += log.Value;
        }
        else
        {
            // Normal hit
            values.NormalValue += log.Value;
        }

        if (!log.IsLucky)
        {
            values.HitCount++;
        }
    }
}
