using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Xml;
using StarResonanceDpsAnalysis.Core.Analyze.Models;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Statistics;

namespace StarResonanceDpsAnalysis.Core.Data;

/// <summary>
/// 为了避免重复创建对象而实例化的存储类, 仅用于存储数据
/// 镜像 DataStorage 的公有成员/方法并将调用转发到静态 DataStorage
/// </summary>
public class InstantizedDataStorage : IDataStorage, IDisposable
{
    private readonly object _battleLogCreatedLock = new();
    private readonly Dictionary<Delegate, Delegate> _battleLogCreatedMap = new();

    private readonly object _dataUpdatedLock = new();
    private readonly Dictionary<Delegate, Delegate> _dataUpdatedMap = new();

    private readonly object _dpsDataUpdatedLock = new();
    private readonly Dictionary<Delegate, Delegate> _dpsDataUpdatedMap = new();

    private readonly object _newSectionCreatedLock = new();
    private readonly Dictionary<Delegate, Delegate> _newSectionCreatedMap = new();

    private readonly object _sectionEndedLock = new();
    private readonly Dictionary<Delegate, Delegate> _sectionEndedMap = new();

    private readonly object _playerInfoUpdatedLock = new();
    private readonly Dictionary<Delegate, Delegate> _playerInfoUpdatedMap = new();

    private readonly object _serverChangedLock = new();
    private readonly Dictionary<Delegate, Delegate> _serverChangedMap = new();

    // Event handler mappings for proper unsubscribe
    private readonly object _serverConnLock = new();
    private readonly Dictionary<Delegate, Delegate> _serverConnMap = new();
    private bool _disposed;

    // Properties (forwarding to DataStorage)
    public PlayerInfo CurrentPlayerInfo => DataStorage.Instance.CurrentPlayerInfo;

    public ReadOnlyDictionary<long, PlayerInfo> ReadOnlyPlayerInfoDatas => DataStorage.Instance.ReadOnlyPlayerInfoDatas;

    public ReadOnlyDictionary<long, DpsData> ReadOnlyFullDpsDatas => DataStorage.Instance.ReadOnlyFullDpsData;

    public IReadOnlyList<DpsData> ReadOnlyFullDpsDataList => DataStorage.Instance.ReadOnlyFullDpsDataList;

    public ReadOnlyDictionary<long, DpsData> ReadOnlySectionedDpsDatas => DataStorage.Instance.ReadOnlySectionedDpsData;

    public IReadOnlyList<DpsData> ReadOnlySectionedDpsDataList => DataStorage.Instance.ReadOnlySectionedDpsDataList;

    public TimeSpan SectionTimeout
    {
        get => DataStorage.Instance.SectionTimeout;
        set => DataStorage.Instance.SectionTimeout = value;
    }

    // DataStorage.Instance.IsServerConnected has public getter and internal setter; expose getter only.
    public bool IsServerConnected
    {
        get => DataStorage.Instance.IsServerConnected;
        set => DataStorage.Instance.IsServerConnected = value;
    }

    public int SampleRecordingInterval
    {
        get => DataStorage.Instance.SampleRecordingInterval;
        set => DataStorage.Instance.SampleRecordingInterval = value;
    }

    // Dispose: detach all wrappers from DataStorage static events
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // ServerConnection
        lock (_serverConnLock)
        {
            foreach (var wrapper in _serverConnMap.Values)
            {
                DataStorage.Instance.ServerConnectionStateChanged -=
                    (ServerConnectionStateChangedEventHandler)wrapper!;
            }

            _serverConnMap.Clear();
        }

        // PlayerInfoUpdated
        lock (_playerInfoUpdatedLock)
        {
            foreach (var wrapper in _playerInfoUpdatedMap.Values)
            {
                DataStorage.Instance.PlayerInfoUpdated -= (PlayerInfoUpdatedEventHandler)wrapper!;
            }

            _playerInfoUpdatedMap.Clear();
        }

        // NewSectionCreated
        lock (_newSectionCreatedLock)
        {
            foreach (var wrapper in _newSectionCreatedMap.Values)
            {
                DataStorage.Instance.NewSectionCreated -= (NewSectionCreatedEventHandler)wrapper!;
            }

            _newSectionCreatedMap.Clear();
        }

        // BattleLogCreated
        lock (_battleLogCreatedLock)
        {
            foreach (var wrapper in _battleLogCreatedMap.Values)
            {
                DataStorage.Instance.BattleLogCreated -= (BattleLogCreatedEventHandler)wrapper!;
            }

            _battleLogCreatedMap.Clear();
        }

        // DpsDataUpdated
        lock (_dpsDataUpdatedLock)
        {
            foreach (var wrapper in _dpsDataUpdatedMap.Values)
            {
                DataStorage.Instance.DpsDataUpdated -= (DpsDataUpdatedEventHandler)wrapper!;
            }

            _dpsDataUpdatedMap.Clear();
        }

        // DataUpdated
        lock (_dataUpdatedLock)
        {
            foreach (var wrapper in _dataUpdatedMap.Values)
            {
                DataStorage.Instance.DataUpdated -= (DataUpdatedEventHandler)wrapper!;
            }

            _dataUpdatedMap.Clear();
        }

        // ServerChanged
        lock (_serverChangedLock)
        {
            foreach (var wrapper in _serverChangedMap.Values)
            {
                DataStorage.Instance.ServerChanged -= (ServerChangedEventHandler)wrapper!;
            }

            _serverChangedMap.Clear();
        }

        GC.SuppressFinalize(this);
    }

    // Events: add/remove will subscribe/unsubscribe corresponding static events.
    public event ServerConnectionStateChangedEventHandler? ServerConnectionStateChanged
    {
        add
        {
            if (value is null) return;
            lock (_serverConnLock)
            {
                if (_serverConnMap.ContainsKey(value)) return;
                ServerConnectionStateChangedEventHandler wrapper = s => value(s);
                _serverConnMap.Add(value, wrapper);
                DataStorage.Instance.ServerConnectionStateChanged += wrapper;
            }
        }
        remove
        {
            if (value is null) return;
            lock (_serverConnLock)
            {
                if (_serverConnMap.TryGetValue(value, out var wrapper))
                {
                    DataStorage.Instance.ServerConnectionStateChanged -=
                        (ServerConnectionStateChangedEventHandler)wrapper!;
                    _serverConnMap.Remove(value);
                }
            }
        }
    }

    public event PlayerInfoUpdatedEventHandler? PlayerInfoUpdated
    {
        add
        {
            if (value is null) return;
            lock (_playerInfoUpdatedLock)
            {
                if (_playerInfoUpdatedMap.ContainsKey(value)) return;
                PlayerInfoUpdatedEventHandler wrapper = p => value(p);
                _playerInfoUpdatedMap.Add(value, wrapper);
                DataStorage.Instance.PlayerInfoUpdated += wrapper;
            }
        }
        remove
        {
            if (value is null) return;
            lock (_playerInfoUpdatedLock)
            {
                if (_playerInfoUpdatedMap.TryGetValue(value, out var wrapper))
                {
                    DataStorage.Instance.PlayerInfoUpdated -= (PlayerInfoUpdatedEventHandler)wrapper!;
                    _playerInfoUpdatedMap.Remove(value);
                }
            }
        }
    }

    public event NewSectionCreatedEventHandler? NewSectionCreated
    {
        add
        {
            if (value is null) return;
            lock (_newSectionCreatedLock)
            {
                if (_newSectionCreatedMap.ContainsKey(value)) return;
                NewSectionCreatedEventHandler wrapper = () => value();
                _newSectionCreatedMap.Add(value, wrapper);
                DataStorage.Instance.NewSectionCreated += wrapper;
            }
        }
        remove
        {
            if (value is null) return;
            lock (_newSectionCreatedLock)
            {
                if (_newSectionCreatedMap.TryGetValue(value, out var wrapper))
                {
                    DataStorage.Instance.NewSectionCreated -= (NewSectionCreatedEventHandler)wrapper!;
                    _newSectionCreatedMap.Remove(value);
                }
            }
        }
    }

    public event SectionEndedEventHandler? SectionEnded
    {
        add
        {
            if (value is null) return;
            lock (_sectionEndedLock)
            {
                if (_sectionEndedMap.ContainsKey(value)) return;
                SectionEndedEventHandler wrapper = () => value();
                _sectionEndedMap.Add(value, wrapper);
                DataStorage.Instance.SectionEnded += wrapper;
            }

        }
        remove
        {
            if (value is null) return;
            lock (_sectionEndedLock)
            {
                if (_sectionEndedMap.TryGetValue(value, out var wrapper))
                {
                    DataStorage.Instance.SectionEnded -= (SectionEndedEventHandler)wrapper!;
                    _sectionEndedMap.Remove(value);
                }
            }
        }
    }

    public event BattleLogCreatedEventHandler? BattleLogCreated
    {
        add
        {
            if (value is null) return;
            lock (_battleLogCreatedLock)
            {
                if (_battleLogCreatedMap.ContainsKey(value)) return;
                BattleLogCreatedEventHandler wrapper = b => value(b);
                _battleLogCreatedMap.Add(value, wrapper);
                DataStorage.Instance.BattleLogCreated += wrapper;
            }
        }
        remove
        {
            if (value is null) return;
            lock (_battleLogCreatedLock)
            {
                if (_battleLogCreatedMap.TryGetValue(value, out var wrapper))
                {
                    DataStorage.Instance.BattleLogCreated -= (BattleLogCreatedEventHandler)wrapper!;
                    _battleLogCreatedMap.Remove(value);
                }
            }
        }
    }

    public event DpsDataUpdatedEventHandler? DpsDataUpdated
    {
        add
        {
            if (value is null) return;
            lock (_dpsDataUpdatedLock)
            {
                if (_dpsDataUpdatedMap.ContainsKey(value)) return;
                DpsDataUpdatedEventHandler wrapper = () => value();
                _dpsDataUpdatedMap.Add(value, wrapper);
                DataStorage.Instance.DpsDataUpdated += wrapper;
            }
        }
        remove
        {
            if (value is null) return;
            lock (_dpsDataUpdatedLock)
            {
                if (_dpsDataUpdatedMap.TryGetValue(value, out var wrapper))
                {
                    DataStorage.Instance.DpsDataUpdated -= (DpsDataUpdatedEventHandler)wrapper!;
                    _dpsDataUpdatedMap.Remove(value);
                }
            }
        }
    }

    public event DataUpdatedEventHandler? DataUpdated
    {
        add
        {
            if (value is null) return;
            lock (_dataUpdatedLock)
            {
                if (_dataUpdatedMap.ContainsKey(value)) return;
                DataUpdatedEventHandler wrapper = () => value();
                _dataUpdatedMap.Add(value, wrapper);
                DataStorage.Instance.DataUpdated += wrapper;
            }
        }
        remove
        {
            if (value is null) return;
            lock (_dataUpdatedLock)
            {
                if (_dataUpdatedMap.TryGetValue(value, out var wrapper))
                {
                    DataStorage.Instance.DataUpdated -= (DataUpdatedEventHandler)wrapper!;
                    _dataUpdatedMap.Remove(value);
                }
            }
        }
    }

    public event ServerChangedEventHandler? ServerChanged
    {
        add
        {
            if (value is null) return;
            lock (_serverChangedLock)
            {
                if (_serverChangedMap.ContainsKey(value)) return;
                ServerChangedEventHandler wrapper = (cur, prev) => value(cur, prev);
                _serverChangedMap.Add(value, wrapper);
                DataStorage.Instance.ServerChanged += wrapper;
            }
        }
        remove
        {
            if (value is null) return;
            lock (_serverChangedLock)
            {
                if (_serverChangedMap.TryGetValue(value, out var wrapper))
                {
                    DataStorage.Instance.ServerChanged -= (ServerChangedEventHandler)wrapper!;
                    _serverChangedMap.Remove(value);
                }
            }
        }
    }

    public event Action? BeforeSectionCleared
    {
        add => DataStorage.Instance.BeforeSectionCleared += value;
        remove => DataStorage.Instance.BeforeSectionCleared -= value;
    }


    // Public methods (forward to DataStorage)
    public void LoadPlayerInfoFromFile()
    {
        DataStorage.Instance.LoadPlayerInfoFromFile();
    }

    public void SavePlayerInfoToFile()
    {
        DataStorage.Instance.SavePlayerInfoToFile();
    }

    public Dictionary<long, PlayerInfoFileData> BuildPlayerDicFromBattleLog(List<BattleLog> battleLogs)
    {
        return DataStorage.Instance.BuildPlayerDicFromBattleLog(battleLogs);
    }

    public void ClearAllDpsData()
    {
        DataStorage.Instance.ClearAllDpsData();
    }

    public void ClearDpsData()
    {
        DataStorage.Instance.ClearSectionDpsData();
    }

    public void ClearCurrentPlayerInfo()
    {
        DataStorage.Instance.ClearCurrentPlayerInfo();
    }

    public void ClearPlayerInfos()
    {
        DataStorage.Instance.ClearPlayerInfos();
    }

    public void ClearAllPlayerInfos()
    {
        DataStorage.Instance.ClearAllPlayerInfos();
    }

    public void ServerChange(string currentServerStr, string prevServer)
    {
        DataStorage.Instance.ServerChange(currentServerStr, prevServer);
    }

    public void SetPlayerLevel(long playerUid, int tmpLevel)
    {
        EnsurePlayer(playerUid);
        DataStorage.Instance.SetPlayerLevel(playerUid, tmpLevel);
    }

    public bool EnsurePlayer(long playerUid)
    {
        return DataStorage.Instance.EnsurePlayer(playerUid);
    }

    public void SetPlayerHP(long playerUid, long hp)
    {
        EnsurePlayer(playerUid);
        DataStorage.Instance.SetPlayerHP(playerUid, hp);
    }

    public void SetPlayerMaxHP(long playerUid, long maxHp)
    {
        EnsurePlayer(playerUid);
        DataStorage.Instance.SetPlayerMaxHP(playerUid, maxHp);
    }

    public void SetPlayerCombatState(long uid, bool combatState)
    {
        EnsurePlayer(uid);
        DataStorage.Instance.SetPlayerCombatState(uid, combatState);
    }

    public void SetPlayerName(long playerUid, string playerName)
    {
        EnsurePlayer(playerUid);
        DataStorage.Instance.SetPlayerName(playerUid, playerName);
    }

    public void SetPlayerCombatPower(long playerUid, int combatPower)
    {
        EnsurePlayer(playerUid);
        DataStorage.Instance.ReadOnlyPlayerInfoDatas[playerUid].CombatPower = combatPower;
    }

    public void SetPlayerProfessionID(long playerUid, int professionId)
    {
        EnsurePlayer(playerUid);
        DataStorage.Instance.ReadOnlyPlayerInfoDatas[playerUid].ProfessionID = professionId;
    }

    public void AddBattleLog(BattleLog log)
    {
        DataStorage.Instance.AddBattleLog(log);
    }

    public void SetPlayerRankLevel(long playerUid, int readInt32)
    {
        EnsurePlayer(playerUid);
        DataStorage.Instance.SetPlayerRankLevel(playerUid, readInt32);
    }

    public void SetPlayerCritical(long playerUid, int readInt32)
    {
        EnsurePlayer(playerUid);
        DataStorage.Instance.SetPlayerCritical(playerUid, readInt32);
    }

    public void SetPlayerLucky(long playerUid, int readInt32)
    {
        EnsurePlayer(playerUid);
        DataStorage.Instance.SetPlayerLucky(playerUid, readInt32);
    }

    public void SetPlayerElementFlag(long playerUid, int readInt32)
    {
        EnsurePlayer(playerUid);
        DataStorage.Instance.ReadOnlyPlayerInfoDatas[playerUid].ElementFlag = readInt32;
    }

    public void SetPlayerReductionLevel(long playerUid, int readInt32)
    {
        EnsurePlayer(playerUid);
        DataStorage.Instance.ReadOnlyPlayerInfoDatas[playerUid].ReductionLevel = readInt32;
    }

    public void SetPlayerEnergyFlag(long playerUid, int readInt32)
    {
        EnsurePlayer(playerUid);
        DataStorage.Instance.ReadOnlyPlayerInfoDatas[playerUid].EnergyFlag = readInt32;
    }

    public void SetNpcTemplateId(long playerUid, int templateId)
    {
        EnsurePlayer(playerUid);
        DataStorage.Instance.SetNpcTemplateId(playerUid, templateId);
    }

    public void SetPlayerSeasonLevel(long playerUid, int seasonLevel)
    {
        EnsurePlayer(playerUid);
        DataStorage.Instance.SetPlayerSeasonLevel(playerUid, seasonLevel);
    }

    public void SetPlayerSeasonStrength(long playerUid, int seasonStrength)
    {
        EnsurePlayer(playerUid);
        DataStorage.Instance.SetPlayerSeasonStrength(playerUid, seasonStrength);
    }

    public void SetPlayerGuild(long playerUid, string guild)
    {
        EnsurePlayer(playerUid);
        DataStorage.Instance.SetPlayerGuild(playerUid, guild);
    }

    public void SetCurrentPlayerUid(long playerUid)
    {
        EnsurePlayer(playerUid);
        DataStorage.Instance.SetCurrentPlayerUid(playerUid);
    }

    public IReadOnlyList<BattleLog> GetBattleLogsForPlayer(long uid, bool fullSession)
    {
        return DataStorage.Instance.GetBattleLogsForPlayer(uid, fullSession);
    }

    public IReadOnlyList<BattleLog> GetBattleLogs(bool fullSession)
    {
        return DataStorage.Instance.GetBattleLogs(fullSession);
    }

    public IReadOnlyDictionary<long, PlayerStatistics> GetStatistics(bool fullSession)
    {
        return DataStorage.Instance.GetStatistics(fullSession);
    }

    public int GetStatisticsCount(bool fullSession)
    {
        return DataStorage.Instance.GetStatisticsCount(fullSession);
    }

    public void SetPlayerCombatStateTime(long uid, long time)
    {
        EnsurePlayer(uid);
        DataStorage.Instance.SetPlayerCombatStateTime(uid, time);
    }
}