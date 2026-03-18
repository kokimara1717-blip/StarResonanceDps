namespace StarResonanceDpsAnalysis.Core.Data.Models;

/// <summary>
/// Result of processing buff effect data.
/// </summary>
public sealed class BuffProcessResult
{
    /// <summary>
    /// List of active monitored buffs for UI update. Null if no buffs are being monitored.
    /// </summary>
    public List<BuffUpdateState>? UpdatePayload { get; init; }

    /// <summary>
    /// List of buff changes that occurred during processing.
    /// </summary>
    public List<BuffChangeEvent> Changes { get; init; } = [];
}
