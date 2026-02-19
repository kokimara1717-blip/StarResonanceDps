using System;
using System.Diagnostics;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace StarResonanceDpsAnalysis.WPF.Services;

/// <summary>
/// Implementation of DPS timer service using DateTime and accumulated time
/// Tracks both section-level and total combat duration
/// </summary>
public partial class DpsTimerService : IDpsTimerService
{
    private readonly ILogger<DpsTimerService> _logger;
    private readonly DispatcherTimer _updateTimer;
    private DateTime _serviceStartTime;
    private DateTime _sectionStartTime;
    private DateTime _sectionEndTime;
    private bool _isSectionEnded;
    private bool _isRunning;
    private DateTime _lastStopTime;

    public TimeSpan SectionDuration => GetSectionElapsed();
    public TimeSpan TotalDuration => GetTotalDuration();
    public bool IsRunning => _isRunning;

    public event EventHandler<TimeSpan>? DurationChanged;

    public DpsTimerService(ILogger<DpsTimerService> logger)
    {
        _logger = logger;
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _updateTimer.Tick += (s, e) =>
        {
            DurationChanged?.Invoke(this, SectionDuration);
        };
    }

    public void Start()
    {
        if (IsRunning) return;

        var now = DateTime.UtcNow;
        if (_serviceStartTime == default)
        {
            _serviceStartTime = now;
            _sectionStartTime = now;
        }
        else if (_lastStopTime != default)
        {
            var gap = now - _lastStopTime;
            _serviceStartTime += gap;
            _sectionStartTime += gap;
            if (_isSectionEnded)
            {
                _sectionEndTime += gap;
            }
        }

        _isRunning = true;
        _updateTimer.Start();
        LogTimerStarted();
    }

    public void Stop()
    {
        if (!IsRunning) return;

        _lastStopTime = DateTime.UtcNow;
        _isRunning = false;
        _updateTimer.Stop();
        LogTimerStopped();
    }

    public void Reset()
    {
        Stop();
        _serviceStartTime = default;
        _sectionStartTime = default;
        _sectionEndTime = default;
        _lastStopTime = default;
        _isSectionEnded = false;
        DurationChanged?.Invoke(this, SectionDuration);
        LogTimerReset();
    }

    public void StartNewSection()
    {
        var now = IsRunning ? DateTime.UtcNow : (_lastStopTime != default ? _lastStopTime : DateTime.UtcNow);
        _sectionStartTime = now;
        _sectionEndTime = default;
        _isSectionEnded = false;
        
        LogNewSectionStarted();
    }

    public void StopSection()
    {
        if (_isSectionEnded) return;
        
        var now = IsRunning ? DateTime.UtcNow : (_lastStopTime != default ? _lastStopTime : DateTime.UtcNow);
        _sectionEndTime = now;
        _isSectionEnded = true;
    }

    private TimeSpan GetSectionElapsed()
    {
        TimeSpan elapsed;
        if (_isSectionEnded)
        {
            elapsed = _sectionEndTime - _sectionStartTime;
        }
        else
        {
            var now = IsRunning ? DateTime.UtcNow : _lastStopTime;
            // Handle case where service hasn't started yet
            if (_sectionStartTime == default) return TimeSpan.Zero;
            elapsed = now - _sectionStartTime;
        }
        
        elapsed = elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
        LogSectionElapsed(elapsed);
        return elapsed;
    }

    private TimeSpan GetTotalDuration()
    {
        if (_serviceStartTime == default) return TimeSpan.Zero;
        
        var now = IsRunning ? DateTime.UtcNow : _lastStopTime;
        var elapsed = now - _serviceStartTime;
        return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
    }

    [LoggerMessage(LogLevel.Trace, "Get section elapsed:{elapsed}")]
    private partial void LogSectionElapsed(TimeSpan elapsed);

    [LoggerMessage(LogLevel.Information, "Timer service started")]
    private partial void LogTimerStarted();

    [LoggerMessage(LogLevel.Information, "Timer service stopped")]
    private partial void LogTimerStopped();

    [LoggerMessage(LogLevel.Information, "Timer service reset")]
    private partial void LogTimerReset();

    [LoggerMessage(LogLevel.Information, "Timer service Started new section")]
    private partial void LogNewSectionStarted();
}
