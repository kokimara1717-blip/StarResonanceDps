using System;

namespace StarResonanceDpsAnalysis.WPF.Services;

/// <summary>
/// Service for managing DPS timer and battle duration tracking
/// Single Responsibility: Timer management and duration calculation
/// </summary>
public interface IDpsTimerService
{
    /// <summary>
    /// Current battle duration (total elapsed time)
    /// </summary>
    TimeSpan SectionDuration { get; }

    /// <summary>
    /// Total combat duration across all sections
    /// </summary>
    TimeSpan TotalDuration { get; }

    /// <summary>
    /// Whether the timer is currently running
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Start the timer
    /// </summary>
    void Start();

    /// <summary>
    /// Stop the timer
    /// </summary>
    void Stop();

    /// <summary>
    /// Reset the timer to zero
    /// </summary>
    void Reset();

    /// <summary>
    /// Mark the start of a new section
    /// </summary>
    void StartNewSection();

    /// <summary>
    /// Event raised when duration changes
    /// </summary>
    event EventHandler<TimeSpan>? DurationChanged;

    /// <summary>
    /// Stop current section
    /// </summary>
    void StopSection();
}
