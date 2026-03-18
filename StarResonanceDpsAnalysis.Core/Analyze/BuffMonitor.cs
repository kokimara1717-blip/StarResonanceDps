using System.Diagnostics;
using Google.Protobuf;
using StarResonanceDpsAnalysis.Core.Data.Models;
using Zproto;

namespace StarResonanceDpsAnalysis.Core.Analyze;

/// <summary>
/// Monitors buff states for a single entity.
/// </summary>
public sealed class BuffMonitor
{
    /// <summary>
    /// Set of buff base IDs that should be monitored globally.
    /// </summary>
    public HashSet<int> MonitoredBuffIds { get; set; } = [];

    /// <summary>
    /// Set of buff base IDs that should only be monitored when applied by the local player.
    /// </summary>
    public HashSet<int> SelfAppliedBuffIds { get; set; } = [];

    /// <summary>
    /// Currently active buffs keyed by buff UUID.
    /// </summary>
    public Dictionary<int, ActiveBuff> ActiveBuffs { get; } = [];

    /// <summary>
    /// If true, monitors all buffs regardless of configuration.
    /// </summary>
    public bool MonitorAllBuff { get; set; }

    public BuffProcessResult ProcessBuffInfo(BuffInfoSync infoSync, ref long serverClockOffset, long localPlayerUid)
    {
        var changes = new List<BuffChangeEvent>();

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long offset = 0;
        var update = infoSync.BuffInfos.Select(info =>
        {
            if (info.HasCreateTime)
            {
                offset = now - info.CreateTime;
            }
            var duration = info.HasDuration ? (int)info.Duration : 0;
            var layer = info.HasLayer ? info.Layer : 1;
            return new BuffUpdateState()
            {
                BaseId = info.BaseId,
                CreateTimeMs = info.HasCreateTime ? info.CreateTime : now,
                DurationMs = duration,
                Layer = layer
            };
        }).ToList();

        serverClockOffset = offset;
        return new BuffProcessResult()
        {
            UpdatePayload = update,
            Changes = changes,
        };
    }

    /// <summary>
    /// Processes buff effect sync data from the game server.
    /// </summary>
    /// <param name="buffEffect">BuffEffectSync message.</param>
    /// <param name="serverClockOffset">Reference to server clock offset (updated during processing).</param>
    /// <param name="localPlayerUid">UID of the local player (for self-applied buff filtering).</param>
    /// <returns>Result containing buff updates and change events.</returns>
    public BuffProcessResult ProcessBuffEffect(BuffEffectSync buffEffect, ref long serverClockOffset, long localPlayerUid)
    {
        var changes = new List<BuffChangeEvent>();

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var effect in buffEffect.BuffEffects)
        {
            if (!effect.HasBuffUuid) continue;
            var buffUuid = effect.BuffUuid;
            switch (effect.Type)
            {
                case EBuffEventType.BuffEventAddTo:
                    Debug.WriteLine($"Buff Event: Add buff {buffUuid}, trigger time:{(effect.HasTriggerTime ? effect.TriggerTime : 0)}");
                    break;
                case EBuffEventType.BuffEventReplace:
                    Debug.WriteLine($"Buff Event: Replace buff {buffUuid}, trigger time:{(effect.HasTriggerTime ? effect.TriggerTime : 0)}");
                    break;
                case EBuffEventType.BuffEventRemove:
                    Debug.WriteLine($"Buff Event: Remove buff {buffUuid}, trigger time:{(effect.HasTriggerTime ? effect.TriggerTime : 0)}");
                    break;
                case EBuffEventType.BuffEventRemoveLayer:
                    Debug.WriteLine($"Buff Event: Remove layer from buff {buffUuid}, trigger time:{(effect.HasTriggerTime ? effect.TriggerTime : 0)}");
                    break;
            }

            foreach (var logicEffect in effect.LogicEffect)
            {
                if (!logicEffect.HasEffectType || !logicEffect.HasRawData) continue;

                if (logicEffect.EffectType == EBuffEffectLogicPbType.BuffEffectAddBuff)
                {
                    try
                    {
                        var buffInfo = BuffInfo.Parser.ParseFrom(logicEffect.RawData);
                        if (!buffInfo.HasBaseId) continue;

                        var baseId = buffInfo.BaseId;
                        var fireUid = (buffInfo.HasFireUuid ? buffInfo.FireUuid : 0) >> 16;
                        var inSelfList = SelfAppliedBuffIds.Contains(baseId);

                        if (inSelfList && fireUid != localPlayerUid) continue;

                        var layer = buffInfo.HasLayer ? buffInfo.Layer : 1;
                        var duration = buffInfo.HasDuration ? (int)buffInfo.Duration : 0;
                        var createTime = buffInfo.HasCreateTime ? buffInfo.CreateTime : now;

                        if (buffInfo.HasCreateTime)
                        {
                            serverClockOffset = now - createTime;
                        }

                        ActiveBuffs[buffUuid] = new ActiveBuff
                        {
                            BaseId = baseId,
                            Layer = layer,
                            Duration = duration,
                            CreateTime = createTime
                        };

                        changes.Add(new BuffChangeEvent
                        {
                            BaseId = baseId,
                            ChangeType = BuffChangeType.Add
                        });
                    }
                    catch (InvalidProtocolBufferException)
                    {
                        // Skip invalid buff info
                    }
                }
                else if (logicEffect.EffectType == EBuffEffectLogicPbType.BuffEffectBuffChange)
                {
                    try
                    {
                        var changeInfo = BuffChange.Parser.ParseFrom(logicEffect.RawData);
                        if (ActiveBuffs.TryGetValue(buffUuid, out var entry))
                        {
                            var baseId = entry.BaseId;

                            if (changeInfo.Layer != 0)
                            {
                                entry.Layer = changeInfo.Layer;
                            }

                            if (changeInfo.Duration != 0)
                            {
                                entry.Duration = changeInfo.Duration;
                            }

                            if (changeInfo.CreateTime != 0)
                            {
                                entry.CreateTime = changeInfo.CreateTime;
                            }

                            changes.Add(new BuffChangeEvent
                            {
                                BaseId = baseId,
                                ChangeType = BuffChangeType.Change
                            });
                        }
                    }
                    catch (InvalidProtocolBufferException)
                    {
                        // Skip invalid buff change info
                    }
                }
            }

            if (effect is { HasType: true, Type: EBuffEventType.BuffEventRemove })
            {
                if (ActiveBuffs.Remove(buffUuid, out var removedBuff))
                {
                    changes.Add(new BuffChangeEvent
                    {
                        BaseId = removedBuff.BaseId,
                        ChangeType = BuffChangeType.Remove
                    });
                }
            }
        }

        List<BuffUpdateState>? updatePayload = null;
        if (MonitoredBuffIds.Count > 0 || SelfAppliedBuffIds.Count > 0 || MonitorAllBuff)
        {
            var clockOffset = serverClockOffset;
            updatePayload = ActiveBuffs.Values
                .Where(buff => MonitorAllBuff ||
                               MonitoredBuffIds.Contains(buff.BaseId) ||
                               SelfAppliedBuffIds.Contains(buff.BaseId))
                .Select(buff => new BuffUpdateState
                {
                    BaseId = buff.BaseId,
                    Layer = buff.Layer,
                    DurationMs = buff.Duration,
                    CreateTimeMs = buff.CreateTime + clockOffset
                })
                .ToList();
        }

        return new BuffProcessResult
        {
            UpdatePayload = updatePayload,
            Changes = changes
        };
    }

    /// <summary>
    /// Clears all active buffs.
    /// </summary>
    public void Clear()
    {
        ActiveBuffs.Clear();
    }
}
