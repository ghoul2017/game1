using System;
using System.Collections.Generic;
using System.Linq;
using Game.Components.Map;
using Game.Components.Monster;
using Game.Components.Player;
using Game.Entities;
using Shared.Common.Config;
using Shared.Common.Const;
using Shared.Common.Protos;
using Shared.Core;
using Shared.Core.Config;
using Shared.Core.Event;
using Shared.Core.Log;

namespace Game.Components.Fight;

/// <summary>
/// 战斗系统。提供战斗单元管理、技能使用、死亡处理等核心战斗逻辑。
/// </summary>
public static class FightSystem
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(FightSystem));
    private const int EffectAreaSingle = 1;
    private const int EffectAreaCircle = 2;
    private const int EffectAreaFan = 3;
    private const int EffectAreaSelf = 4;

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
        if (self.ThreatMap.Count == 0)
            return 0;

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

    /// <summary>获取战斗单元（通过全局查找）</summary>
    public static FightUnitComponent? GetFightUnit(long objectId)
    {
        var mapObj = Context.Map.FindMapObject(objectId);
        return mapObj?.GetComponent<FightUnitComponent>();
    }

    /// <summary>获取技能配置</summary>
    public static SkillConfig? GetSkillConfig(int skillId)
    {
        return ConfigManager.Get<SkillConfig>(skillId);
    }

    /// <summary>
    /// 使用技能。核心战斗入口。
    /// </summary>
    public static UseSkillResult UseSkill(long attackerId, int skillId, int x, int y, long targetId)
    {
        var result = new UseSkillResult();

        var attackerFight = GetFightUnit(attackerId);
        if (attackerFight == null)
        {
            result.ErrorMessage = "不在战斗状态";
            return result;
        }

        if (attackerFight.IsDead)
        {
            result.ErrorMessage = "已死亡";
            return result;
        }

        var skill = attackerFight.GetSkill(skillId);
        if (skill == null)
        {
            result.ErrorMessage = "技能不存在";
            return result;
        }

        var skillCfg = GetSkillConfig(skillId);
        if (skillCfg == null)
        {
            result.ErrorMessage = "技能配置未找到";
            return result;
        }

        // CD检查
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (skill.LastUseTime + skillCfg.CdTime > now)
        {
            result.ErrorMessage = "技能冷却中";
            return result;
        }

        var attackerObj = Context.Map.FindMapObject(attackerId);
        if (attackerObj == null)
        {
            result.ErrorMessage = "不在地图中";
            return result;
        }

        var map = Context.Map.GetMap(attackerObj.MapId, attackerObj.Line);
        if (map == null)
        {
            result.ErrorMessage = "不在地图中";
            return result;
        }

        var targetObj = targetId != 0 ? map.GetObject(targetId) : null;
        var targetFight = targetId != 0 ? targetObj?.GetComponent<FightUnitComponent>() : null;

        if (skillCfg.EffectArea == EffectAreaSingle)
        {
            if (targetFight == null)
            {
                result.ErrorMessage = "目标不存在";
                return result;
            }

            if (targetFight.IsDead)
            {
                result.ErrorMessage = "目标已死亡";
                return result;
            }
        }

        if (targetObj != null && skillCfg.CastRange > 0)
        {
            int dist = Math.Max(Math.Abs(attackerObj.X - targetObj.X), Math.Abs(attackerObj.Y - targetObj.Y));
            if (dist > skillCfg.CastRange)
            {
                result.ErrorMessage = "超出攻击范围";
                return result;
            }
        }

        // 更新技能使用时间
        skill.LastUseTime = now;

        var targets = CollectSkillTargets(map, attackerObj, targetObj, targetFight, targetId, skillCfg, x, y);
        if (targets.Count == 0)
        {
            result.ErrorMessage = "目标不存在";
            return result;
        }

        var hitResults = skillCfg.EffectArea == EffectAreaSingle
            ? FightCore.UseSkill(attackerFight, attackerId, targets[0].fight, targets[0].id, skillCfg)
            : FightCore.UseSkillAoe(attackerFight, attackerId, targets, skillCfg);

        // 成功，ErrorMessage 保持空
        result.AttackerId = attackerId;
        result.SkillId = skillId;
        result.TargetId = targetId;
        result.X = x;
        result.Y = y;
        result.HitResults = hitResults;

        foreach (var hit in hitResults)
        {
            if (hit.IsDead)
                OnUnitKilled(hit.TargetId, attackerId);
        }

        BroadcastSkillEffect(attackerId, result);
        Log.Debug($"技能使用 Attacker:{attackerId} Skill:{skillId} Target:{targetId} Hits:{hitResults.Count}");
        return result;
    }

    private static List<(FightUnitComponent fight, long id)> CollectSkillTargets(
        GameMap map,
        MapObjectEntity attackerObj,
        MapObjectEntity? targetObj,
        FightUnitComponent? targetFight,
        long targetId,
        SkillConfig skillCfg,
        int x,
        int y)
    {
        if (skillCfg.EffectArea == EffectAreaSingle)
        {
            return targetFight != null && !targetFight.IsDead
                ? new List<(FightUnitComponent fight, long id)> { (targetFight, targetId) }
                : new List<(FightUnitComponent fight, long id)>();
        }

        int centerX = x;
        int centerY = y;
        if (skillCfg.EffectArea == EffectAreaSelf)
        {
            centerX = attackerObj.X;
            centerY = attackerObj.Y;
        }
        else if ((skillCfg.EffectArea == EffectAreaCircle || skillCfg.EffectArea == EffectAreaFan) && targetObj != null)
        {
            centerX = targetObj.X;
            centerY = targetObj.Y;
        }
        else if (centerX == 0 && centerY == 0)
        {
            centerX = attackerObj.X;
            centerY = attackerObj.Y;
        }

        int range = skillCfg.EffectParam > 0 ? skillCfg.EffectParam : Math.Max(1, skillCfg.CastRange);
        int maxTarget = skillCfg.MaxTarget > 0 ? skillCfg.MaxTarget : 1;

        return map.GetObjectsInRange(centerX, centerY, range)
            .Where(o => o.ObjectId != attackerObj.ObjectId)
            .Select(o => new
            {
                Obj = o,
                Fight = o.GetComponent<FightUnitComponent>(),
                Distance = Math.Max(Math.Abs(o.X - centerX), Math.Abs(o.Y - centerY))
            })
            .Where(t => t.Fight != null && !t.Fight.IsDead)
            .OrderByDescending(t => t.Obj.ObjectId == targetId)
            .ThenBy(t => t.Distance)
            .Take(maxTarget)
            .Select(t => (t.Fight!, t.Obj.ObjectId))
            .ToList();
    }

    /// <summary>战斗单元死亡回调</summary>
    private static void OnUnitKilled(long victimId, long killerId)
    {
        var victimFight = GetFightUnit(victimId);
        if (victimFight == null)
            return;

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
            // 直接调用 GameMap 处理怪物死亡（掉落、刷新等）
            if (mapObj != null)
            {
                var map = Context.Map.GetMap(mapObj.MapId, mapObj.Line);
                map?.OnMonsterKilled(victimId, killerId);

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

    /// <summary>复活战斗单元</summary>
    public static bool Revive(long objectId, int hpPercent = 100)
    {
        var fight = GetFightUnit(objectId);
        if (fight == null || !fight.IsDead)
            return false;

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

    private static void BroadcastSkillEffect(long attackerId, UseSkillResult skillResult)
    {
        var ack = new UseSkillAck
        {
            ErrorMessage = skillResult.ErrorMessage,
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
}

/// <summary>技能使用结果</summary>
public class UseSkillResult
{
    public string ErrorMessage { get; set; } = "";
    public long AttackerId { get; set; }
    public int SkillId { get; set; }
    public long TargetId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public List<SkillHitResult> HitResults { get; set; } = new();
}
