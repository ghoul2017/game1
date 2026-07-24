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
public static class FightHelper
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(FightHelper));
    private const int EffectAreaSingle = 1;
    private const int EffectAreaCircle = 2;
    private const int EffectAreaFan = 3;
    private const int EffectAreaSelf = 4;

    /// <summary>获取战斗单元（通过全局查找）</summary>
    public static FightUnitComponent? GetFightUnit(long objectId)
    {
        var mapObj = Context.Map.FindMapObject(objectId);
        return mapObj?.TryGetComponent<FightUnitComponent>();
    }

    /// <summary>获取技能配置</summary>
    public static SkillConfig? GetSkillConfig(int skillId)
    {
        return ConfigManager.Get<SkillConfig>(skillId);
    }

    /// <summary>
    /// 使用技能。核心战斗入口。
    /// </summary>
    public static string UseSkill(long attackerId, int skillId, int x, int y, long targetId)
    {
        var attackerFight = GetFightUnit(attackerId);
        if (attackerFight == null)
            return "不在战斗状态";

        if (attackerFight.IsDead)
            return "已死亡";

        var skill = attackerFight.GetSkill(skillId);
        if (skill == null)
            return "技能不存在";

        var skillCfg = GetSkillConfig(skillId);
        if (skillCfg == null)
            return "技能配置未找到";

        // CD检查
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (skill.LastUseTime + skillCfg.CdTime > now)
            return "技能冷却中";

        var attackerObj = Context.Map.FindMapObject(attackerId);
        if (attackerObj == null)
            return "不在地图中";

        var map = Context.Map.GetMap(attackerObj.MapId, attackerObj.Line);
        if (map == null)
            return "不在地图中";

        var targetObj = targetId != 0 ? map.GetObject(targetId) : null;
        var targetFight = targetId != 0 ? targetObj?.TryGetComponent<FightUnitComponent>() : null;

        if (skillCfg.EffectArea == EffectAreaSingle)
        {
            if (targetFight == null)
                return "目标不存在";

            if (targetFight.IsDead)
                return "目标已死亡";
        }

        if (targetObj != null && skillCfg.CastRange > 0)
        {
            int dist = Math.Max(Math.Abs(attackerObj.X - targetObj.X), Math.Abs(attackerObj.Y - targetObj.Y));
            if (dist > skillCfg.CastRange)
                return "超出攻击范围";
        }

        // 更新技能使用时间
        skill.LastUseTime = now;

        var targets = CollectSkillTargets(map, attackerObj, targetObj, targetFight, targetId, skillCfg, x, y);
        if (targets.Count == 0)
            return "目标不存在";

        var hitResults = skillCfg.EffectArea == EffectAreaSingle
            ? FightCore.UseSkill(attackerFight, attackerId, targets[0].fight, targets[0].id, skillCfg)
            : FightCore.UseSkillAoe(attackerFight, attackerId, targets, skillCfg);

        foreach (var hit in hitResults)
        {
            if (hit.IsDead)
                OnUnitKilled(hit.TargetId, attackerId);
        }

        BroadcastSkillEffect(attackerId, skillId, targetId, x, y, hitResults);
        Log.Debug($"技能使用 Attacker:{attackerId} Skill:{skillId} Target:{targetId} Hits:{hitResults.Count}");
        return "";
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
                Fight = o.TryGetComponent<FightUnitComponent>(),
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

    private static void BroadcastSkillEffect(
        long attackerId,
        int skillId,
        long targetId,
        int x,
        int y,
        List<SkillHitResult> hitResults)
    {
        var ack = new UseSkillAck
        {
            AttackerId = attackerId,
            SkillId = skillId,
            MainTargetId = targetId,
            X = x,
            Y = y,
        };

        foreach (var hit in hitResults)
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
