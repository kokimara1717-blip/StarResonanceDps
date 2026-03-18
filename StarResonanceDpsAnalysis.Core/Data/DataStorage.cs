using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.Core.Analyze.Models;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Extends.Data;
using StarResonanceDpsAnalysis.Core.Statistics;

namespace StarResonanceDpsAnalysis.Core.Data;

/// <summary>
/// 数据存储
/// </summary>
public class DataStorage : IDataStorage, IAsyncDisposable
{
    //private static readonly StatisticsEngine _engine = new();
    private readonly StatisticsAdapter _adapter = new();
    private readonly object _sectionTimeoutLock = new();
    private Timer? _sectionTimeoutTimer;
    private DateTime _lastLogWallClockAtUtc = DateTime.MinValue;
    private bool _isSectionTimedOut;
    private int _sampleRecordingInterval = 1000;
    private bool _isSampleRecordingStarted;

    public DataStorage(ILogger<DataStorage>? logger = null)
    {
        if (logger != null)
        {
            Logger = logger;
        }
    }

    public static DataStorage Instance { get; } = new(); // Backward compatibility singleton instance for legacy code

    public long CurrentPlayerUID { get; set; }

    /// <summary>
    /// 当前玩家信息
    /// </summary>
    public PlayerInfo CurrentPlayerInfo
    {
        get
        {
            var ret = PlayerInfoDatas.GetValueOrDefault(CurrentPlayerUID);
            if (ret == null)
            {
                EnsurePlayer(CurrentPlayerUID);
                ret = PlayerInfoDatas[CurrentPlayerUID];
            }

            return ret;
        }
    }


    /// <summary>
    /// 玩家信息字典 (Key: UID)
    /// </summary>
    private Dictionary<long, PlayerInfo> PlayerInfoDatas { get; } = [];

    /// <summary>
    /// 只读玩家信息字典 (Key: UID)
    /// </summary>
    public ReadOnlyDictionary<long, PlayerInfo> ReadOnlyPlayerInfoDatas => PlayerInfoDatas.AsReadOnly();

    /// <summary>
    /// 最后一次战斗日志
    /// </summary>
    private BattleLog? LastBattleLog { get; set; }

    /// <summary>
    /// 全程玩家DPS字典 (Key: UID)
    /// </summary>
    private Dictionary<long, DpsData> FullDpsData { get; } = [];

    /// <summary>
    /// 只读全程玩家DPS字典 (Key: UID)
    /// </summary>
    public ReadOnlyDictionary<long, DpsData> ReadOnlyFullDpsData => FullDpsData.AsReadOnly();

    public IReadOnlyDictionary<long, PlayerStatistics> ReadOnlyFullPlayerStatistics =>
        _adapter.GetStatistics(true);

    /// <summary>
    /// 只读全程玩家DPS列表; 注意! 频繁读取该属性可能会导致性能问题!
    /// </summary>
    public IReadOnlyList<DpsData> ReadOnlyFullDpsDataList => FullDpsData.Values.ToList().AsReadOnly();

    /// <summary>
    /// 阶段性玩家DPS字典 (Key: UID)
    /// </summary>
    private Dictionary<long, DpsData> SectionedDpsData { get; } = [];

    /// <summary>
    /// 阶段性只读玩家DPS字典 (Key: UID)
    /// </summary>
    public ReadOnlyDictionary<long, DpsData> ReadOnlySectionedDpsData => SectionedDpsData.AsReadOnly();

    public IReadOnlyDictionary<long, PlayerStatistics> ReadOnlySectionedPlayerStatistics =>
        _adapter.GetStatistics(false);

    /// <summary>
    /// 阶段性只读玩家DPS列表; 注意! 频繁读取该属性可能会导致性能问题!
    /// </summary>
    public IReadOnlyList<DpsData> ReadOnlySectionedDpsDataList => SectionedDpsData.Values.ToList().AsReadOnly();

    /// <summary>
    /// 战斗日志分段超时时间 (默认: 5000ms)
    /// </summary>
    public TimeSpan SectionTimeout { get; set; } = TimeSpan.FromMilliseconds(5000);

    /// <summary>
    /// Sample recording interval in milliseconds
    /// </summary>
    public int SampleRecordingInterval
    {
        get => _sampleRecordingInterval;
        set
        {
            var clamped = Math.Max(1, value);
            if (_sampleRecordingInterval == clamped) return;
            Logger.LogInformation("Sample recording interval changed {oldValue} -> {newValue}",
                _sampleRecordingInterval, clamped);
            _sampleRecordingInterval = clamped;
            if (_isSampleRecordingStarted)
            {
                _adapter.StartSampleRecording(_sampleRecordingInterval);
            }
        }
    }

    public ILogger<IDataStorage> Logger { get; set; } = NullLogger<IDataStorage>.Instance;

    /// <summary>
    /// 强制新分段标记
    /// </summary>
    /// <remarks>
    /// 设置为 true 后将在下一次添加战斗日志时, 强制创建一个新的分段之后重置为 false
    /// </remarks>
    private bool ForceNewBattleSection { get; set; }

    /// <summary>
    /// 是否正在监听服务器
    /// </summary>
    public bool IsServerConnected
    {
        get;
        set
        {
            if (field == value) return;
            field = value;

            if (value)
            {
                EnsureSectionMonitorStarted();
            }

            try
            {
                ServerConnectionStateChanged?.Invoke(value);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"An error occurred during trigger event(ServerConnectionStateChanged) => {ex.Message}\r\n{ex.StackTrace}");
            }
        }
    }

    /// <summary>
    /// 服务器的监听连接状态变更事件
    /// </summary>
    public event ServerConnectionStateChangedEventHandler? ServerConnectionStateChanged;

    /// <summary>
    /// 玩家信息更新事件
    /// </summary>
    public event PlayerInfoUpdatedEventHandler? PlayerInfoUpdated;

    /// <summary>
    /// 战斗日志新分段创建事件
    /// </summary>
    public event NewSectionCreatedEventHandler? NewSectionCreated;

    /// <summary>
    /// 战斗日志更新事件
    /// </summary>
    public event BattleLogCreatedEventHandler? BattleLogCreated;

    /// <summary>
    /// DPS数据更新事件
    /// </summary>
    public event DpsDataUpdatedEventHandler? DpsDataUpdated;

    /// <summary>
    /// 数据更新事件 (玩家信息或战斗日志更新时触发)
    /// </summary>
    public event DataUpdatedEventHandler? DataUpdated;

    /// <summary>
    /// 服务器变更事件 (地图变更)
    /// </summary>
    public event ServerChangedEventHandler? ServerChanged;

    public event SectionEndedEventHandler? SectionEnded;
    public event Action? BeforeSectionCleared;
    public event BuffEffectReceivedEventHandler? BuffEffectReceived;

    /// <summary>
    /// 从文件加载缓存玩家信息
    /// </summary>
    public void LoadPlayerInfoFromFile()
    {
        PlayerInfoCacheFileV3_0_0 playerInfoCaches;
        try
        {
            playerInfoCaches = PlayerInfoCacheReader.ReadFile();
        }
        catch (FileNotFoundException e)
        {
            // 缓存文件不存在, 直接忽略
            Debug.WriteLine($"Player info cache file not found: {e.Message}");
            return;
        }

        foreach (var playerInfoCache in playerInfoCaches.PlayerInfos)
        {
            if (!PlayerInfoDatas.TryGetValue(playerInfoCache.UID, out var playerInfo))
            {
                playerInfo = new PlayerInfo()
                {
                    UID = playerInfoCache.UID,
                };
            }

            playerInfo.ProfessionID ??= playerInfoCache.ProfessionID;
            playerInfo.CombatPower ??= playerInfoCache.CombatPower;
            playerInfo.Critical ??= playerInfoCache.Critical;
            playerInfo.Lucky ??= playerInfoCache.Lucky;
            playerInfo.MaxHP ??= playerInfoCache.MaxHP;
            playerInfo.SeasonStrength ??= playerInfoCache.SeasonStrength;

            if (string.IsNullOrEmpty(playerInfo.Name))
            {
                playerInfo.Name = playerInfoCache.Name;
            }

            if (string.IsNullOrEmpty(playerInfo.SubProfessionName))
            {
                playerInfo.SubProfessionName = playerInfoCache.SubProfessionName;
            }

            PlayerInfoDatas[playerInfo.UID] = playerInfo;
        }
    }

    /// <summary>
    /// 保存缓存玩家信息到文件
    /// </summary>
    public void SavePlayerInfoToFile()
    {
        try
        {
            LoadPlayerInfoFromFile();
        }
        catch (Exception)
        {
            // 无缓存或缓存篡改直接无视重新保存新文件
        }

        var list = PlayerInfoDatas.Values.ToList();
        PlayerInfoCacheWriter.WriteToFile([.. list]);
    }

    /// <summary>
    /// 检查或创建玩家信息
    /// </summary>
    /// <param name="uid"></param>
    /// <returns>是否已经存在; 是: true, 否: false</returns>
    /// <remarks>
    /// 如果传入的 UID 已存在, 则不会进行任何操作;
    /// 否则会创建一个新的 PlayerInfo 并触发 PlayerInfoUpdated 事件
    /// </remarks>
    public bool EnsurePlayer(long uid)
    {
        /*
         * 因为修改 PlayerInfo 必须触发 PlayerInfoUpdated 事件,
         * 所以不能用 GetOrCreate 的方式来返回 PlayerInfo 对象,
         * 否则会造成外部使用 PlayerInfo 对象后没有触发事件的问题
         * * * * * * * * * * * * * * * * * * * * * * * * * * */

        if (PlayerInfoDatas.ContainsKey(uid))
        {
            return true;
        }

        PlayerInfoDatas[uid] = new PlayerInfo { UID = uid };

        TriggerPlayerInfoUpdated(uid);

        return false;
    }

    public void SetPlayerElementFlag(long playerUid, int readInt32)
    {
        throw new NotImplementedException();
    }

    public void SetPlayerReductionLevel(long playerUid, int readInt32)
    {
        throw new NotImplementedException();
    }

    public void SetPlayerEnergyFlag(long playerUid, int readInt32)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 设置当前玩家 UID，并同步到 CurrentPlayerInfo
    /// </summary>
    /// <param name="uid">当前玩家UID</param>
    public void SetCurrentPlayerUid(long uid)
    {
        if (uid == 0) return;

        var changed = CurrentPlayerUID != uid;
        CurrentPlayerUID = uid;

        var existed = EnsurePlayer(uid);

        if (changed && existed)
        {
            RaisePlayerInfoUpdated(uid);
            RaiseDataUpdated();
        }
    }

    /// <summary>
    /// 触发玩家信息更新事件
    /// </summary>
    /// <param name="uid">UID</param>
    private void TriggerPlayerInfoUpdated(long uid)
    {
        RaisePlayerInfoUpdated(uid);

        RaiseDataUpdated();
    }

    /// <summary>
    /// 检查或创建玩家战斗日志列表
    /// </summary>
    /// <param name="uid">UID</param>
    /// <returns>是否已经存在; 是: true, 否: false</returns>
    /// <remarks>
    /// 如果传入的 UID 已存在, 则不会进行任何操作;
    /// 否则会创建一个新的对应 UID 的 List&lt;BattleLog&gt;
    /// </remarks>
    internal (DpsData fullData, DpsData sectionedData) GetOrCreateDpsDataByUid(long uid)
    {
        var fullDpsData = GetOrCreate(FullDpsData, uid, () => new DpsData { UID = uid });
        var sectionedDpsData = GetOrCreate(SectionedDpsData, uid, () => new DpsData { UID = uid });

        return (fullDpsData, sectionedDpsData);

        // helper to reduce repetition
        static TValue GetOrCreate<TValue>(Dictionary<long, TValue> dict, long key, Func<TValue> factory)
            where TValue : class
        {
            if (!dict.TryGetValue(key, out var value))
            {
                value = factory();
                dict[key] = value;
            }

            return value;
        }
    }

    /// <summary>
    /// 添加战斗日志 (会自动创建日志分段)
    /// </summary>
    /// <param name="log">战斗日志</param>
    public void AddBattleLog(BattleLog log)
    {
        // 当前封包时间
        var tt = new TimeSpan(log.TimeTicks);
        // 是否创建新战斗分段标记
        var sectionFlag = false;
        if (LastBattleLog != null)
        {
            // 如果 战斗超时 或 强制创建新战斗分段 时, 创建新分段
            var prevTt = new TimeSpan(LastBattleLog.TimeTicks);
            if (tt - prevTt > SectionTimeout || ForceNewBattleSection)
            {
                sectionFlag = true;
                ForceNewBattleSection = false;
            }
        }
        else
        {
            sectionFlag = true;
        }

        // 如果创建新战斗分段
        if (sectionFlag)
        {
            var shouldKeepCombatState = false;
            if (CurrentPlayerUID != 0 && PlayerInfoDatas.TryGetValue(CurrentPlayerUID, out var currentPlayerInfo))
            {
                shouldKeepCombatState = currentPlayerInfo.CombatState;
            }

            try
            {
                RaiseBeforeSectionCleared();
                PrivateClearSectionDpsData();
                if (shouldKeepCombatState)
                {
                    _adapter.SetCombatState(true);
                }

                RaiseNewSectionCreated();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"An error occurred during trigger event(NewSectionCreated) => {ex.Message}\r\n{ex.StackTrace}");
            }
        }

        // 如果目标是玩家
        if (log.IsTargetPlayer)
        {
            // 如果是治疗数据包
            if (log.IsHeal)
            {
                // 设置通用基础信息
                var (fullData, sectionedData) = SetLogInfos(log.AttackerUuid, log);

                // 尝试通过技能ID设置对应副职
                TrySetSubProfessionBySkillId(log.AttackerUuid, log.SkillID);

                // 叠加治疗量
                fullData.AddTotalHeal(log.Value);
                sectionedData.AddTotalHeal(log.Value);
            }
            else
            {
                // 设置通用基础信息
                var (fullData, sectionedData) = SetLogInfos(log.TargetUuid, log);

                // 叠加受击伤害
                fullData.AddTotalTakenDamage(log.Value);
                sectionedData.AddTotalTakenDamage(log.Value);
            }
        }
        // 如果目标不是玩家
        else
        {
            // 不是 治疗数据包 且 攻击者是玩家
            if (!log.IsHeal && log.IsAttackerPlayer)
            {
                // 设置通用基础信息
                var (fullData, sectionedData) = SetLogInfos(log.AttackerUuid, log);

                // 尝试通过技能ID设置对应副职
                TrySetSubProfessionBySkillId(log.AttackerUuid, log.SkillID);

                // 叠加输出伤害
                fullData.AddTotalAttackDamage(log.Value);
                sectionedData.AddTotalAttackDamage(log.Value);
            }

            // 提升局部, 统一局部变量名
            {
                // 设置通用基础信息
                var (fullData, sectionedData) = SetLogInfos(log.TargetUuid, log);

                // 叠加受击伤害
                fullData.AddTotalTakenDamage(log.Value);
                sectionedData.AddTotalTakenDamage(log.Value);

                // 将Dps数据记录为NPC数据
                fullData.IsNpcData = true;
                sectionedData.IsNpcData = true;
            }
        }

        _adapter.ProcessLog(log);
        // 最后一个日志赋值
        UpdateLastLogState(log);

        EnsureSampleRecordingStarted();

        RaiseBattleLogCreated(log);
        RaiseDpsDataUpdated();
        RaiseDataUpdated();
    }

    private void EnsureSectionMonitorStarted()
    {
        if (_sectionTimeoutTimer != null) return;
        _sectionTimeoutTimer = new Timer(_ => CheckSectionTimeout(), null, TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1));
    }

    private void CheckSectionTimeout()
    {
        DateTime last;
        bool alreadyTimedOut;

        lock (_sectionTimeoutLock)
        {
            last = _lastLogWallClockAtUtc;
            alreadyTimedOut = _isSectionTimedOut;
        }

        if (alreadyTimedOut) return;
        if (last == DateTime.MinValue) return;

        if (DateTime.UtcNow - last <= SectionTimeout) return;

        if (_adapter.GetStatisticsCount(false) == 0)
        {
            lock (_sectionTimeoutLock)
            {
                _isSectionTimedOut = true;
            }

            return;
        }

        lock (_sectionTimeoutLock)
        {
            _isSectionTimedOut = true;
            ForceNewBattleSection = true;
        }

        RaiseSectionEnded();
    }

    private void EnsureSampleRecordingStarted()
    {
        if (_isSampleRecordingStarted) return;
        _adapter.StartSampleRecording(_sampleRecordingInterval);
        _isSampleRecordingStarted = true;
    }

    private void UpdateLastLogState(BattleLog log)
    {
        LastBattleLog = log;

        lock (_sectionTimeoutLock)
        {
            _lastLogWallClockAtUtc = DateTime.UtcNow;
            _isSectionTimedOut = false;
        }

        EnsureSectionMonitorStarted();
    }

    /// <summary>
    /// 设置通用基础信息
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="log"></param>
    /// <returns></returns>
    private (DpsData fullData, DpsData sectionedData) SetLogInfos(long uid, BattleLog log)
    {
        // 检查或创建玩家信息
        EnsurePlayer(uid);

        // 检查或创建玩家战斗日志列表
        var (fullData, sectionedData) = GetOrCreateDpsDataByUid(uid);

        fullData.StartLoggedTick ??= log.TimeTicks;
        fullData.LastLoggedTick = log.TimeTicks;

        fullData.UpdateSkillData(log.SkillID, skillData =>
        {
            skillData.TotalValue += log.Value;
            skillData.UseTimes += 1;
            skillData.CritTimes += log.IsCritical ? 1 : 0;
            skillData.LuckyTimes += log.IsLucky ? 1 : 0;
        });

        sectionedData.StartLoggedTick ??= log.TimeTicks;
        sectionedData.LastLoggedTick = log.TimeTicks;

        sectionedData.UpdateSkillData(log.SkillID, skillData =>
        {
            skillData.TotalValue += log.Value;
            skillData.UseTimes += 1;
            skillData.CritTimes += log.IsCritical ? 1 : 0;
            skillData.LuckyTimes += log.IsLucky ? 1 : 0;
        });

        return (fullData, sectionedData);
    }

    private void TrySetSubProfessionBySkillId(long uid, long skillId)
    {
        if (!PlayerInfoDatas.TryGetValue(uid, out var playerInfo))
        {
            return;
        }

        var subProfessionName = skillId.GetSubProfessionBySkillId();
        var spec = skillId.GetClassSpecBySkillId();
        if (!string.IsNullOrEmpty(subProfessionName))
        {
            playerInfo.SubProfessionName = subProfessionName;
            playerInfo.Spec = spec;
            TriggerPlayerInfoUpdated(uid);
        }
    }

    /// <summary>
    /// 通过战斗日志构建玩家信息字典
    /// </summary>
    /// <param name="battleLogs">战斗日志</param>
    /// <returns></returns>
    public Dictionary<long, PlayerInfoFileData> BuildPlayerDicFromBattleLog(List<BattleLog> battleLogs)
    {
        var playerDic = new Dictionary<long, PlayerInfoFileData>();
        foreach (var log in battleLogs)
        {
            if (!playerDic.ContainsKey(log.AttackerUuid) &&
                PlayerInfoDatas.TryGetValue(log.AttackerUuid, out var attackerPlayerInfo))
            {
                playerDic.Add(log.AttackerUuid, attackerPlayerInfo);
            }

            if (!playerDic.ContainsKey(log.TargetUuid) &&
                PlayerInfoDatas.TryGetValue(log.TargetUuid, out var targetPlayerInfo))
            {
                playerDic.Add(log.TargetUuid, targetPlayerInfo);
            }
        }

        return playerDic;
    }

    /// <summary>
    /// 清除所有DPS数据 (包括全程和阶段性)
    /// </summary>
    public void ClearAllDpsData()
    {
        ForceNewBattleSection = true;

        // Preserve current section logs before full clear.
        if (_adapter.GetStatisticsCount(false) > 0)
        {
            RaiseBeforeSectionCleared();
        }

        SectionedDpsData.Clear();
        FullDpsData.Clear();
        _adapter.ClearAll();

        RaiseDpsDataUpdated();
        RaiseDataUpdated();
    }

    public void ClearDpsData()
    {
        ClearSectionDpsData();
    }

    private void PrivateClearSectionDpsData()
    {
        SectionedDpsData.Clear();
        _adapter.ResetSection();
    }

    /// <summary>
    /// 标记新的战斗日志分段 (清空阶段性Dps数据)
    /// </summary>
    public void ClearSectionDpsData()
    {
        ForceNewBattleSection = true;

        // Preserve current section logs before manual clear.
        if (_adapter.GetStatisticsCount(false) > 0)
        {
            RaiseBeforeSectionCleared();
        }

        PrivateClearSectionDpsData();

        try
        {
            DpsDataUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"An error occurred during trigger event(DpsDataUpdated) => {ex.Message}\r\n{ex.StackTrace}");
        }

        try
        {
            DataUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"An error occurred during trigger event(DataUpdated) => {ex.Message}\r\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// 清除所有玩家信息
    /// </summary>
    public void ClearPlayerInfos()
    {
        PlayerInfoDatas.Clear();

        try
        {
            DataUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"An error occurred during trigger event(DataUpdated) => {ex.Message}\r\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// 清除所有数据 (包括缓存历史)
    /// </summary>
    public void ClearAllPlayerInfos()
    {
        PlayerInfoDatas.Clear();

        try
        {
            DataUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"An error occurred during trigger event(DataUpdated) => {ex.Message}\r\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// Notifies that buff effect data has been received for an entity.
    /// </summary>
    /// <param name="entityUid">Entity UID</param>
    /// <param name="buffResult">Raw buff effect payload</param>
    public void NotifyBuffEffectReceived(long entityUid, BuffProcessResult buffResult)
    {
        RaiseBuffEffectReceived(entityUid, buffResult);
    }

    private void RaiseBuffEffectReceived(long entityUid, BuffProcessResult payload)
    {
        try
        {
            BuffEffectReceived?.Invoke(entityUid, payload);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"An error occurred during trigger event(BuffEffectReceived) => {ex.Message}\r\n{ex.StackTrace}");
        }
    }

    #region SetPlayerProperties

    /// <summary>
    /// 设置玩家名称
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="name">玩家名称</param>
    public void SetPlayerName(long uid, string name)
    {
        EnsurePlayer(uid);
        PlayerInfoDatas[uid].Name = name;

        TriggerPlayerInfoUpdated(uid);
    }

    /// <summary>
    /// 设置玩家职业ID
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="professionId">职业ID</param>
    public void SetPlayerProfessionID(long uid, int professionId)
    {
        EnsurePlayer(uid);
        PlayerInfoDatas[uid].ProfessionID = professionId;

        TriggerPlayerInfoUpdated(uid);
    }

    /// <summary>
    /// 设置玩家战力
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="combatPower">战力</param>
    public void SetPlayerCombatPower(long uid, int combatPower)
    {
        EnsurePlayer(uid);
        PlayerInfoDatas[uid].CombatPower = combatPower;

        TriggerPlayerInfoUpdated(uid);
    }

    public void SetPlayerCombatState(long uid, bool combatState)
    {
        EnsurePlayer(uid);
        var prev = PlayerInfoDatas[uid].CombatState;
        if (prev == combatState) return;
        Debug.WriteLine($"PlayerCombatState:[{uid}] {prev} -> {combatState}");
        PlayerInfoDatas[uid].CombatState = combatState;
        if (CurrentPlayerUID != 0) _adapter.SetCombatState(combatState);
        TriggerPlayerInfoUpdated(uid);
    }

    /// <summary>
    /// 设置玩家等级
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="level">等级</param>
    public void SetPlayerLevel(long uid, int level)
    {
        EnsurePlayer(uid);
        PlayerInfoDatas[uid].Level = level;

        TriggerPlayerInfoUpdated(uid);
    }

    /// <summary>
    /// 设置玩家 RankLevel
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="rankLevel">RankLevel</param>
    /// <remarks>
    /// 暂不清楚 RankLevel 的具体含义...
    /// </remarks>
    public void SetPlayerRankLevel(long uid, int rankLevel)
    {
        EnsurePlayer(uid);
        PlayerInfoDatas[uid].RankLevel = rankLevel;

        TriggerPlayerInfoUpdated(uid);
    }

    public void SetPlayerGuild(long uid, string guild)
    {
        EnsurePlayer(uid);
        PlayerInfoDatas[uid].Guild = guild;
        TriggerPlayerInfoUpdated(uid);
    }

    /// <summary>
    /// 设置玩家暴击
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="critical">暴击值</param>
    public void SetPlayerCritical(long uid, int critical)
    {
        EnsurePlayer(uid);
        PlayerInfoDatas[uid].Critical = critical;

        TriggerPlayerInfoUpdated(uid);
    }

    /// <summary>
    /// 设置玩家幸运
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="lucky">幸运值</param>
    public void SetPlayerLucky(long uid, int lucky)
    {
        EnsurePlayer(uid);
        PlayerInfoDatas[uid].Lucky = lucky;

        TriggerPlayerInfoUpdated(uid);
    }

    /// <summary>
    /// 设置玩家当前HP
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="hp">当前HP</param>
    public void SetPlayerHP(long uid, long hp)
    {
        EnsurePlayer(uid);
        PlayerInfoDatas[uid].HP = hp;

        TriggerPlayerInfoUpdated(uid);
    }

    /// <summary>
    /// 设置玩家最大HP
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="maxHp">最大HP</param>
    public void SetPlayerMaxHP(long uid, long maxHp)
    {
        EnsurePlayer(uid);
        PlayerInfoDatas[uid].MaxHP = maxHp;

        TriggerPlayerInfoUpdated(uid);
    }

    public void SetPlayerSeasonLevel(long uid, int seasonLevel)
    {
        PlayerInfoDatas[uid].SeasonLevel = seasonLevel;
        TriggerPlayerInfoUpdated(uid);
    }

    public void SetPlayerSeasonStrength(long uid, int seasonStrength)
    {
        EnsurePlayer(uid);
        PlayerInfoDatas[uid].SeasonStrength = seasonStrength;
        TriggerPlayerInfoUpdated(uid);
    }

    public void SetNpcTemplateId(long playerUid, int templateId)
    {
        EnsurePlayer(playerUid);
        PlayerInfoDatas[playerUid].NpcTemplateId = templateId;
        TriggerPlayerInfoUpdated(playerUid);
    }

    public void SetPlayerCombatStateTime(long uid, long time)
    {
        EnsurePlayer(uid);
        PlayerInfoDatas[uid].CombatStateTime = time;
        TriggerPlayerInfoUpdated(uid);
    }

    private void RaiseSectionEnded()
    {
        try
        {
            SectionEnded?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"An error occurred during trigger event(SectionEnded) => {ex.Message}\r\n{ex.StackTrace}");
        }
    }

    private void RaiseNewSectionCreated()
    {
        try
        {
            NewSectionCreated?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"An error occurred during trigger event(NewSectionStarted) => {ex.Message}\r\n{ex.StackTrace}");
        }
    }

    private void RaiseBeforeSectionCleared()
    {
        BeforeSectionCleared?.Invoke();
    }

    private void RaiseDpsDataUpdated()
    {
        try
        {
            DpsDataUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"An error occurred during trigger event(DpsDataUpdated) => {ex.Message}\r\n{ex.StackTrace}");
        }
    }

    private void RaiseDataUpdated()
    {
        try
        {
            DataUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"An error occurred during trigger event(DataUpdated) => {ex.Message}\r\n{ex.StackTrace}");
        }
    }

    private void RaisePlayerInfoUpdated(long uid)
    {
        try
        {
            PlayerInfoUpdated?.Invoke(PlayerInfoDatas[uid]);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"An error occurred during trigger event(PlayerInfoUpdated) => {ex.Message}\r\n{ex.StackTrace}");
        }
    }

    private void RaiseBattleLogCreated(BattleLog log)
    {
        try
        {
            // 触发战斗日志创建事件
            BattleLogCreated?.Invoke(log);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"An error occurred during trigger event(BattleLogCreated) => {ex.Message}\r\n{ex.StackTrace}");
        }
    }

    private void RaiseServerChanged(string currentServer, string prevServer)
    {
        try
        {
            ServerChanged?.Invoke(currentServer, prevServer);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"An error occurred during trigger event(ServerChanged) => {ex.Message}\r\n{ex.StackTrace}");
        }
    }

    public void ServerChange(string curr, string prev)
    {
        RaiseServerChanged(curr, prev);
    }

    #endregion

    public IReadOnlyDictionary<long, PlayerStatistics> GetStatistics(bool fullSession)
    {
        return _adapter.GetStatistics(fullSession);
    }

    public IReadOnlyList<BattleLog> GetBattleLogs(bool fullSession)
    {
        return _adapter.GetBattleLogs(fullSession);
    }

    public IReadOnlyList<BattleLog> GetBattleLogsForPlayer(long uid, bool fullSession)
    {
        return _adapter.GetBattleLogsForPlayer(uid, fullSession);
    }

    public int GetStatisticsCount(bool fullSession)
    {
        return _adapter.GetStatisticsCount(fullSession);
    }

    public void Dispose()
    {
        _sectionTimeoutTimer?.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_sectionTimeoutTimer != null) await _sectionTimeoutTimer.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}