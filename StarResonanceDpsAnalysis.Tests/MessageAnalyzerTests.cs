using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.Core.Analyze.V1;
using StarResonanceDpsAnalysis.Core.Analyze.V2.Processors.WorldNtf;
using StarResonanceDpsAnalysis.Core.Data;

namespace StarResonanceDpsAnalysis.Tests;

public class MessageAnalyzerTests : IDisposable
{
    private static DataStorage _dataStorage = DataStorage.Instance;
    private readonly MessageAnalyzer _messageAnalyzer = new MessageAnalyzer(_dataStorage, new EntityBuffMonitors());
    public MessageAnalyzerTests()
    {
        _dataStorage.ClearAllDpsData();
        _dataStorage.ClearAllPlayerInfos();
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

    public void Dispose()
    {
        _dataStorage.ClearAllDpsData();
        _dataStorage.ClearAllPlayerInfos();
    }
}
