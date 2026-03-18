namespace StarResonanceDpsAnalysis.Core.Analyze;

/// <summary>
/// Manages buff monitors for multiple entities (e.g., bosses, enemies).
/// </summary>
public sealed class EntityBuffMonitors
{
    /// <summary>
    /// Dictionary of buff monitors keyed by entity UID.
    /// </summary>
    public Dictionary<long, BuffMonitor> Monitors { get; } = [];

    /// <summary>
    /// Global set of buff IDs to monitor across all entities.
    /// </summary>
    public HashSet<int> MonitoredBuffIds { get; private set; } = [];

    /// <summary>
    /// Set of buff IDs that should only be tracked when applied by the local player.
    /// </summary>
    public HashSet<int> SelfAppliedBuffIds { get; private set; } = [];

    /// <summary>
    /// Clears all entity monitors.
    /// </summary>
    public void Clear()
    {
        Monitors.Clear();
    }

    /// <summary>
    /// Updates the global configuration for monitored buffs.
    /// </summary>
    /// <param name="globalIds">List of buff base IDs to monitor globally.</param>
    /// <param name="selfAppliedIds">List of buff base IDs to monitor only when self-applied.</param>
    public void SetConfig(IEnumerable<int> globalIds, IEnumerable<int> selfAppliedIds)
    {
        MonitoredBuffIds = [..globalIds];
        SelfAppliedBuffIds = [..selfAppliedIds];

        foreach (var monitor in Monitors.Values)
        {
            monitor.MonitoredBuffIds = [..MonitoredBuffIds];
            monitor.SelfAppliedBuffIds = [..SelfAppliedBuffIds];
        }
    }

    /// <summary>
    /// Gets or creates a buff monitor for the specified entity UID.
    /// </summary>
    /// <param name="entityUid">UID of the entity to monitor.</param>
    /// <returns>BuffMonitor instance for the entity.</returns>
    public BuffMonitor MonitorFor(long entityUid)
    {
        if (!Monitors.TryGetValue(entityUid, out var monitor))
        {
            monitor = new BuffMonitor
            {
                MonitoredBuffIds = [..MonitoredBuffIds],
                SelfAppliedBuffIds = [..SelfAppliedBuffIds]
            };
            Monitors[entityUid] = monitor;
        }

        return monitor;
    }
}
