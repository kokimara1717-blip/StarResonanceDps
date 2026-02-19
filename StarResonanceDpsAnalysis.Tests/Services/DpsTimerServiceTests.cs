using System;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using StarResonanceDpsAnalysis.WPF.Services;
using Xunit;

namespace StarResonanceDpsAnalysis.Tests.Services;

/// <summary>
/// Unit tests for DpsTimerService demonstrating SOLID principles
/// - Single Responsibility: Tests only timing logic
/// - No dependencies on UI or other services
/// - Independently testable
/// </summary>
public class DpsTimerServiceTests
{
    [Fact]
    public void Start_WhenNotRunning_StartsTimer()
    {
        // Arrange
        var service = new DpsTimerService(NullLogger<DpsTimerService>.Instance);
        
        // Act
        service.Start();
        
        // Assert
        Assert.True(service.IsRunning);
    }
    
    [Fact]
    public void Stop_WhenRunning_StopsTimer()
    {
        // Arrange
        var service = new DpsTimerService(NullLogger<DpsTimerService>.Instance);
        service.Start();
        
        // Act
        service.Stop();
        
        // Assert
        Assert.False(service.IsRunning);
    }
    
    [Fact]
    public void Reset_ClearsAllTimers()
    {
        // Arrange
        var service = new DpsTimerService(NullLogger<DpsTimerService>.Instance);
        service.Start();
        Thread.Sleep(100);
        service.StartNewSection();
        
        // Act
        service.Reset();
        
        // Assert
        Assert.False(service.IsRunning);
        Assert.Equal(TimeSpan.Zero, service.SectionDuration);
        Assert.Equal(TimeSpan.Zero, service.TotalDuration);
    }
    
    [Fact]
    public void StartNewSection_DoesNotResetTotalDuration()
    {
        // Arrange
        var service = new DpsTimerService(NullLogger<DpsTimerService>.Instance);
        service.Start();
        Thread.Sleep(100); // Simulate first section
        var firstDuration = service.SectionDuration;
        
        // Act
        service.StartNewSection();
        Thread.Sleep(50); // Simulate second section
        
        // Assert
        Assert.True(service.TotalDuration >= firstDuration);
        Assert.True(service.TotalDuration.TotalMilliseconds >= 150);
    }
    
    [Fact]
    public void SectionDuration_UpdatesWhileRunning()
    {
        // Arrange
        var service = new DpsTimerService(NullLogger<DpsTimerService>.Instance);
        service.Start();
        Thread.Sleep(100);
        
        // Act
        var sectionDuration = service.SectionDuration;
        Thread.Sleep(50); // Timer continues
        var currentDuration = service.SectionDuration;
        
        // Assert
        Assert.True(sectionDuration.TotalMilliseconds >= 100);
        Assert.True(currentDuration.TotalMilliseconds >= 150); // Should have continued
    }
    
    [Fact]
    public void StopSection_FreezesSectionDuration()
    {
        // Arrange
        var service = new DpsTimerService(NullLogger<DpsTimerService>.Instance);
        service.Start();
        Thread.Sleep(100);

        // Act
        service.StopSection();
        var frozenDuration = service.SectionDuration;
        Thread.Sleep(50);
        var currentDuration = service.SectionDuration;

        // Assert
        Assert.Equal(frozenDuration, currentDuration);
        Assert.True(frozenDuration.TotalMilliseconds >= 100);
        // Total duration should continue
        Assert.True(service.TotalDuration > currentDuration);
    }

    [Fact]
    public void Stop_PausesTotalDuration()
    {
        // Arrange
        var service = new DpsTimerService(NullLogger<DpsTimerService>.Instance);
        service.Start();
        Thread.Sleep(100);
        service.Stop();
        var durationAtStop = service.TotalDuration;

        // Act
        Thread.Sleep(100);
        var durationAfterPause = service.TotalDuration;
        service.Start();
        Thread.Sleep(100);
        var durationAfterResume = service.TotalDuration;

        // Assert
        Assert.Equal(durationAtStop, durationAfterPause);
        Assert.True(durationAfterResume.TotalMilliseconds >= durationAtStop.TotalMilliseconds + 100);
        // Allowing for some execution time variance, it should be roughly +100ms
    }

    [Fact]
    public void SectionDuration_ReturnsCurrentDuration()
    {
        // Arrange
        var service = new DpsTimerService(NullLogger<DpsTimerService>.Instance);
        service.Start();
        Thread.Sleep(100);
        
        // Act
        var duration = service.SectionDuration;
        
        // Assert
        Assert.True(duration.TotalMilliseconds >= 100);
    }
    
    [Fact]
    public void MultipleStartCalls_DoNotResetTimer()
    {
        // Arrange
        var service = new DpsTimerService(NullLogger<DpsTimerService>.Instance);
        service.Start();
        Thread.Sleep(100);
        var firstDuration = service.SectionDuration;
        
        // Act
        service.Start(); // Should not reset
        Thread.Sleep(50);
        var secondDuration = service.SectionDuration;
        
        // Assert
        Assert.True(secondDuration >= firstDuration);
    }
}
