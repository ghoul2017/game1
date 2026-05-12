using System;
using System.Collections.Generic;
using Game.Components.Map;
using Game.Components.Monster;
using Game.Components.Player;
using Game.Config;
using Game.Entities;
using Shared.Core;
using Shared.Common.Config;
using Shared.Common.Protos;
using Shared.Core.Event;
using Shared.Core.Log;

namespace Game.Components.Fight;

/// <summary>
/// 战斗系统。提供战斗单元管理、技能使用、死亡处理等核心战斗逻辑。
/// </summary>
public static class FightSystem
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(FightSystem));

    #region FightUnitComponent 扩展方法

    /// <summary>受伤，返回实际扣血量</summary>
    public static int TakeDamage(this FightUnitComponent self, int damage)
    {
        int actual = Math.Min(damage, self.Attr.Hp);
        self.Attr.Hp = Math.Max(0, self.Attr.Hp - damage);
        return actual;
    }

    /// <summary>回血，返回实际回血量</summary>
    public static int Heal(this FightUnitComponent self, int amount)
    {
        int actual = Math.Min(amount, self.Attr.MaxHp - self.Attr.Hp);
        self.Attr.Hp = Math.Min(self.Attr.MaxHp, self.Attr.Hp + amount);
        return actual;
    }

    /// <summary>添加仇恨</summary>
    public static void AddThreat(this FightUnitComponent self, long targetId, int value)
    {
        self.ThreatMap.TryGetValue(targetId, out var existing);
        self.ThreatMap[targetId] = existing + value;
    }

    /// <summary>获取仇恨最高的目标</summary>
    public static long GetMaxThreatTarget(this FightUnitComponent self)
    {
        if (self.ThreatMap.Count == 0) return 0;

        long targetId = 0;
        int maxThreat = 0;
        foreach (var kv in self.ThreatMap)
        {
            if (kv.Value > maxThreat)
            {
                maxThreat = kv.Value;
                targetId = kv.Key;
            }
        }
        return targetId;
    }

    /// <summary>添加技能</summary>
    public static void AddSkill(this FightUnitComponent self, Skill skill)
    {
        self.Skills[skill.SkillId] = skill;
    }

    /// <summary>获取技能</summary>
    public static Skill? GetSkill(this FightUnitComponent self, int skillId)
    {
        return self.Skills.TryGetValue(skillId, out var skill) ? skill : null;
    }

    /// <summary>重置战斗状态（满血复活）</summary>
    public static void ResetFightState(this FightUnitComponent self)
    {
        self.Attr.Hp = self.Attr.MaxHp;
        self.ThreatMap.Clear();
        self.SkillCoolDown.Clear();
        self.KillerId = 0;
        self.KillerSkillId = 0;
    }

    #endregion

    #region 战斗单元管理

    /// <summary>注册战斗单元（玩家/怪物进入地图时调用）</summary>
    public static FightUnitComponent RegisterUnit(this MapObjectEntity entity, FightAttribute attr)
    {
        var fight = entity.AddComponent<FightUnitComponent>();
        fight.Attr = attr;
        Log.Debug($"注册战斗单元 ObjectId:{entity.ObjectId} Name:{entity.Name}");
        return fight;
    }

    /// <summary>注销战斗单元（玩家/怪物离开地图时调用）</summary>
    public static void UnregisterUnit(this MapObjectEntity entity)
    {
        if (entity.TryGetComponent<FightUnitComponent>(out var fight))
        {
            entity.RemoveComponent(fight);
            Log.Debug($"注销战斗单元 ObjectId:{entity.ObjectId}");
        }
    }

    /// <summary>获取战斗单元（通过全局查找）</summary>
    public static FightUnitComponent? GetFightUnit(long objectId)
    {
        var mapObj = Context.Map.FindMapObject(objectId);
        if (mapObj != null && mapObj.TryGetComponent<FightUnitComponent>(out var fight))
            return fight;
        return null;
    }

    #endregion

    #region 技能配置

    /// <summary>获取技能配置</summary>
    public static SkillConfig? GetSkillConfig(int skillId)
    {
        return ConfigManager.Get<SkillConfig>(skillId);
    }

    #endregion

    #region 技能使用

    /// <summary>
    /// 使用技能。核心战斗入口。
    /// </summary>
    public static UseSkillResult UseSkill(long attackerId, int skillId, int x, int y, long targetId)
    {
        var result = new UseSkillResult();

        var attackerFight = GetFightUnit(attackerId);
        if (attackerFight == null)
        {
            result.Code = FightErrorCode.NotInFight;
            return result;
        }

        if (attackerFight.IsDead)
        {
            result.Code = FightErrorCode.AlreadyDead;
            return result;
        }

        var skill = attackerFight.GetSkill(skillId);
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

        var targetFight = GetFightUnit(targetId);
        if (targetFight == null)
        {
            result.Code = FightErrorCode.TargetNotFound;
            return result;
        }

        if (targetFight.IsDead)
        {
            result.Code = FightErrorCode.TargetDead;
            return result;
        }

        // 距离检查
        var map = Context.Map.GetPlayerMap(attackerId);
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
        var hitResults = FightCore.UseSkill(attackerFight, attackerId, targetFight, targetId, skillCfg);

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
    private static void OnUnitKilled(long victimId, long killerId)
    {
        var victimFight = GetFightUnit(victimId);
        if (victimFight == null) return;

        var killerFight = GetFightUnit(killerId);

        Log.Info($"战斗死亡 Victim:{victimId} Killer:{killerId}");

        var mapObj = Context.Map.FindMapObject(victimId);
        bool isMonster = mapObj != null && mapObj.ObjectType == MapObjectType.Monster;

        // 通知被击杀者
        Context.Players.SendToPlayer(victimId, new KilledByNtf
        {
            KillerId = killerId,
            KillerName = killerFight != null ? Context.Map.FindMapObject(killerId)?.Name ?? "" : "",
            IsMonster = isMonster,
        });

        // 通知视野内其他玩家
        Context.Map.BroadcastToAoiWatchers(victimId, new HpChangedNtf
        {
            ObjectId = victimId,
            DeltaHp = -victimFight.Attr.Hp,
            Hp = 0,
            Reason = HpChangeReason.Killed,
        });

        if (isMonster)
        {
            // 直接调用 MonsterSystem 处理怪物死亡（掉落、刷新等）
            Context.Monster.OnMonsterKilled(victimId, killerId);

            // 发布事件给 GameTask 等其他模块
            if (mapObj != null)
            {
                EventBus.Instance.Publish(new EventType.MonsterKilledEvent
                {
                    MonsterId = victimId,
                    KillerId = killerId,
                    MonsterCfgId = mapObj.CfgId,
                    MapId = mapObj.MapId,
                    Line = mapObj.Line,
                });
            }
        }
        else
        {
            // 玩家死亡事件
            EventBus.Instance.Publish(new EventType.PlayerDeathEvent
            {
                RoleId = victimId,
                KillerId = killerId,
            });
        }
    }

    #endregion

    #region 复活

    /// <summary>复活战斗单元</summary>
    public static bool Revive(long objectId, int hpPercent = 100)
    {
        var fight = GetFightUnit(objectId);
        if (fight == null || !fight.IsDead) return false;

        fight.Attr.Hp = fight.Attr.MaxHp * hpPercent / 100;
        fight.ThreatMap.Clear();
        fight.KillerId = 0;
        fight.KillerSkillId = 0;

        Context.Map.BroadcastToAoiWatchers(objectId, new HpChangedNtf
        {
            ObjectId = objectId,
            DeltaHp = fight.Attr.Hp,
            Hp = fight.Attr.Hp,
            Reason = HpChangeReason.Revive,
        });

        Log.Info($"战斗复活 ObjectId:{objectId} Hp:{fight.Attr.Hp}");
        return true;
    }

    #endregion

    #region 消息发送

    private static void BroadcastSkillEffect(long attackerId, UseSkillResult skillResult)
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
        Context.Players.SendToPlayer(attackerId, ack);

        // 广播给视野内玩家
        Context.Map.BroadcastToAoiWatchers(attackerId, ack);
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
