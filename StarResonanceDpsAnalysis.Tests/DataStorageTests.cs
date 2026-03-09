using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;

namespace StarResonanceDpsAnalysis.Tests;

public class DataStorageTests : IDisposable
{
    public DataStorageTests()
    {
        DataStorage.Instance.ClearAllDpsData();
        DataStorage.Instance.ClearAllPlayerInfos();
        DataStorage.Instance.ClearCurrentPlayerInfo();
    }

    public void Dispose()
    {
        DataStorage.Instance.ClearAllDpsData();
        DataStorage.Instance.ClearAllPlayerInfos();
        DataStorage.Instance.ClearCurrentPlayerInfo();
    }

    [Fact]
    public void EnsurePlayer_AddsNewPlayer()
    {
        var uid = 987654321L;

        var existedBefore = DataStorage.Instance.EnsurePlayer(uid);
        var existedAfter = DataStorage.Instance.EnsurePlayer(uid);

        Assert.False(existedBefore);
        Assert.True(existedAfter);
        Assert.True(DataStorage.Instance.ReadOnlyPlayerInfoDatas.ContainsKey(uid));
    }

    [Fact]
    public void AddBattleLog_PlayerDamageNpc_UpdatesStaticStore()
    {
        var attackerUid = 6001L;
        var targetUid = 7002L;
        DataStorage.Instance.EnsurePlayer(attackerUid);
        DataStorage.Instance.EnsurePlayer(targetUid);

        var log = new BattleLog
        {
            PacketID = 42,
            TimeTicks = TimeSpan.FromMilliseconds(500).Ticks,
            SkillID = 123,
            AttackerUuid = attackerUid,
            TargetUuid = targetUid,
            Value = 777,
            ValueElementType = 0,
            DamageSourceType = 0,
            IsAttackerPlayer = true,
            IsTargetPlayer = false,
            IsLucky = false,
            IsCritical = false,
            IsHeal = false,
            IsMiss = false,
            IsDead = false
        };

        DataStorage.Instance.AddBattleLog(log);

        var attackerData = DataStorage.Instance.ReadOnlyFullDpsData[attackerUid];
        Assert.Equal(777, attackerData.TotalAttackDamage);
        var npcData = DataStorage.Instance.ReadOnlyFullDpsData[targetUid];
        Assert.True(npcData.IsNpcData);
        Assert.Equal(777, npcData.TotalTakenDamage);
    }

    [Fact]
    public void AddBattleLog_TriggersEvents()
    {
        var battleLogCreated = false;
        var dpsDataUpdated = false;
        var dataUpdated = false;

        DataStorage.Instance.BattleLogCreated += OnDataStorageOnBattleLogCreated;
        DataStorage.Instance.DpsDataUpdated += DataStorageOnDpsDataUpdated;
        DataStorage.Instance.DataUpdated += OnDataUpdated;

        var log = new BattleLog { TimeTicks = 1, AttackerUuid = 1, Value = 10 };
        DataStorage.Instance.AddBattleLog(log);

        Assert.True(battleLogCreated);
        Assert.True(dpsDataUpdated);
        Assert.True(dataUpdated);

        // Cleanup static event handlers
        DataStorage.Instance.BattleLogCreated -= OnDataStorageOnBattleLogCreated;
        DataStorage.Instance.DpsDataUpdated -= DataStorageOnDpsDataUpdated;
        DataStorage.Instance.DataUpdated -= OnDataUpdated;


        return;

        void OnDataUpdated()
        {
            dataUpdated = true;
        }

        void OnDataStorageOnBattleLogCreated(BattleLog _)
        {
            battleLogCreated = true;
        }

        void DataStorageOnDpsDataUpdated()
        {
            dpsDataUpdated = true;
        }
    }

    /*
    [Fact]
    public void AddBattleLog_CreatesNewSection_WhenTimeout()
    {
        // 本测试需要单独运行，不能和其他测试并行
        var newSectionCreated = false;
        DataStorage.Instance.NewSectionCreated += OnDataStorageOnNewSectionCreated;

        var log1 = new BattleLog { TimeTicks = TimeSpan.FromSeconds(1).Ticks, AttackerUuid = 1, Value = 10 };
        DataStorage.Instance.AddBattleLog(log1);

        Assert.False(newSectionCreated);

        var log2 = new BattleLog { TimeTicks = TimeSpan.FromSeconds(10).Ticks, AttackerUuid = 1, Value = 10 };
        DataStorage.Instance.AddBattleLog(log2);

        Assert.True(newSectionCreated);

        DataStorage.Instance.NewSectionCreated -= OnDataStorageOnNewSectionCreated;
        return;

        void OnDataStorageOnNewSectionCreated()
        {
            newSectionCreated = true;
        }
    }
    */

    [Fact]
    public void ClearDpsData_ClearsSectionedData()
    {
        DataStorage.Instance.AddBattleLog(new BattleLog { AttackerUuid = 1, Value = 100 });
        Assert.NotEmpty(DataStorage.Instance.ReadOnlySectionedDpsData);

        DataStorage.Instance.ClearSectionDpsData();

        Assert.Empty(DataStorage.Instance.ReadOnlySectionedDpsData);
        Assert.NotEmpty(DataStorage.Instance.ReadOnlyFullDpsData);
    }

    [Fact]
    public void ClearAllDpsData_ClearsAllDps()
    {
        DataStorage.Instance.AddBattleLog(new BattleLog { AttackerUuid = 1, Value = 100, IsAttackerPlayer = true });
        Assert.NotEmpty(DataStorage.Instance.ReadOnlyFullDpsData);
        Assert.NotEmpty(DataStorage.Instance.ReadOnlySectionedDpsData);

        DataStorage.Instance.ClearAllDpsData();

        Assert.Empty(DataStorage.Instance.ReadOnlyFullDpsData);
        Assert.Empty(DataStorage.Instance.ReadOnlySectionedDpsData);
    }

    [Fact]
    public void SetPlayerName_UpdatesName_And_TriggersEvent()
    {
        var uid = 55502692L;
        DataStorage.Instance.EnsurePlayer(uid);

        var eventTriggered = false;
        PlayerInfo? updatedInfo = null;

        PlayerInfoUpdatedEventHandler handler = info =>
        {
            eventTriggered = true;
            updatedInfo = info;
        };
        DataStorage.Instance.PlayerInfoUpdated += handler;

        var newName = "Test Player Static";
        DataStorage.Instance.SetPlayerName(uid, newName);

        Assert.Equal(newName, DataStorage.Instance.ReadOnlyPlayerInfoDatas[uid].Name);
        Assert.True(eventTriggered);
        Assert.NotNull(updatedInfo);
        Assert.Equal(newName, updatedInfo.Name);

        DataStorage.Instance.PlayerInfoUpdated -= handler;
    }
}