using System;
using System.Collections.Generic;
using Game.Config;
using Game.Game.Map;
using Game.Game.Player;
using Shared.Common.Config;
using Game.Core;
using Game.Game.Monster;
using Shared.Core.Log;
using Game.Network;
using Shared.Core.Network;
using Shared.Common.Protos;

namespace Game.Game.Fight;

/// <summary>
/// 战斗管理器。处理技能使用、伤害结算、死亡处理等战斗核心逻辑。
/// 参考: Java 项目的 FightManager。
/// </summary>
public class FightManager
{
    private static readonly ILog Log = LogManager.GetLogger<FightManager>();

    public static FightManager Instance { get; } = new();

    /// <summary>在线战斗单元 key=objectId</summary>
    private readonly Dictionary<long, FightUnit> _units = new();


    private readonly object _lock = new();

    private FightManager() { }

    /// <summary>设置配置管理器（由 FightModel.InitAsync 注入）</summary>
    public void SetConfigManager()
    {
    }

    #region 战斗单元管理

    /// <summary>注册战斗单元（玩家进入地图时调用）</summary>
    public FightUnit RegisterUnit(long objectId, int objectType, string name, int cfgId, FightAttribute attr)
    {
        var unit = new FightUnit(objectId, objectType, name, cfgId, attr);
        lock (_lock)
        {
            _units[objectId] = unit;
        }
        Log.Debug($"注册战斗单元 ObjectId:{objectId} Name:{name}");
        return unit;
    }

    /// <summary>注销战斗单元（玩家离开地图时调用）</summary>
    public void UnregisterUnit(long objectId)
    {
        lock (_lock)
        {
            _units.Remove(objectId);
        }
        Log.Debug($"注销战斗单元 ObjectId:{objectId}");
    }

    /// <summary>获取战斗单元</summary>
    public FightUnit? GetUnit(long objectId)
    {
        lock (_lock)
        {
            return _units.TryGetValue(objectId, out var unit) ? unit : null;
        }
    }

    #endregion

    #region 技能配置

    /// <summary>获取技能配置（从 ConfigManager 加载的 CSV 数据）</summary>
    public SkillConfig? GetSkillConfig(int skillId)
    {
        return ConfigComponent.ConfigManager.Get<SkillConfig>(skillId);
    }

    #endregion

    #region 技能使用

    /// <summary>
    /// 使用技能。核心战斗入口。
    /// </summary>
    public UseSkillResult UseSkill(long attackerId, int skillId, int x, int y, long targetId)
    {
        var result = new UseSkillResult();

        var attacker = GetUnit(attackerId);
        if (attacker == null)
        {
            result.Code = FightErrorCode.NotInFight;
            return result;
        }

        if (attacker.IsDead)
        {
            result.Code = FightErrorCode.AlreadyDead;
            return result;
        }

        var skill = attacker.GetSkill(skillId);
        if (skill == null)
        {
            result.Code = FightErrorCode.SkillNotFound;
            return result;
        }

        var skillCfg = GetSkillConfig(skillId);
        if (skillCfg == null)
        {
            result.Code = FightErrorCode.SkillConfigNotFound;
            return result;
        }

        // CD检查
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (skill.LastUseTime + skillCfg.CdTime > now)
        {
            result.Code = FightErrorCode.SkillInCd;
            return result;
        }

        var target = GetUnit(targetId);
        if (target == null)
        {
            result.Code = FightErrorCode.TargetNotFound;
            return result;
        }

        if (target.IsDead)
        {
            result.Code = FightErrorCode.TargetDead;
            return result;
        }

        // 距离检查
        var map = MapManager.Instance.GetPlayerMap(attackerId);
        if (map != null)
        {
            var attackerObj = map.GetObject(attackerId);
            var targetObj = map.GetObject(targetId);
            if (attackerObj != null && targetObj != null)
            {
                int dist = Math.Max(Math.Abs(attackerObj.X - targetObj.X), Math.Abs(attackerObj.Y - targetObj.Y));
                if (skillCfg.CastRange > 0 && dist > skillCfg.CastRange)
                {
                    result.Code = FightErrorCode.OutOfRange;
                    return result;
                }
            }
        }

        // 更新技能使用时间
        skill.LastUseTime = now;

        // 执行战斗计算
        var hitResults = FightCore.UseSkill(attacker, target, skillCfg);

        result.Code = FightErrorCode.Ok;
        result.AttackerId = attackerId;
        result.SkillId = skillId;
        result.TargetId = targetId;
        result.X = x;
        result.Y = y;
        result.HitResults = hitResults;

        // 处理死亡
        foreach (var hit in hitResults)
        {
            if (hit.IsDead)
            {
                OnUnitKilled(hit.TargetId, attackerId);
            }
        }

        // 广播技能效果给视野内玩家
        BroadcastSkillEffect(attackerId, result);

        Log.Debug($"技能使用 Attacker:{attackerId} Skill:{skillId} Target:{targetId} Hits:{hitResults.Count}");
        return result;
    }

    #endregion

    #region 死亡处理

    /// <summary>战斗单元死亡回调</summary>
    private void OnUnitKilled(long victimId, long killerId)
    {
        var victim = GetUnit(victimId);
        if (victim == null) return;

        Log.Info($"战斗死亡 Victim:{victimId}({victim.Name}) Killer:{killerId}");

        bool isMonster = victim.ObjectType == MapObjectType.Npc;

        // 通知被击杀者
        SendToRole(victimId, new KilledByNtf
        {
            KillerId = killerId,
            KillerName = GetUnit(killerId)?.Name ?? "",
            IsMonster = isMonster,
        });

        // 通知视野内其他玩家
        BroadcastToWatchers(victimId, new HpChangedNtf
        {
            ObjectId = victimId,
            DeltaHp = -victim.Attr.Hp,
            Hp = 0,
            Reason = HpChangeReason.Killed,
        });

        // 如果是怪物，处理掉落
        if (isMonster && MonsterManager.Instance != null)
        {
            MonsterManager.Instance.OnMonsterKilled(victimId, killerId);
        }
    }

    #endregion

    #region 复活

    /// <summary>复活战斗单元</summary>
    public bool Revive(long objectId, int hpPercent = 100)
    {
        var unit = GetUnit(objectId);
        if (unit == null || !unit.IsDead) return false;

        unit.Attr.Hp = unit.Attr.MaxHp * hpPercent / 100;
        unit.ThreatMap.Clear();
        unit.KillerId = 0;
        unit.KillerSkillId = 0;

        BroadcastToWatchers(objectId, new HpChangedNtf
        {
            ObjectId = objectId,
            DeltaHp = unit.Attr.Hp,
            Hp = unit.Attr.Hp,
            Reason = HpChangeReason.Revive,
        });

        Log.Info($"战斗复活 ObjectId:{objectId} Hp:{unit.Attr.Hp}");
        return true;
    }

    #endregion

    #region 消息发送

    /// <summary>发送 IMessage 给指定角色</summary>
    private static void SendToRole(long roleId, IMessage msg)
    {
        PlayerManager.Instance.SendToPlayer(roleId, msg);
    }

    private void BroadcastSkillEffect(long attackerId, UseSkillResult skillResult)
    {
        var ack = new UseSkillAck
        {
            Code = skillResult.Code,
            AttackerId = skillResult.AttackerId,
            SkillId = skillResult.SkillId,
            MainTargetId = skillResult.TargetId,
            X = skillResult.X,
            Y = skillResult.Y,
        };

        foreach (var hit in skillResult.HitResults)
        {
            if (hit.Missed)
            {
                ack.MissedIds.Add(hit.TargetId);
            }
            else
            {
                ack.Targets.Add(new SkillTargetInfo
                {
                    TargetId = hit.TargetId,
                    DeltaHp = -hit.Damage,
                    Hp = hit.Hp,
                    IsCritical = hit.IsCritical,
                });
            }
        }

        // 发送给攻击者
        SendToRole(attackerId, ack);

        // 广播给视野内玩家
        BroadcastToWatchers(attackerId, ack);
    }

    private void BroadcastToWatchers(long objectId, IMessage msg)
    {
        var map = MapManager.Instance.GetPlayerMap(objectId);
        if (map == null) return;

        var mapObj = map.GetObject(objectId);
        if (mapObj == null) return;

        var watchers = map.Aoi.GetObjectsByRange(mapObj.X, mapObj.Y, 1);
        foreach (var watcher in watchers)
        {
            if (watcher.ObjectId != objectId)
            {
                SendToRole(watcher.ObjectId, msg);
            }
        }
    }

    #endregion

    #region 清理

    /// <summary>清除所有战斗单元（停服时调用）</summary>
    public void RemoveAll()
    {
        lock (_lock)
        {
            _units.Clear();
        }
        Log.Info("战斗管理器已清空");
    }

    #endregion
}

/// <summary>技能使用结果</summary>
public class UseSkillResult
{
    public int Code { get; set; }
    public long AttackerId { get; set; }
    public int SkillId { get; set; }
    public long TargetId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public List<SkillHitResult> HitResults { get; set; } = new();
}
