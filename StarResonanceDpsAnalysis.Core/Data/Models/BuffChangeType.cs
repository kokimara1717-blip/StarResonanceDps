namespace StarResonanceDpsAnalysis.Core.Data.Models;

/// <summary>
/// Type of buff change event.
/// </summary>
public enum BuffChangeType
{
    /// <summary>
    /// Buff was added to an entity.
    /// </summary>
    Add,

    /// <summary>
    /// Buff properties were changed (layer, duration, etc).
    /// </summary>
    Change,

    /// <summary>
    /// Buff was removed from an entity.
    /// </summary>
    Remove
}
