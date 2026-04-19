using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Game.Config;
using Game.Core;
using Game.Game.Bag;
using Game.Game.Fight;
using Game.Game.Map;
using Game.Game.Player;
using Shared.Common.Config;
using Shared.Common.Protos;
using Shared.Core.Log;
using Game.Network;
using Shared.Core.Network;

namespace Game.Game.Monster;

/// <summary>
/// 怪物管理器。管理怪物的出生、刷新、AI、掉落等。
/// </summary>
public class MonsterManager
{
    private static readonly ILog Log = LogManager.GetLogger<MonsterManager>();

    public static MonsterManager Instance { get; } = new();

    /// <summary>地图上的怪物 key=(mapId,line), value=怪物列表</summary>
    private readonly Dictionary<long, Dictionary<long, MonsterUnit>> _mapMonsters = new();

    /// <summary>怪物ID生成器</summary>
    private long _nextMonsterId = 100000;

    /// <summary>刷新计时器 key=(mapId,line,cfgId), value=下次刷新时间</summary>
    private readonly Dictionary<string, long> _spawnTimers = new();

    private readonly object _lock = new();
    private CancellationTokenSource? _updateCts;
    private Task? _updateTask;

    private MonsterManager() { }

    #region 生命周期

    /// <summary>启动怪物管理器</summary>
    public void Start()
    {
        _updateCts = new CancellationTokenSource();
        _updateTask = Task.Run(() => UpdateLoop(_updateCts.Token));
        Log.Info("怪物管理器已启动");
    }

    /// <summary>停止怪物管理器</summary>
    public void Stop()
    {
        _updateCts?.Cancel();
        _updateTask?.Wait(1000);
        _updateCts?.Dispose();
        lock (_lock)
        {
            _mapMonsters.Clear();
        }
        Log.Info("怪物管理器已停止");
    }

    /// <summary>初始化地图怪物（玩家进入地图时调用）</summary>
    public void InitMapMonsters(int mapId, int line)
    {

        var monsters = ConfigComponent.ConfigManager.GetAll<MonsterConfig>()
            .Where(m => m.MapId == mapId)
            .ToList();

        if (monsters.Count == 0) return;

        long mapKey = GetMapKey(mapId, line);
        lock (_lock)
        {
            if (!_mapMonsters.ContainsKey(mapKey))
                _mapMonsters[mapKey] = new Dictionary<long, MonsterUnit>();

            foreach (var cfg in monsters)
            {
                // 初始化刷新计时器
                string timerKey = GetTimerKey(mapId, line, cfg.Id);
                if (!_spawnTimers.ContainsKey(timerKey))
                    _spawnTimers[timerKey] = 0;

                // 生成初始怪物
                for (int i = 0; i < cfg.MaxCount; i++)
                {
                    SpawnMonster(mapId, line, cfg);
                }
            }
        }

        Log.Info($"地图 {mapId} 怪物初始化完成: {monsters.Count} 种怪物");
    }

    #endregion

    #region 怪物生成

    /// <summary>生成怪物</summary>
    public MonsterUnit? SpawnMonster(int mapId, int line, MonsterConfig cfg)
    {
        var map = MapManager.Instance.GetMap(mapId, line);
        if (map == null) return null;

        // 随机位置
        var random = new Random();
        int x = random.Next(cfg.SpawnX1, cfg.SpawnX2 + 1);
        int y = random.Next(cfg.SpawnY1, cfg.SpawnY2 + 1);

        return SpawnMonsterAt(mapId, line, cfg, x, y);
    }

    /// <summary>在指定位置生成怪物</summary>
    public MonsterUnit? SpawnMonsterAt(int mapId, int line, MonsterConfig cfg, int x, int y)
    {
        var map = MapManager.Instance.GetMap(mapId, line);
        if (map == null) return null;

        lock (_lock)
        {
            long mapKey = GetMapKey(mapId, line);
            if (!_mapMonsters.ContainsKey(mapKey))
                _mapMonsters[mapKey] = new Dictionary<long, MonsterUnit>();

            var mapMonsters = _mapMonsters[mapKey];

            // 检查当前数量
            var currentCount = mapMonsters.Values.Count(m => m.CfgId == cfg.Id);
            if (currentCount >= cfg.MaxCount)
                return null;

            long monsterId = Interlocked.Increment(ref _nextMonsterId);

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

            var fightUnit = FightManager.Instance.RegisterUnit(
                monsterId,
                MapObjectType.Monster,
                cfg.Name,
                cfg.Id,
                fightAttr
            );

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

            Log.Debug($"生成怪物 MonsterId:{monsterId} CfgId:{cfg.Id} Pos:({x},{y})");
            return monster;
        }
    }

    #endregion

    #region 怪物死亡

    /// <summary>
    /// 怪物死亡
    /// </summary>
    public void OnMonsterKilled(long monsterId, long killerId)
    {
        lock (_lock)
        {
            // 从所有地图中查找
            foreach (var kvp in _mapMonsters)
            {
                if (kvp.Value.TryGetValue(monsterId, out var monster))
                {
                    // 移除战斗单元
                    FightManager.Instance.UnregisterUnit(monsterId);

                    // 广播死亡
                    var mapId = monster.Npc?.MapId ?? 0;
                    var line = monster.Npc?.Line ?? 0;
                    BroadcastMonsterDeath(mapId, line, monsterId);

                    // 从地图移除
                    var map = MapManager.Instance.GetMap(mapId, line);
                    map?.RemoveObject(monsterId);

                    // 移除
                    kvp.Value.Remove(monsterId);

                    if (monster.Config != null)
                    {
                        // 设置刷新计时器
                        string timerKey = GetTimerKey(mapId, line, monster.CfgId);
                        _spawnTimers[timerKey] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                            + monster.Config.SpawnInterval * 1000;

                        // 计算掉落
                        CalculateAndGrantDrop(monster, killerId, mapId, line);
                    }

                    Log.Info($"怪物死亡 MonsterId:{monsterId} KillerId:{killerId}");
                    return;
                }
            }
        }
    }

    /// <summary>
    /// 计算并发放掉落
    /// </summary>
    private async void CalculateAndGrantDrop(MonsterUnit monster, long killerId, int mapId, int line)
    {
        if (monster.Config == null) return;

        var player = PlayerManager.Instance.GetByRoleId(killerId);
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
    private async Task GrantExpToPlayer(long roleId, int exp)
    {
        if (exp <= 0) return;

        var roleAttr = await ItemEffectManager.Instance.LoadRoleAttributeAsync(roleId);
        if (roleAttr == null) return;

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
            PlayerManager.Instance.SendToPlayer(roleId, new LevelUpNtf { Level = newLevel });
            Log.Info($"玩家升级 RoleId:{roleId} Level:{newLevel}");
        }

        await ItemEffectManager.Instance.SaveRoleAttributeAsync(roleAttr);

        // 发送经验变化通知
        PlayerManager.Instance.SendToPlayer(roleId, new ExpChangedNtf { Exp = roleAttr.Exp, Level = roleAttr.Level });
    }

    /// <summary>
    /// 发放金币给玩家
    /// </summary>
    private async Task GrantGoldToPlayer(long roleId, int gold)
    {
        if (gold <= 0) return;

        var roleAttr = await ItemEffectManager.Instance.LoadRoleAttributeAsync(roleId);
        if (roleAttr == null) return;

        roleAttr.Gold += gold;
        await ItemEffectManager.Instance.SaveRoleAttributeAsync(roleAttr);

        // 发送金币变化通知
        PlayerManager.Instance.SendToPlayer(roleId, new GoldChangedNtf { Gold = roleAttr.Gold });
    }

    /// <summary>
    /// 发放物品给玩家
    /// </summary>
    private async Task GrantItemToPlayer(long roleId, int configId, int count)
    {
        var bag = await BagManager.Instance.LoadBagAsync(roleId);
        var result = BagManager.Instance.AddItem(bag, configId, count);
        if (result.Success)
        {
            await BagManager.Instance.SaveBagAsync(bag);

            // 发送物品获得通知
            PlayerManager.Instance.SendToPlayer(roleId, new ItemGainNtf
            {
                ConfigId = configId,
                Count = result.AddedCount,
            });
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
    private int CalculateLevel(int exp)
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

    /// <summary>AI更新循环</summary>
    private async Task UpdateLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, ct);

                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                List<MonsterUnit> allMonsters;

                lock (_lock)
                {
                    allMonsters = _mapMonsters.Values
                        .SelectMany(m => m.Values)
                        .ToList();
                }

                foreach (var monster in allMonsters)
                {
                    try
                    {
                        UpdateMonsterAi(monster, now);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"AI更新异常 MonsterId:{monster.ObjectId}", ex);
                    }
                }

                // 检查刷新
                CheckSpawnTimers(now);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error("怪物AI更新循环异常", ex);
            }
        }
    }

    /// <summary>更新单个怪物AI</summary>
    private void UpdateMonsterAi(MonsterUnit monster, long now)
    {
        if (monster.Config == null) return;
        if (monster.FightUnit != null && monster.FightUnit.IsDead) return;

        // 更新间隔
        long updateInterval = 1000;
        if (now - monster.LastUpdateTime < updateInterval) return;
        monster.LastUpdateTime = now;

        var map = MapManager.Instance.GetMap(monster.Npc?.MapId ?? 0, monster.Npc?.Line ?? 0);
        if (map == null) return;

        switch (monster.AiState)
        {
            case AiState.Idle:
                UpdateAiIdle(monster, map);
                break;

            case AiState.Patrol:
                UpdateAiPatrol(monster, map, now);
                break;

            case AiState.Chase:
                UpdateAiChase(monster, map, now);
                break;

            case AiState.Attack:
                UpdateAiAttack(monster, map, now);
                break;
        }
    }

    /// <summary>待机AI</summary>
    private void UpdateAiIdle(MonsterUnit monster, GameMap map)
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
    private void UpdateAiPatrol(MonsterUnit monster, GameMap map, long now)
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
                TryMove(monster, map, moveX, moveY);
            }
        }
    }

    /// <summary>追击AI</summary>
    private void UpdateAiChase(MonsterUnit monster, GameMap map, long now)
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
            Math.Abs(targetPlayer.Y - monster.Y)
        );

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
            TryMove(monster, map, moveX, moveY);
        }
    }

    /// <summary>攻击AI</summary>
    private void UpdateAiAttack(MonsterUnit monster, GameMap map, long now)
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
        var result = FightManager.Instance.UseSkill(monster.ObjectId, 1, monster.X, monster.Y, targetPlayer.RoleId);
        monster.LastAttackTime = now;

        // 丢失目标后返回
        monster.AiState = AiState.Idle;
        monster.ChaseTarget = null;
    }

    /// <summary>查找目标</summary>
    private long? FindTarget(MonsterUnit monster, GameMap map)
    {
        var sightRange = monster.Config?.SightRange ?? 5;

        var players = map.GetAllPlayers();
        foreach (var player in players)
        {
            int dist = Math.Max(
                Math.Abs(player.X - monster.X),
                Math.Abs(player.Y - monster.Y)
            );

            if (dist <= sightRange)
                return player.RoleId;
        }

        return null;
    }

    /// <summary>获取随机巡逻点</summary>
    private (int X, int Y)? GetRandomPatrolPoint(MonsterUnit monster)
    {
        if (monster.Config == null) return null;

        var random = new Random();
        int x = random.Next(monster.Config.SpawnX1, monster.Config.SpawnX2 + 1);
        int y = random.Next(monster.Config.SpawnY1, monster.Config.SpawnY2 + 1);
        return (x, y);
    }

    /// <summary>尝试移动</summary>
    private bool TryMove(MonsterUnit monster, GameMap map, int x, int y)
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
    private void CheckSpawnTimers(long now)
    {
        var timersToReset = new List<string>();

        foreach (var timerKvp in _spawnTimers)
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
                var cfg = ConfigComponent.ConfigManager.Get<MonsterConfig>(cfgId);
                if (cfg != null)
                {
                    SpawnMonster(mapId, line, cfg);
                }
            }
        }
    }

    #endregion

    #region 广播

    /// <summary>广播怪物出现</summary>
    private void BroadcastMonsterAppear(int mapId, int line, MonsterUnit monster)
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
    private void BroadcastMonsterDeath(int mapId, int line, long monsterId)
    {
        var ntf = new MapNpcLeaveNtf
        {
            ObjectId = monsterId,
        };

        BroadcastToMap(mapId, line, ntf);
    }

    /// <summary>广播消息给地图上所有玩家</summary>
    private void BroadcastToMap(int mapId, int line, IMessage msg)
    {
        var map = MapManager.Instance.GetMap(mapId, line);
        if (map == null) return;

        foreach (var player in map.GetAllPlayers())
        {
            PlayerManager.Instance.SendToPlayer(player.RoleId, msg);
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
    public int GetMapMonsterCount(int mapId, int line)
    {
        lock (_lock)
        {
            long mapKey = GetMapKey(mapId, line);
            return _mapMonsters.TryGetValue(mapKey, out var monsters)
                ? monsters.Count : 0;
        }
    }

    #endregion
}

/// <summary>
/// 怪物单位
/// </summary>
public class MonsterUnit
{
    /// <summary>怪物唯一ID</summary>
    public long ObjectId { get; }

    /// <summary>配置</summary>
    public MonsterConfig? Config { get; }

    /// <summary>战斗单元</summary>
    public FightUnit? FightUnit { get; set; }

    /// <summary>地图NPC对象</summary>
    public MapNpc? Npc { get; set; }

    /// <summary>当前X坐标</summary>
    public int X { get; set; }

    /// <summary>当前Y坐标</summary>
    public int Y { get; set; }

    /// <summary>AI状态</summary>
    public AiState AiState { get; set; } = AiState.Idle;

    /// <summary>巡逻目标</summary>
    public (int X, int Y)? PatrolTarget { get; set; }

    /// <summary>追击目标（玩家RoleId）</summary>
    public long? ChaseTarget { get; set; }

    /// <summary>上次攻击时间</summary>
    public long LastAttackTime { get; set; }

    /// <summary>上次AI更新时间</summary>
    public long LastUpdateTime { get; set; }

    /// <summary>配置ID</summary>
    public int CfgId => Config?.Id ?? 0;

    public MonsterUnit(long objectId, MonsterConfig config)
    {
        ObjectId = objectId;
        Config = config;
    }
}

/// <summary>
/// AI状态
/// </summary>
public enum AiState
{
    Idle = 0,
    Patrol = 1,
    Chase = 2,
    Attack = 3,
}
