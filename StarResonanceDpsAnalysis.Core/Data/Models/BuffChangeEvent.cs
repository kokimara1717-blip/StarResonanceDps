namespace StarResonanceDpsAnalysis.Core.Data.Models;

/// <summary>
/// Event representing a buff change on an entity.
/// </summary>
public sealed class BuffChangeEvent
{
    /// <summary>
    /// Base buff ID that changed.
    /// </summary>
    public required int BaseId { get; init; }

    /// <summary>
    /// Type of change that occurred.
    /// </summary>
    public required BuffChangeType ChangeType { get; init; }
}
