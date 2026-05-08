using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Game.Components.Bag;
using Game.Components.Fight;
using Game.Components.Map;
using Game.Components.Player;
using Game.Config;
using Game.Core;
using Shared.Common.Config;
using Shared.Common.Protos;
using Shared.Core.Event;
using Shared.Core.Log;
using Shared.Core.Network;
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
            self.MapMonsters[mapKey] = new Dictionary<long, MonsterUnit>();

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
    public static MonsterUnit? SpawnMonster(this MonsterComponent self, int mapId, int line, MonsterConfig cfg)
    {
        var map = Context.Map.GetMap(mapId, line);
        if (map == null) return null;

        // 随机位置
        var random = new Random();
        int x = random.Next(cfg.SpawnX1, cfg.SpawnX2 + 1);
        int y = random.Next(cfg.SpawnY1, cfg.SpawnY2 + 1);

        return SpawnMonsterAt(self, mapId, line, cfg, x, y);
    }

    /// <summary>在指定位置生成怪物</summary>
    public static MonsterUnit? SpawnMonsterAt(this MonsterComponent self, int mapId, int line, MonsterConfig cfg, int x, int y)
    {
        var map = Context.Map.GetMap(mapId, line);
        if (map == null) return null;

        long mapKey = GetMapKey(mapId, line);
        if (!self.MapMonsters.ContainsKey(mapKey))
            self.MapMonsters[mapKey] = new Dictionary<long, MonsterUnit>();

        var mapMonsters = self.MapMonsters[mapKey];

        // 检查当前数量
        var currentCount = mapMonsters.Values.Count(m => m.CfgId == cfg.Id);
        if (currentCount >= cfg.MaxCount)
            return null;

        long monsterId = Interlocked.Increment(ref self.NextMonsterId);

        var monster = new MonsterUnit(monsterId, cfg);

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

        var fightUnit = Context.Fight.RegisterUnit(
            monsterId,
            MapObjectType.Npc,
            cfg.Name,
            cfg.Id,
            fightAttr);

        // 添加到地图
        var npc = new MapNpc((int)monsterId, cfg.Id, cfg.Name, mapId, line, x, y);
        map.AddObject(npc);

        monster.Npc = npc;
        monster.FightUnit = fightUnit;
        monster.X = x;
        monster.Y = y;
        monster.AiState = AiState.Idle;
        monster.LastUpdateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        mapMonsters[monsterId] = monster;

        // 广播给附近玩家
        BroadcastMonsterAppear(mapId, line, monster);

        // 发布怪物生成事件
        EventBus.Instance.Publish(new EventType.MonsterSpawnedEvent
        {
            MonsterId = monsterId,
            CfgId = cfg.Id,
            MapId = mapId,
            Line = line,
        });

        Log.Debug($"生成怪物 MonsterId:{monsterId} CfgId:{cfg.Id} Pos:({x},{y})");
        return monster;
    }

    #endregion

    #region 怪物死亡

    /// <summary>
    /// 怪物死亡
    /// </summary>
    public static void OnMonsterKilled(this MonsterComponent self, long monsterId, long killerId)
    {
        // 从所有地图中查找
        foreach (var kvp in self.MapMonsters)
        {
            if (kvp.Value.TryGetValue(monsterId, out var monster))
            {
                // 移除战斗单元
                Context.Fight.UnregisterUnit(monsterId);

                // 广播死亡
                var mapId = monster.Npc?.MapId ?? 0;
                var line = monster.Npc?.Line ?? 0;
                BroadcastMonsterDeath(mapId, line, monsterId);

                // 从地图移除
                var map = Context.Map.GetMap(mapId, line);
                map?.RemoveObject(monsterId);

                // 移除
                kvp.Value.Remove(monsterId);

                if (monster.Config != null)
                {
                    // 设置刷新计时器
                    string timerKey = GetTimerKey(mapId, line, monster.CfgId);
                    self.SpawnTimers[timerKey] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        + monster.Config.SpawnInterval * 1000;

                    // 计算掉落
                    CalculateAndGrantDrop(self, monster, killerId, mapId, line);
                }

                Log.Info($"怪物死亡 MonsterId:{monsterId} KillerId:{killerId}");
                return;
            }
        }
    }

    /// <summary>
    /// 计算并发放掉落
    /// </summary>
    private static async void CalculateAndGrantDrop(MonsterComponent self, MonsterUnit monster, long killerId, int mapId, int line)
    {
        if (monster.Config == null) return;

        var player = Context.Players.GetByRoleId(killerId);
        if (player == null) return;

        // 发放经验
        await GrantExpToPlayer(killerId, monster.Config.Exp);

        // 发放金币
        var random = new Random();
        int gold = random.Next(monster.Config.GoldMin, monster.Config.GoldMax + 1);
        if (gold > 0)
        {
            await GrantGoldToPlayer(killerId, gold);
        }

        // 计算物品掉落
        if (!string.IsNullOrEmpty(monster.Config.DropItems))
        {
            var dropItemIds = monster.Config.DropItems.Split(',');
            foreach (var itemIdStr in dropItemIds)
            {
                if (int.TryParse(itemIdStr.Trim(), out int itemId))
                {
                    // 按掉落概率判定
                    if (random.Next(1, 10001) <= monster.Config.DropRate)
                    {
                        await GrantItemToPlayer(killerId, itemId, 1);
                    }
                }
            }
        }

        Log.Info($"怪物掉落 KillerId:{killerId} Exp:{monster.Config.Exp} Gold:{gold}");
    }

    /// <summary>
    /// 发放经验给玩家
    /// </summary>
    private static async Task GrantExpToPlayer(long roleId, int exp)
    {
        if (exp <= 0) return;

        var player = Context.Players.GetByRoleId(roleId);
        if (player == null) return;

        var roleAttr = player.RoleAttribute;
        roleAttr.Exp += exp;
        // 检查升级（简化处理）
        int newLevel = CalculateLevel(roleAttr.Exp);
        if (newLevel > roleAttr.Level)
        {
            roleAttr.Level = newLevel;
            roleAttr.MaxHp += 10;
            roleAttr.MaxMp += 5;
            roleAttr.Hp = roleAttr.MaxHp;
            roleAttr.Mp = roleAttr.MaxMp;

            // 发送升级通知
            Context.Players.SendToPlayer(roleId, new LevelUpNtf { Level = newLevel });

            // 发布升级事件
            EventBus.Instance.Publish(new EventType.PlayerLevelUpEvent { RoleId = roleId, NewLevel = newLevel });

            Log.Info($"玩家升级 RoleId:{roleId} Level:{newLevel}");
        }

        await roleAttr.SaveAttributeAsync();

        // 发送经验变化通知
        Context.Players.SendToPlayer(roleId, new ExpChangedNtf { Exp = roleAttr.Exp, Level = roleAttr.Level });

        // 发布经验变化事件
        EventBus.Instance.Publish(new EventType.PlayerExpChangedEvent { RoleId = roleId, Exp = roleAttr.Exp, Level = roleAttr.Level });
    }

    /// <summary>
    /// 发放金币给玩家
    /// </summary>
    private static async Task GrantGoldToPlayer(long roleId, int gold)
    {
        if (gold <= 0) return;

        var player = Context.Players.GetByRoleId(roleId);
        if (player == null) return;

        var roleAttr = player.RoleAttribute;
        roleAttr.Gold += gold;
        await roleAttr.SaveAttributeAsync();

        // 发送金币变化通知
        Context.Players.SendToPlayer(roleId, new GoldChangedNtf { Gold = roleAttr.Gold });

        // 发布金币变化事件
        EventBus.Instance.Publish(new EventType.PlayerGoldChangedEvent { RoleId = roleId, Gold = roleAttr.Gold });
    }

    /// <summary>
    /// 发放物品给玩家
    /// </summary>
    private static async Task GrantItemToPlayer(long roleId, int configId, int count)
    {
        var player = Context.Players.GetByRoleId(roleId);
        if (player == null) return;

        var bag = await player.RoleBag.LoadBagAsync(roleId);
        var result = player.RoleBag.AddItem(configId, count);
        if (result.Success)
        {
            await player.RoleBag.SaveBagAsync();

            // 发送物品获得通知
            Context.Players.SendToPlayer(roleId, new ItemGainNtf
            {
                ConfigId = configId,
                Count = result.AddedCount,
            });

            // 发布物品获得事件
            EventBus.Instance.Publish(new EventType.PlayerItemGainedEvent { RoleId = roleId, ConfigId = configId, Count = result.AddedCount });

            Log.Info($"发放物品 RoleId:{roleId} ConfigId:{configId} Count:{result.AddedCount}");
        }
        else
        {
            Log.Warn($"背包已满，无法发放物品 RoleId:{roleId} ConfigId:{configId}");
        }
    }

    /// <summary>
    /// 根据经验计算等级（简化公式：每级需100*level点经验）
    /// </summary>
    private static int CalculateLevel(int exp)
    {
        int level = 1;
        int totalExp = 0;
        while (true)
        {
            int required = 100 * level;
            if (totalExp + required > exp) break;
            totalExp += required;
            level++;
            if (level >= 100) break; // 封顶100级
        }
        return level;
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
    private static void UpdateMonsterAi(MonsterComponent self, MonsterUnit monster, long now)
    {
        if (monster.Config == null) return;
        if (monster.FightUnit != null && monster.FightUnit.IsDead) return;

        // 更新间隔
        long updateInterval = 1000;
        if (now - monster.LastUpdateTime < updateInterval) return;
        monster.LastUpdateTime = now;

        var map = Context.Map.GetMap(monster.Npc?.MapId ?? 0, monster.Npc?.Line ?? 0);
        if (map == null) return;

        switch (monster.AiState)
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
    private static void UpdateAiIdle(MonsterComponent self, MonsterUnit monster, GameMap map)
    {
        if (monster.Config != null && (monster.Config.AiType == 2 || monster.Config.AiType == 3))
        {
            // 主动或守卫AI，开始巡逻
            monster.AiState = AiState.Patrol;
            monster.PatrolTarget = GetRandomPatrolPoint(monster);
        }
        else
        {
            // 被动AI，查找附近目标
            var targetId = FindTarget(monster, map);
            if (targetId.HasValue)
            {
                monster.AiState = AiState.Chase;
                monster.ChaseTarget = targetId;
            }
        }
    }

    /// <summary>巡逻AI</summary>
    private static void UpdateAiPatrol(MonsterComponent self, MonsterUnit monster, GameMap map, long now)
    {
        // 检查是否有可攻击目标
        var targetId = FindTarget(monster, map);
        if (targetId.HasValue)
        {
            monster.AiState = AiState.Chase;
            monster.ChaseTarget = targetId;
            return;
        }

        // 移动向巡逻点
        if (monster.PatrolTarget.HasValue)
        {
            int dx = monster.PatrolTarget.Value.X - monster.X;
            int dy = monster.PatrolTarget.Value.Y - monster.Y;

            if (Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1)
            {
                // 到达巡逻点，切换新巡逻点
                monster.AiState = AiState.Idle;
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
    private static void UpdateAiChase(MonsterComponent self, MonsterUnit monster, GameMap map, long now)
    {
        if (monster.ChaseTarget == null) return;

        var targetPlayer = map.GetPlayer(monster.ChaseTarget.Value);
        if (targetPlayer == null)
        {
            // 目标丢失，返回待机
            monster.AiState = AiState.Idle;
            monster.ChaseTarget = null;
            return;
        }

        // 计算距离
        int dist = Math.Max(
            Math.Abs(targetPlayer.X - monster.X),
            Math.Abs(targetPlayer.Y - monster.Y));

        if (monster.Config != null && dist <= monster.Config.AttackRange)
        {
            // 进入攻击距离
            monster.AiState = AiState.Attack;
            monster.LastAttackTime = now;
        }
        else if (monster.Config != null && dist > monster.Config.SightRange)
        {
            // 超出视野，返回
            monster.AiState = AiState.Idle;
            monster.ChaseTarget = null;
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
    private static void UpdateAiAttack(MonsterComponent self, MonsterUnit monster, GameMap map, long now)
    {
        if (monster.ChaseTarget == null) return;

        var targetPlayer = map.GetPlayer(monster.ChaseTarget.Value);
        if (targetPlayer == null)
        {
            monster.AiState = AiState.Idle;
            monster.ChaseTarget = null;
            return;
        }

        // 检查攻击间隔
        int attackInterval = 2000; // 2秒攻击一次
        if (now - monster.LastAttackTime < attackInterval) return;

        // 执行攻击（使用普通攻击技能ID=1）
        Context.Fight.UseSkill(monster.ObjectId, 1, monster.X, monster.Y, targetPlayer.RoleId);
        monster.LastAttackTime = now;

        // 丢失目标后返回
        monster.AiState = AiState.Idle;
        monster.ChaseTarget = null;
    }

    /// <summary>查找目标</summary>
    private static long? FindTarget(MonsterUnit monster, GameMap map)
    {
        var sightRange = monster.Config?.SightRange ?? 5;

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
    private static (int X, int Y)? GetRandomPatrolPoint(MonsterUnit monster)
    {
        if (monster.Config == null) return null;

        var random = new Random();
        int x = random.Next(monster.Config.SpawnX1, monster.Config.SpawnX2 + 1);
        int y = random.Next(monster.Config.SpawnY1, monster.Config.SpawnY2 + 1);
        return (x, y);
    }

    /// <summary>尝试移动</summary>
    private static bool TryMove(MonsterComponent self, MonsterUnit monster, GameMap map, int x, int y)
    {
        if (monster.Npc == null) return false;

        if (map.MoveObject(monster.ObjectId, x, y))
        {
            monster.X = x;
            monster.Y = y;

            // 广播移动给附近玩家
            var ntf = new MapNpcMoveNtf
            {
                ObjectId = monster.ObjectId,
                X = x,
                Y = y,
            };
            BroadcastToMap(monster.Npc.MapId, monster.Npc.Line, ntf);

            return true;
        }
        return false;
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

    #region 广播

    /// <summary>广播怪物出现</summary>
    private static void BroadcastMonsterAppear(int mapId, int line, MonsterUnit monster)
    {
        if (monster.Npc == null) return;

        var ntf = new MapNpcEnterNtf
        {
            ObjectId = monster.ObjectId,
            NpcId = monster.CfgId,
            Name = monster.Config?.Name ?? "",
            X = monster.X,
            Y = monster.Y,
        };

        BroadcastToMap(mapId, line, ntf);
    }

    /// <summary>广播怪物死亡</summary>
    private static void BroadcastMonsterDeath(int mapId, int line, long monsterId)
    {
        var ntf = new MapNpcLeaveNtf
        {
            ObjectId = monsterId,
        };

        BroadcastToMap(mapId, line, ntf);
    }

    /// <summary>广播消息给地图上所有玩家</summary>
    private static void BroadcastToMap(int mapId, int line, IMessage msg)
    {
        var map = Context.Map.GetMap(mapId, line);
        if (map == null) return;

        foreach (var player in map.GetAllPlayers())
        {
            Context.Players.SendToPlayer(player.RoleId, msg);
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
