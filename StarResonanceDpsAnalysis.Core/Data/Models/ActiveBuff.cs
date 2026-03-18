namespace StarResonanceDpsAnalysis.Core.Data.Models;

/// <summary>
/// Represents an active buff on an entity.
/// </summary>
public sealed class ActiveBuff
{
    /// <summary>
    /// Base buff ID from the game data.
    /// </summary>
    public required int BaseId { get; set; }

    /// <summary>
    /// Stack layer/count of the buff.
    /// </summary>
    public int Layer { get; set; }

    /// <summary>
    /// Duration of the buff in milliseconds.
    /// </summary>
    public long Duration { get; set; }

    /// <summary>
    /// Server time when the buff was created (in milliseconds).
    /// </summary>
    public long CreateTime { get; set; }
}
