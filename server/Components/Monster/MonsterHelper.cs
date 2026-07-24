using System;
using System.Collections.Generic;
using System.Linq;
using Game.Components.Fight;
using Game.Components.Map;
using Game.Components.Player;
using Shared.Core;
using Shared.Core.Config;
using Shared.Common.Config;
using Shared.Core.Log;

namespace Game.Components.Monster;

/// <summary>
/// 怪物系统。提供刷怪、AI、死亡、掉落、刷新等功能。
/// 作为 GameMap 扩展方法，无状态设计。
/// </summary>
public static class MonsterHelper
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(MonsterHelper));

    /// <summary>初始化地图怪物（地图创建时调用）</summary>
    public static void InitMonsters(this GameMap map)
    {
        var monsters = ConfigManager.GetAll<MonsterConfig>()
            .Where(m => m.MapId == map.CfgId)
            .ToList();

        if (monsters.Count == 0)
            return;

        foreach (var cfg in monsters)
        {
            string timerKey = GetTimerKey(map, cfg.Id);
            if (!map.SpawnTimers.ContainsKey(timerKey))
                map.SpawnTimers[timerKey] = 0;

            for (int i = 0; i < cfg.MaxCount; i++)
            {
                map.SpawnMonster(cfg);
            }
        }

        Log.Info($"地图 {map.CfgId} 怪物初始化完成: {monsters.Count} 种怪物");
    }

    /// <summary>在刷新区域内随机位置生成怪物</summary>
    public static MonsterEntity? SpawnMonster(this GameMap map, MonsterConfig cfg)
    {
        int x = map.Random.Next(cfg.SpawnX1, cfg.SpawnX2 + 1);
        int y = map.Random.Next(cfg.SpawnY1, cfg.SpawnY2 + 1);
        return map.SpawnMonsterAt(cfg, x, y);
    }

    /// <summary>在指定位置生成怪物</summary>
    public static MonsterEntity? SpawnMonsterAt(this GameMap map, MonsterConfig cfg, int x, int y)
    {
        if (map.GetMonsterCountByCfgId(cfg.Id) >= cfg.MaxCount)
            return null;

        long monsterId = IdGenerator.Instance.NextId();

        var fightAttr = new FightAttribute
        {
            MaxHp = cfg.MaxHp,
            Hp = cfg.MaxHp,
            PhyAttMin = cfg.Attack,
            PhyAttMax = cfg.Attack,
            PhyDefMin = cfg.Defense,
            PhyDefMax = cfg.Defense,
        };

        var monster = MonsterEntity.Create(monsterId, cfg, map.CfgId, map.Line, x, y, fightAttr);

        map.AddObject(monster);

        return monster;
    }

    /// <summary>怪物死亡</summary>
    public static void OnMonsterKilled(this GameMap map, long monsterId, long killerId)
    {
        var monster = map.GetMonster(monsterId);
        if (monster == null)
            return;

        map.RemoveObject(monsterId);

        if (monster.Info.Config != null)
        {
            string timerKey = GetTimerKey(map, monster.CfgId);
            map.SpawnTimers[timerKey] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                + monster.Info.Config.SpawnInterval * 1000;

            CalculateAndGrantDrop(map, monster, killerId);
        }

        Log.Info($"怪物死亡 MonsterId:{monsterId} KillerId:{killerId}");
    }

    /// <summary>AI 更新入口（由 GameMap 每秒调用）</summary>
    public static void UpdateAllMonsters(this GameMap map)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var snapshot = map.GetAllMonsters();

        foreach (var monster in snapshot)
        {
            try
            {
                UpdateMonsterAi(map, monster, now);
            }
            catch (Exception ex)
            {
                Log.Error($"AI更新异常 MonsterId:{monster.ObjectId}", ex);
            }
        }

        CheckSpawnTimers(map, now);
    }

    /// <summary>更新单个怪物AI</summary>
    private static void UpdateMonsterAi(GameMap map, MonsterEntity monster, long now)
    {
        if (monster.Info.Config == null)
            return;
        if (monster.Fight.IsDead)
            return;

        long updateInterval = 1000;
        if (now - monster.Ai.LastUpdateTime < updateInterval)
            return;
        monster.Ai.LastUpdateTime = now;

        switch (monster.Ai.AiState)
        {
            case AiState.Idle:
                UpdateAiIdle(map, monster);
                break;

            case AiState.Patrol:
                UpdateAiPatrol(map, monster);
                break;

            case AiState.Chase:
                UpdateAiChase(map, monster, now);
                break;

            case AiState.Attack:
                UpdateAiAttack(map, monster, now);
                break;
        }
    }

    /// <summary>待机AI</summary>
    private static void UpdateAiIdle(GameMap map, MonsterEntity monster)
    {
        var config = monster.Info.Config ??
            throw new InvalidOperationException($"怪物缺少配置 MonsterId:{monster.Id}");

        if (config.AiType == 2 || config.AiType == 3)
        {
            monster.Ai.AiState = AiState.Patrol;
            monster.Ai.PatrolTarget = GetRandomPatrolPoint(map, monster);
        }
        else
        {
            var targetId = FindTarget(map, monster);
            if (targetId.HasValue)
            {
                monster.Ai.AiState = AiState.Chase;
                monster.Ai.ChaseTarget = targetId;
            }
        }
    }

    /// <summary>巡逻AI</summary>
    private static void UpdateAiPatrol(GameMap map, MonsterEntity monster)
    {
        var targetId = FindTarget(map, monster);
        if (targetId.HasValue)
        {
            monster.Ai.AiState = AiState.Chase;
            monster.Ai.ChaseTarget = targetId;
            return;
        }

        if (monster.Ai.PatrolTarget.HasValue)
        {
            int dx = monster.Ai.PatrolTarget.Value.X - monster.X;
            int dy = monster.Ai.PatrolTarget.Value.Y - monster.Y;

            if (Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1)
            {
                monster.Ai.AiState = AiState.Idle;
            }
            else
            {
                int moveX = monster.X + Math.Sign(dx);
                int moveY = monster.Y + Math.Sign(dy);
                map.MoveObject(monster.ObjectId, moveX, moveY);
            }
        }
    }

    /// <summary>追击AI</summary>
    private static void UpdateAiChase(GameMap map, MonsterEntity monster, long now)
    {
        var config = monster.Info.Config ??
            throw new InvalidOperationException($"怪物缺少配置 MonsterId:{monster.Id}");

        if (monster.Ai.ChaseTarget == null)
            return;

        var targetPlayer = map.GetPlayer(monster.Ai.ChaseTarget.Value);
        if (targetPlayer == null)
        {
            monster.Ai.AiState = AiState.Idle;
            monster.Ai.ChaseTarget = null;
            return;
        }

        int dist = Math.Max(
            Math.Abs(targetPlayer.X - monster.X),
            Math.Abs(targetPlayer.Y - monster.Y));

        if (dist <= config.AttackRange)
        {
            monster.Ai.AiState = AiState.Attack;
            monster.Ai.LastAttackTime = now;
        }
        else if (dist > config.SightRange)
        {
            monster.Ai.AiState = AiState.Idle;
            monster.Ai.ChaseTarget = null;
        }
        else
        {
            int moveX = monster.X + Math.Sign(targetPlayer.X - monster.X);
            int moveY = monster.Y + Math.Sign(targetPlayer.Y - monster.Y);
            map.MoveObject(monster.ObjectId, moveX, moveY);
        }
    }

    /// <summary>攻击AI</summary>
    private static void UpdateAiAttack(GameMap map, MonsterEntity monster, long now)
    {
        if (monster.Ai.ChaseTarget == null)
            return;

        var targetPlayer = map.GetPlayer(monster.Ai.ChaseTarget.Value);
        if (targetPlayer == null)
        {
            monster.Ai.AiState = AiState.Idle;
            monster.Ai.ChaseTarget = null;
            return;
        }

        int attackInterval = 2000;
        if (now - monster.Ai.LastAttackTime < attackInterval)
            return;

        FightHelper.UseSkill(monster.ObjectId, 1, monster.X, monster.Y, targetPlayer.RoleId);
        monster.Ai.LastAttackTime = now;

        monster.Ai.AiState = AiState.Idle;
        monster.Ai.ChaseTarget = null;
    }

    /// <summary>查找目标</summary>
    private static long? FindTarget(GameMap map, MonsterEntity monster)
    {
        var sightRange = monster.Info.Config?.SightRange ?? 5;

        foreach (var player in map.GetAllPlayers())
        {
            int dist = Math.Max(
                Math.Abs(player.X - monster.X),
                Math.Abs(player.Y - monster.Y));

            if (dist <= sightRange)
                return player.RoleId;
        }

        return null;
    }

    /// <summary>获取随机巡逻点</summary>
    private static (int X, int Y)? GetRandomPatrolPoint(GameMap map, MonsterEntity monster)
    {
        if (monster.Info.Config == null)
            return null;

        int x = map.Random.Next(monster.Info.Config.SpawnX1, monster.Info.Config.SpawnX2 + 1);
        int y = map.Random.Next(monster.Info.Config.SpawnY1, monster.Info.Config.SpawnY2 + 1);
        return (x, y);
    }

    /// <summary>计算并发放掉落</summary>
    private static void CalculateAndGrantDrop(GameMap map, MonsterEntity monster, long killerId)
    {
        if (monster.Info.Config == null)
            return;

        var player = Context.Players.GetByRoleId(killerId);
        if (player == null)
            return;

        Context.Players.GrantExp(killerId, monster.Info.Config.Exp);

        int gold = map.Random.Next(monster.Info.Config.GoldMin, monster.Info.Config.GoldMax + 1);
        if (gold > 0)
        {
            Context.Players.GrantGold(killerId, gold);
        }

        if (!string.IsNullOrEmpty(monster.Info.Config.DropItems))
        {
            var dropItemIds = monster.Info.Config.DropItems.Split(',');
            foreach (var itemIdStr in dropItemIds)
            {
                if (int.TryParse(itemIdStr.Trim(), out int itemId))
                {
                    if (map.Random.Next(1, 10001) <= monster.Info.Config.DropRate)
                    {
                        Context.Players.GrantItem(killerId, itemId, 1);
                    }
                }
            }
        }

        Log.Info($"怪物掉落 KillerId:{killerId} Exp:{monster.Info.Config.Exp} Gold:{gold}");
    }

    /// <summary>检查刷新计时器</summary>
    private static void CheckSpawnTimers(GameMap map, long now)
    {
        var timersToReset = new List<string>();

        foreach (var timerKvp in map.SpawnTimers)
        {
            if (timerKvp.Value > 0 && now >= timerKvp.Value)
            {
                timersToReset.Add(timerKvp.Key);
            }
        }

        foreach (var timerKey in timersToReset)
        {
            if (ParseTimerKey(timerKey, out int cfgId))
            {
                var cfg = ConfigManager.Get<MonsterConfig>(cfgId);
                if (cfg != null)
                {
                    map.SpawnMonster(cfg);
                }
            }
        }
    }

    private static string GetTimerKey(GameMap map, int cfgId) => $"{map.CfgId}_{map.Line}_{cfgId}";

    private static bool ParseTimerKey(string key, out int cfgId)
    {
        cfgId = 0;
        var parts = key.Split('_');
        if (parts.Length != 3)
            return false;
        return int.TryParse(parts[2], out cfgId);
    }
}
