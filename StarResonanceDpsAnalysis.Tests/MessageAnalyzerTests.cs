using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.Core.Analyze.V1;
using StarResonanceDpsAnalysis.Core.Analyze.V2.Processors.WorldNtf;
using StarResonanceDpsAnalysis.Core.Data;

namespace StarResonanceDpsAnalysis.Tests;

public class MessageAnalyzerTests : IDisposable
{
    private static DataStorage _dataStorage = DataStorage.Instance;
    private readonly MessageAnalyzer _messageAnalyzer = new MessageAnalyzer(_dataStorage);
    public MessageAnalyzerTests()
    {
        _dataStorage.ClearAllDpsData();
        _dataStorage.ClearAllPlayerInfos();
        _dataStorage.ClearCurrentPlayerInfo();
    }

    [Fact]
    public void Process_SyncNearEntities_PopulatesStaticStorage()
    {
        var playerUid = 55502962L;
        var payload = TestMessageBuilder.BuildSyncNearEntitiesPayload(playerUid, "Static Hero", 66);
        var envelope = TestMessageBuilder.BuildNotifyEnvelope(WorldNtfMessageId.SyncNearEntities, payload);

        _messageAnalyzer.Process(envelope);

        Assert.True(_dataStorage.ReadOnlyPlayerInfoDatas.TryGetValue(playerUid, out var info));
        Assert.Equal("Static Hero", info!.Name);
        Assert.Equal(66, info.Level);
    }

    [Fact]
    public void Process_MalformedPacket_DoesNotThrow()
    {
        var malformed = new byte[] { 0x00, 0x00, 0x00, 0x01 };
        var exception = Record.Exception(() => _messageAnalyzer.Process(malformed));
        Assert.Null(exception);
    }

    [Fact]
    public void Process_SyncNearDeltaInfo_StampsAttackerBuffsIntoBattleLog()
    {
        var attackerUid = 55502962L;
        var targetUid = 66602962L;
        const int buffTableUuid = 41001;
        const int buffLayer = 2;

        _dataStorage.SetCurrentPlayerUid(attackerUid);
        _dataStorage.SetPlayerCombatState(attackerUid, true);

        var payload = TestMessageBuilder.BuildSyncNearDeltaInfoWithBuffAndDamagePayload(
            targetUid,
            attackerUid,
            buffTableUuid,
            buffLayer,
            damage: 12345,
            skillId: 9001);
        var envelope = TestMessageBuilder.BuildNotifyEnvelope(WorldNtfMessageId.SyncNearDeltaInfo, payload);

        _messageAnalyzer.Process(envelope);

        var logs = _dataStorage.GetBattleLogs(fullSession: false);
        var log = Assert.Single(logs);
        Assert.Contains(log.AttackerActiveBuffs, x => x.TableUuid == buffTableUuid && x.Layer == buffLayer);

        var snapshot = _dataStorage.GetEntityBuffSnapshot(attackerUid);
        Assert.NotNull(snapshot);
        Assert.Equal(attackerUid, snapshot!.EntityUid);
        var buff = Assert.Single(snapshot.Buffs);
        Assert.Equal(buffTableUuid, buff.TableUuid);
        Assert.Equal(buffLayer, buff.Layer);
    }

    public void Dispose()
    {
        _dataStorage.ClearAllDpsData();
        _dataStorage.ClearAllPlayerInfos();
        _dataStorage.ClearCurrentPlayerInfo();
    }
}
