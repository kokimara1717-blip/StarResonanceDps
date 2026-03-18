using Google.Protobuf;
using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.Core.Data.Models;
using Zproto;

namespace StarResonanceDpsAnalysis.Tests;

public class BuffMonitorTests
{
    [Fact]
    public void ProcessBuffEffectBytes_AddBuff_AddsToActiveBuffs()
    {
        // Arrange
        var monitor = new BuffMonitor
        {
            MonitorAllBuff = true
        };
        long serverOffset = 0;
        const long localPlayerUid = 12345L;
        const int buffBaseId = 100;
        const int buffUuid = 1;

        var buffInfo = new BuffInfo
        {
            BaseId = buffBaseId,
            Layer = 3,
            Duration = 5000,
            CreateTime = 1000000,
            FireUuid = localPlayerUid << 16
        };

        var buffEffect = new BuffEffect
        {
            BuffUuid = buffUuid,
            Type = EBuffEventType.BuffEventAddTo
        };

        var logicEffect = new BuffEffectLogicInfo
        {
            EffectType = EBuffEffectLogicPbType.BuffEffectAddBuff,
            RawData = buffInfo.ToByteString()
        };
        buffEffect.LogicEffect.Add(logicEffect);

        var buffEffectSync = new BuffEffectSync();
        buffEffectSync.BuffEffects.Add(buffEffect);

        // Act
        var result = monitor.ProcessBuffEffect(buffEffectSync, ref serverOffset, localPlayerUid);

        // Assert
        Assert.Single(monitor.ActiveBuffs);
        Assert.True(monitor.ActiveBuffs.ContainsKey(buffUuid));
        Assert.Equal(buffBaseId, monitor.ActiveBuffs[buffUuid].BaseId);
        Assert.Equal(3, monitor.ActiveBuffs[buffUuid].Layer);
        Assert.Equal(5000, monitor.ActiveBuffs[buffUuid].Duration);

        Assert.Single(result.Changes);
        Assert.Equal(BuffChangeType.Add, result.Changes[0].ChangeType);
        Assert.Equal(buffBaseId, result.Changes[0].BaseId);

        Assert.NotNull(result.UpdatePayload);
        Assert.Single(result.UpdatePayload);
    }

    [Fact]
    public void ProcessBuffEffectBytes_RemoveBuff_RemovesFromActiveBuffs()
    {
        // Arrange
        var monitor = new BuffMonitor
        {
            MonitorAllBuff = true
        };
        const int buffUuid = 1;
        const int buffBaseId = 100;

        monitor.ActiveBuffs[buffUuid] = new ActiveBuff
        {
            BaseId = buffBaseId,
            Layer = 1,
            Duration = 1000,
            CreateTime = 1000000
        };

        long serverOffset = 0;
        const long localPlayerUid = 12345L;

        var buffEffect = new BuffEffect
        {
            BuffUuid = buffUuid,
            Type = EBuffEventType.BuffEventRemove
        };

        var buffEffectSync = new BuffEffectSync();
        buffEffectSync.BuffEffects.Add(buffEffect);

        // Act
        var result = monitor.ProcessBuffEffect(buffEffectSync, ref serverOffset, localPlayerUid);

        // Assert
        Assert.Empty(monitor.ActiveBuffs);
        Assert.Single(result.Changes);
        Assert.Equal(BuffChangeType.Remove, result.Changes[0].ChangeType);
        Assert.Equal(buffBaseId, result.Changes[0].BaseId);
    }

    [Fact]
    public void EntityBuffMonitors_SetConfig_UpdatesAllMonitors()
    {
        // Arrange
        var entityMonitors = new EntityBuffMonitors();
        var monitor1 = entityMonitors.MonitorFor(1L);
        var monitor2 = entityMonitors.MonitorFor(2L);

        // Act
        entityMonitors.SetConfig([100, 200], [300]);

        // Assert
        Assert.Equal(2, monitor1.MonitoredBuffIds.Count);
        Assert.Contains(100, monitor1.MonitoredBuffIds);
        Assert.Contains(200, monitor1.MonitoredBuffIds);
        Assert.Single(monitor1.SelfAppliedBuffIds);
        Assert.Contains(300, monitor1.SelfAppliedBuffIds);

        Assert.Equal(2, monitor2.MonitoredBuffIds.Count);
        Assert.Single(monitor2.SelfAppliedBuffIds);
    }

    [Fact]
    public void ProcessBuffEffectBytes_FiltersBySelfApplied()
    {
        // Arrange
        var monitor = new BuffMonitor();
        monitor.SelfAppliedBuffIds.Add(100);

        long serverOffset = 0;
        const long localPlayerUid = 12345L;
        const long otherPlayerUid = 99999L;
        const int buffBaseId = 100;
        const int buffUuid = 1;

        var buffInfo = new BuffInfo
        {
            BaseId = buffBaseId,
            Layer = 1,
            Duration = 5000,
            CreateTime = 1000000,
            FireUuid = otherPlayerUid << 16 // Different player
        };

        var buffEffect = new BuffEffect
        {
            BuffUuid = buffUuid
        };

        var logicEffect = new BuffEffectLogicInfo
        {
            EffectType = EBuffEffectLogicPbType.BuffEffectAddBuff,
            RawData = buffInfo.ToByteString()
        };
        buffEffect.LogicEffect.Add(logicEffect);

        var buffEffectSync = new BuffEffectSync();
        buffEffectSync.BuffEffects.Add(buffEffect);

        // Act
        var result = monitor.ProcessBuffEffect(buffEffectSync, ref serverOffset, localPlayerUid);

        // Assert
        Assert.Empty(monitor.ActiveBuffs); // Should be filtered out
        Assert.Empty(result.Changes);
    }
}

public class EntityBuffMonitorsTests
{
    [Fact]
    public void SetConfig_UpdatesAllMonitors()
    {
        // Arrange
        var entityMonitors = new EntityBuffMonitors();
        var monitor1 = entityMonitors.MonitorFor(1L);
        var monitor2 = entityMonitors.MonitorFor(2L);

        // Act
        entityMonitors.SetConfig([100, 200], [300]);

        // Assert
        Assert.Equal(2, monitor1.MonitoredBuffIds.Count);
        Assert.Contains(100, monitor1.MonitoredBuffIds);
        Assert.Contains(200, monitor1.MonitoredBuffIds);
        Assert.Single(monitor1.SelfAppliedBuffIds);
        Assert.Contains(300, monitor1.SelfAppliedBuffIds);

        Assert.Equal(2, monitor2.MonitoredBuffIds.Count);
        Assert.Single(monitor2.SelfAppliedBuffIds);
    }

    [Fact]
    public void MonitorFor_CreatesNewMonitorIfNotExists()
    {
        // Arrange
        var entityMonitors = new EntityBuffMonitors();
        const long entityUid = 999L;

        // Act
        var monitor = entityMonitors.MonitorFor(entityUid);

        // Assert
        Assert.NotNull(monitor);
        Assert.Same(monitor, entityMonitors.MonitorFor(entityUid)); // Same instance
    }

    [Fact]
    public void Clear_RemovesAllMonitors()
    {
        // Arrange
        var entityMonitors = new EntityBuffMonitors();
        entityMonitors.MonitorFor(1L);
        entityMonitors.MonitorFor(2L);

        // Act
        entityMonitors.Clear();

        // Assert
        Assert.Empty(entityMonitors.Monitors);
    }
}
