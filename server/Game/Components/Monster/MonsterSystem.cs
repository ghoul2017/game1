using System;
using System.Collections.Generic;
using System.Linq;
using Game.Components.Fight;
using Game.Components.Map;
using Game.Components.Player;
using Game.Config;
using Shared.Common.Config;
using Shared.Core;
using Shared.Core.Event;
using Shared.Core.Log;
using Task = System.Threading.Tasks.Task;

namespace Game.Components.Monster;

/// <summary>
/// 怪物系统。提供怪物生成、刷新、AI、掉落等功能。
/// 所有操作通过 MonsterComponent 扩展方法实现，无状态设计。
/// </summary>
public static class MonsterSystem
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(MonsterSystem));

    #region 生命周期

    /// <summary>启动怪物管理器</summary>
    public static void Start(this MonsterComponent self)
    {
        self.UpdateTimer = GameTimer.Start(1000, () => UpdateAllMonsters(self));
        Log.Info("怪物管理器已启动");
    }

    /// <summary>停止怪物管理器</summary>
    public static void Stop(this MonsterComponent self)
    {
        GameTimer.Stop(self.UpdateTimer);
        self.MapMonsters.Clear();
        Log.Info("怪物管理器已停止");
    }

    #endregion

    #region 初始化

    /// <summary>初始化地图怪物（玩家进入地图时调用）</summary>
    public static void InitMapMonsters(this MonsterComponent self, int mapId, int line)
    {
        var monsters = ConfigManager.GetAll<MonsterConfig>()
            .Where(m => m.MapId == mapId)
            .ToList();

        if (monsters.Count == 0) return;

        long mapKey = GetMapKey(mapId, line);
        if (!self.MapMonsters.ContainsKey(mapKey))
            self.MapMonsters[mapKey] = new Dictionary<long, MonsterEntity>();

        foreach (var cfg in monsters)
        {
            // 初始化刷新计时器
            string timerKey = GetTimerKey(mapId, line, cfg.Id);
            if (!self.SpawnTimers.ContainsKey(timerKey))
                self.SpawnTimers[timerKey] = 0;

            // 生成初始怪物
            for (int i = 0; i < cfg.MaxCount; i++)
            {
                self.SpawnMonster(mapId, line, cfg);
            }
        }

        Log.Info($"地图 {mapId} 怪物初始化完成: {monsters.Count} 种怪物");
    }

    #endregion

    #region 怪物生成

    /// <summary>生成怪物</summary>
    public static MonsterEntity? SpawnMonster(this MonsterComponent self, int mapId, int line, MonsterConfig cfg)
    {
        var map = Context.Map.GetMap(mapId, line);
        if (map == null) return null;

        // 随机位置
        int x = self.Random.Next(cfg.SpawnX1, cfg.SpawnX2 + 1);
        int y = self.Random.Next(cfg.SpawnY1, cfg.SpawnY2 + 1);

        return SpawnMonsterAt(self, mapId, line, cfg, x, y);
    }

    /// <summary>在指定位置生成怪物</summary>
    public static MonsterEntity? SpawnMonsterAt(this MonsterComponent self, int mapId, int line, MonsterConfig cfg, int x, int y)
    {
        var map = Context.Map.GetMap(mapId, line);
        if (map == null) return null;

        long mapKey = GetMapKey(mapId, line);
        if (!self.MapMonsters.ContainsKey(mapKey))
            self.MapMonsters[mapKey] = new Dictionary<long, MonsterEntity>();

        var mapMonsters = self.MapMonsters[mapKey];

        // 检查当前数量
        var currentCount = mapMonsters.Values.Count(m => m.CfgId == cfg.Id);
        if (currentCount >= cfg.MaxCount)
            return null;

        long monsterId = self.NextMonsterId++;

        var monster = MonsterEntity.Create(monsterId, cfg, mapId, line, x, y);

        // 创建战斗单元
        var fightAttr = new FightAttribute
        {
            MaxHp = cfg.MaxHp,
            Hp = cfg.MaxHp,
            PhyAttMin = cfg.Attack,
            PhyAttMax = cfg.Attack,
            PhyDefMin = cfg.Defense,
            PhyDefMax = cfg.Defense,
        };

        monster.RegisterUnit(fightAttr);
        monster.Fight.AddSkill(new Skill(1));

        // 添加到地图
        map.AddObject(monster);

        mapMonsters[monsterId] = monster;

        Log.Debug($"生成怪物 MonsterId:{monsterId} CfgId:{cfg.Id} Pos:({x},{y})");
        return monster;
    }

    #endregion

    #region 怪物死亡

    /// <summary>怪物死亡</summary>
    public static void OnMonsterKilled(this MonsterComponent self, long monsterId, long killerId)
    {
        // 从所有地图中查找
        foreach (var kvp in self.MapMonsters)
        {
            if (kvp.Value.TryGetValue(monsterId, out var monster))
            {
                // 注销战斗单元
                monster.UnregisterUnit();

                // 从地图移除
                int mapId = monster.MapId;
                int line = monster.Line;
                var map = Context.Map.GetMap(mapId, line);
                map?.RemoveObject(monsterId);

                // 移除
                kvp.Value.Remove(monsterId);

                if (monster.Info.Config != null)
                {
                    // 设置刷新计时器
                    string timerKey = GetTimerKey(mapId, line, monster.CfgId);
                    self.SpawnTimers[timerKey] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        + monster.Info.Config.SpawnInterval * 1000;

                    // 计算掉落
                    _ = CalculateAndGrantDropAsync(self, monster, killerId);
                }

                Log.Info($"怪物死亡 MonsterId:{monsterId} KillerId:{killerId}");
                return;
            }
        }
    }

    /// <summary>计算并发放掉落</summary>
    private static async Task CalculateAndGrantDropAsync(MonsterComponent self, MonsterEntity monster, long killerId)
    {
        if (monster.Info.Config == null) return;

        var player = Context.Players.GetByRoleId(killerId);
        if (player == null) return;

        // 发放经验
        await Context.Players.GrantExpAsync(killerId, monster.Info.Config.Exp);

        // 发放金币
        int gold = self.Random.Next(monster.Info.Config.GoldMin, monster.Info.Config.GoldMax + 1);
        if (gold > 0)
        {
            await Context.Players.GrantGoldAsync(killerId, gold);
        }

        // 计算物品掉落
        if (!string.IsNullOrEmpty(monster.Info.Config.DropItems))
        {
            var dropItemIds = monster.Info.Config.DropItems.Split(',');
            foreach (var itemIdStr in dropItemIds)
            {
                if (int.TryParse(itemIdStr.Trim(), out int itemId))
                {
                    if (self.Random.Next(1, 10001) <= monster.Info.Config.DropRate)
                    {
                        await Context.Players.GrantItemAsync(killerId, itemId, 1);
                    }
                }
            }
        }

        Log.Info($"怪物掉落 KillerId:{killerId} Exp:{monster.Info.Config.Exp} Gold:{gold}");
    }

    #endregion

    #region AI更新

    /// <summary>AI更新（由 GameTimer 每秒触发，在主线程执行）</summary>
    private static void UpdateAllMonsters(MonsterComponent self)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var allMonsters = self.MapMonsters.Values
            .SelectMany(m => m.Values)
            .ToList();

        foreach (var monster in allMonsters)
        {
            try
            {
                UpdateMonsterAi(self, monster, now);
            }
            catch (Exception ex)
            {
                Log.Error($"AI更新异常 MonsterId:{monster.ObjectId}", ex);
            }
        }

        // 检查刷新
        CheckSpawnTimers(self, now);
    }

    /// <summary>更新单个怪物AI</summary>
    private static void UpdateMonsterAi(MonsterComponent self, MonsterEntity monster, long now)
    {
        if (monster.Info.Config == null) return;
        if (monster.Fight.IsDead) return;

        // 更新间隔
        long updateInterval = 1000;
        if (now - monster.Ai.LastUpdateTime < updateInterval) return;
        monster.Ai.LastUpdateTime = now;

        var map = Context.Map.GetMap(monster.MapId, monster.Line);
        if (map == null) return;

        switch (monster.Ai.AiState)
        {
            case AiState.Idle:
                UpdateAiIdle(self, monster, map);
                break;

            case AiState.Patrol:
                UpdateAiPatrol(self, monster, map, now);
                break;

            case AiState.Chase:
                UpdateAiChase(self, monster, map, now);
                break;

            case AiState.Attack:
                UpdateAiAttack(self, monster, map, now);
                break;
        }
    }

    /// <summary>待机AI</summary>
    private static void UpdateAiIdle(MonsterComponent self, MonsterEntity monster, GameMap map)
    {
        if (monster.Info.Config != null && (monster.Info.Config.AiType == 2 || monster.Info.Config.AiType == 3))
        {
            // 主动或守卫AI，开始巡逻
            monster.Ai.AiState = AiState.Patrol;
            monster.Ai.PatrolTarget = GetRandomPatrolPoint(self, monster);
        }
        else
        {
            // 被动AI，查找附近目标
            var targetId = FindTarget(monster, map);
            if (targetId.HasValue)
            {
                monster.Ai.AiState = AiState.Chase;
                monster.Ai.ChaseTarget = targetId;
            }
        }
    }

    /// <summary>巡逻AI</summary>
    private static void UpdateAiPatrol(MonsterComponent self, MonsterEntity monster, GameMap map, long now)
    {
        // 检查是否有可攻击目标
        var targetId = FindTarget(monster, map);
        if (targetId.HasValue)
        {
            monster.Ai.AiState = AiState.Chase;
            monster.Ai.ChaseTarget = targetId;
            return;
        }

        // 移动向巡逻点
        if (monster.Ai.PatrolTarget.HasValue)
        {
            int dx = monster.Ai.PatrolTarget.Value.X - monster.X;
            int dy = monster.Ai.PatrolTarget.Value.Y - monster.Y;

            if (Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1)
            {
                // 到达巡逻点，切换新巡逻点
                monster.Ai.AiState = AiState.Idle;
            }
            else
            {
                // 移动
                int moveX = monster.X + Math.Sign(dx);
                int moveY = monster.Y + Math.Sign(dy);
                TryMove(self, monster, map, moveX, moveY);
            }
        }
    }

    /// <summary>追击AI</summary>
    private static void UpdateAiChase(MonsterComponent self, MonsterEntity monster, GameMap map, long now)
    {
        if (monster.Ai.ChaseTarget == null) return;

        var targetPlayer = map.GetPlayer(monster.Ai.ChaseTarget.Value);
        if (targetPlayer == null)
        {
            // 目标丢失，返回待机
            monster.Ai.AiState = AiState.Idle;
            monster.Ai.ChaseTarget = null;
            return;
        }

        // 计算距离
        int dist = Math.Max(
            Math.Abs(targetPlayer.X - monster.X),
            Math.Abs(targetPlayer.Y - monster.Y));

        if (monster.Info.Config != null && dist <= monster.Info.Config.AttackRange)
        {
            // 进入攻击距离
            monster.Ai.AiState = AiState.Attack;
            monster.Ai.LastAttackTime = now;
        }
        else if (monster.Info.Config != null && dist > monster.Info.Config.SightRange)
        {
            // 超出视野，返回
            monster.Ai.AiState = AiState.Idle;
            monster.Ai.ChaseTarget = null;
        }
        else
        {
            // 追击
            int moveX = monster.X + Math.Sign(targetPlayer.X - monster.X);
            int moveY = monster.Y + Math.Sign(targetPlayer.Y - monster.Y);
            TryMove(self, monster, map, moveX, moveY);
        }
    }

    /// <summary>攻击AI</summary>
    private static void UpdateAiAttack(MonsterComponent self, MonsterEntity monster, GameMap map, long now)
    {
        if (monster.Ai.ChaseTarget == null) return;

        var targetPlayer = map.GetPlayer(monster.Ai.ChaseTarget.Value);
        if (targetPlayer == null)
        {
            monster.Ai.AiState = AiState.Idle;
            monster.Ai.ChaseTarget = null;
            return;
        }

        // 检查攻击间隔
        int attackInterval = 2000; // 2秒攻击一次
        if (now - monster.Ai.LastAttackTime < attackInterval) return;

        // 执行攻击（使用普通攻击技能ID=1）
        FightSystem.UseSkill(monster.ObjectId, 1, monster.X, monster.Y, targetPlayer.RoleId);
        monster.Ai.LastAttackTime = now;

        // 丢失目标后返回
        monster.Ai.AiState = AiState.Idle;
        monster.Ai.ChaseTarget = null;
    }

    /// <summary>查找目标</summary>
    private static long? FindTarget(MonsterEntity monster, GameMap map)
    {
        var sightRange = monster.Info.Config?.SightRange ?? 5;

        var players = map.GetAllPlayers();
        foreach (var player in players)
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
    private static (int X, int Y)? GetRandomPatrolPoint(MonsterComponent self, MonsterEntity monster)
    {
        if (monster.Info.Config == null) return null;

        int x = self.Random.Next(monster.Info.Config.SpawnX1, monster.Info.Config.SpawnX2 + 1);
        int y = self.Random.Next(monster.Info.Config.SpawnY1, monster.Info.Config.SpawnY2 + 1);
        return (x, y);
    }

    /// <summary>尝试移动</summary>
    private static bool TryMove(MonsterComponent self, MonsterEntity monster, GameMap map, int x, int y)
    {
        return map.MoveObject(monster.ObjectId, x, y);
    }

    /// <summary>检查刷新计时器</summary>
    private static void CheckSpawnTimers(MonsterComponent self, long now)
    {
        var timersToReset = new List<string>();

        foreach (var timerKvp in self.SpawnTimers)
        {
            if (timerKvp.Value > 0 && now >= timerKvp.Value)
            {
                timersToReset.Add(timerKvp.Key);
            }
        }

        foreach (var timerKey in timersToReset)
        {
            if (ParseTimerKey(timerKey, out int mapId, out int line, out int cfgId))
            {
                var cfg = ConfigManager.Get<MonsterConfig>(cfgId);
                if (cfg != null)
                {
                    self.SpawnMonster(mapId, line, cfg);
                }
            }
        }
    }

    #endregion

    #region 辅助方法

    private static long GetMapKey(int mapId, int line) => (long)mapId * 1000 + line;

    private static string GetTimerKey(int mapId, int line, int cfgId) =>
        $"{mapId}_{line}_{cfgId}";

    private static bool ParseTimerKey(string key, out int mapId, out int line, out int cfgId)
    {
        mapId = 0; line = 0; cfgId = 0;
        var parts = key.Split('_');
        if (parts.Length != 3) return false;
        return int.TryParse(parts[0], out mapId) &&
               int.TryParse(parts[1], out line) &&
               int.TryParse(parts[2], out cfgId);
    }

    /// <summary>获取地图怪物数量</summary>
    public static int GetMapMonsterCount(this MonsterComponent self, int mapId, int line)
    {
        long mapKey = GetMapKey(mapId, line);
        return self.MapMonsters.TryGetValue(mapKey, out var monsters)
            ? monsters.Count : 0;
    }

    #endregion
}
