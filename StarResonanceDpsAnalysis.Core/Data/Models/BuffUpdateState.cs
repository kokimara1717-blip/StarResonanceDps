namespace StarResonanceDpsAnalysis.Core.Data.Models;

/// <summary>
/// Represents the current state of a buff for UI updates.
/// </summary>
public sealed class BuffUpdateState
{
    /// <summary>
    /// Base buff ID.
    /// </summary>
    public required int BaseId { get; init; }

    /// <summary>
    /// Current stack layer/count.
    /// </summary>
    public int Layer { get; init; }

    /// <summary>
    /// Duration in milliseconds.
    /// </summary>
    public long DurationMs { get; init; }

    /// <summary>
    /// Creation time adjusted with server clock offset (in milliseconds).
    /// </summary>
    public long CreateTimeMs { get; init; }
}
