using System;
using System.Collections.Generic;
using System.Linq;
using Game.Components.Fight;
using Game.Components.Monster;
using Game.Components.Player;
using Game.Entities;
using Game.Core.Aoi;
using Shared.Core;
using Shared.Core.Config;
using Shared.Common.Config;
using Shared.Core.Log;
using Task = System.Threading.Tasks.Task;

namespace Game.Components.Map;

/// <summary>
/// 地图实例。管理一张地图上的所有对象（玩家投影/怪物），集成 AOI 系统和怪物 AI。
/// </summary>
public class GameMap
{
    private static readonly ILog Log = LogManager.GetLogger<GameMap>();

    /// <summary>地图配置ID</summary>
    public int CfgId { get; }

    /// <summary>分线ID</summary>
    public int Line { get; }

    /// <summary>地图名称</summary>
    public string Name { get; }

    /// <summary>地图宽度（格子数）</summary>
    public int Width { get; }

    /// <summary>地图高度（格子数）</summary>
    public int Height { get; }

    /// <summary>默认出生X坐标</summary>
    public int BirthX { get; }

    /// <summary>默认出生Y坐标</summary>
    public int BirthY { get; }

    /// <summary>AOI 系统</summary>
    public TowerAoi Aoi { get; }

    /// <summary>地图上所有对象</summary>
    private readonly Dictionary<long, MapObjectEntity> _objects = new();

    /// <summary>地图上的玩家投影</summary>
    private readonly Dictionary<long, MapPlayerEntity> _players = new();

    /// <summary>地图上的怪物</summary>
    private readonly Dictionary<long, MonsterEntity> _monsters = new();

    /// <summary>刷怪计时器（timerKey → 下次刷新时间）</summary>
    private readonly Dictionary<string, long> _spawnTimers = new();

    /// <summary>AI 更新定时器</summary>
    private GameTimerHandle _aiTimer;

    /// <summary>共享随机数</summary>
    private readonly Random _random = new();

    public GameMap(int cfgId, int line, string name, int width, int height, int birthX, int birthY)
    {
        CfgId = cfgId;
        Line = line;
        Name = name;
        Width = width;
        Height = height;
        BirthX = birthX;
        BirthY = birthY;
        Aoi = new TowerAoi();
        Aoi.AddListener(new MapAoiNotifier());
    }

    /// <summary>地图唯一标识（cfgId * 1000 + line）</summary>
    public long MapKey => (long)CfgId * 1000 + Line;

    /// <summary>当前地图玩家数</summary>
    public int PlayerCount => _players.Count;

    /// <summary>当前地图对象总数</summary>
    public int ObjectCount => _objects.Count;

    /// <summary>当前地图怪物数</summary>
    public int MonsterCount => _monsters.Count;

    /// <summary>添加对象到地图</summary>
    public bool AddObject(MapObjectEntity obj)
    {
        if (_objects.ContainsKey(obj.ObjectId))
        {
            Log.Warn($"对象已存在地图中 ObjectId:{obj.ObjectId}");
            return false;
        }

        obj.MapId = CfgId;
        obj.Line = Line;

        _objects[obj.ObjectId] = obj;

        switch (obj)
        {
            case MapPlayerEntity player:
                _players[player.RoleId] = player;
                Aoi.AddAoiEntity(obj, 4);
                break;
            case MonsterEntity monster:
                _monsters[monster.ObjectId] = monster;
                Aoi.AddAoiEntity(obj);
                break;
            default:
                Aoi.AddAoiEntity(obj);
                break;
        }

        Log.Debug($"对象进入地图 ObjectId:{obj.ObjectId} MapId:{CfgId} Line:{Line} Pos:({obj.X},{obj.Y})");
        return true;
    }

    /// <summary>从地图移除对象</summary>
    public bool RemoveObject(long objectId)
    {
        if (!_objects.Remove(objectId, out var obj))
            return false;

        switch (obj)
        {
            case MapPlayerEntity:
                _players.Remove(objectId);
                break;
            case MonsterEntity:
                _monsters.Remove(objectId);
                break;
        }

        Aoi.RemoveAoiEntity(obj);

        obj.MapId = 0;
        obj.Line = 0;

        Log.Debug($"对象离开地图 ObjectId:{objectId} MapId:{CfgId} Line:{Line}");
        return true;
    }

    /// <summary>获取地图上的对象</summary>
    public MapObjectEntity? GetObject(long objectId)
    {
        return _objects.TryGetValue(objectId, out var obj) ? obj : null;
    }

    /// <summary>获取地图上的玩家投影</summary>
    public MapPlayerEntity? GetPlayer(long roleId)
    {
        return _players.TryGetValue(roleId, out var p) ? p : null;
    }

    /// <summary>获取地图上所有玩家投影（快照）</summary>
    public List<MapPlayerEntity> GetAllPlayers()
    {
        return new List<MapPlayerEntity>(_players.Values);
    }

    /// <summary>获取地图上所有怪物（快照）</summary>
    public List<MonsterEntity> GetAllMonsters()
    {
        return new List<MonsterEntity>(_monsters.Values);
    }

    /// <summary>获取视野范围内的对象</summary>
    public List<MapObjectEntity> GetObjectsInRange(int x, int y, int range)
    {
        return Aoi.GetObjectsByRange(x, y, range)
            .Cast<MapObjectEntity>()
            .ToList();
    }

    /// <summary>更新对象位置（移动）</summary>
    public bool MoveObject(long objectId, int newX, int newY)
    {
        if (!_objects.TryGetValue(objectId, out var obj))
            return false;

        // 边界检查
        if (newX < 0 || newX >= Width || newY < 0 || newY >= Height)
        {
            Log.Warn($"移动坐标超出范围 ObjectId:{objectId} ({newX},{newY}) MapSize:{Width}x{Height}");
            return false;
        }

        var oldX = obj.X;
        var oldY = obj.Y;

        Aoi.UpdateAoiEntity(obj, oldX, oldY, newX, newY);

        return true;
    }

    /// <summary>获取地图怪物数量（按配置ID）</summary>
    public int GetMonsterCountByCfgId(int cfgId)
    {
        return _monsters.Values.Count(m => m.CfgId == cfgId);
    }

    // 刷怪

    /// <summary>初始化地图怪物（地图创建时调用）</summary>
    public void InitMonsters()
    {
        var monsters = ConfigManager.GetAll<MonsterConfig>()
            .Where(m => m.MapId == CfgId)
            .ToList();

        if (monsters.Count == 0) return;

        foreach (var cfg in monsters)
        {
            string timerKey = GetTimerKey(cfg.Id);
            if (!_spawnTimers.ContainsKey(timerKey))
                _spawnTimers[timerKey] = 0;

            for (int i = 0; i < cfg.MaxCount; i++)
            {
                SpawnMonster(cfg);
            }
        }

        Log.Info($"地图 {CfgId} 怪物初始化完成: {monsters.Count} 种怪物");
    }

    /// <summary>在刷新区域内随机位置生成怪物</summary>
    public MonsterEntity? SpawnMonster(MonsterConfig cfg)
    {
        int x = _random.Next(cfg.SpawnX1, cfg.SpawnX2 + 1);
        int y = _random.Next(cfg.SpawnY1, cfg.SpawnY2 + 1);
        return SpawnMonsterAt(cfg, x, y);
    }

    /// <summary>在指定位置生成怪物</summary>
    public MonsterEntity? SpawnMonsterAt(MonsterConfig cfg, int x, int y)
    {
        // 检查当前数量
        if (GetMonsterCountByCfgId(cfg.Id) >= cfg.MaxCount)
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

        var monster = MonsterEntity.Create(monsterId, cfg, CfgId, Line, x, y, fightAttr);

        AddObject(monster);

        Log.Debug($"生成怪物 MonsterId:{monsterId} CfgId:{cfg.Id} Pos:({x},{y})");
        return monster;
    }

    // AI 定时器

    /// <summary>启动 AI 更新定时器</summary>
    public void StartAiTimer()
    {
        _aiTimer = GameTimer.Start(1000, UpdateAllMonsters);
        Log.Info($"地图 {CfgId} AI 定时器已启动");
    }

    /// <summary>停止 AI 更新定时器</summary>
    public void StopAiTimer()
    {
        GameTimer.Stop(_aiTimer);
        Log.Info($"地图 {CfgId} AI 定时器已停止");
    }

    // AI 更新

    /// <summary>AI更新（由 GameTimer 每秒触发，在主线程执行）</summary>
    private void UpdateAllMonsters()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var snapshot = _monsters.Values.ToList();

        foreach (var monster in snapshot)
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

        CheckSpawnTimers(now);
    }

    /// <summary>更新单个怪物AI</summary>
    private void UpdateMonsterAi(MonsterEntity monster, long now)
    {
        if (monster.Info.Config == null) return;
        if (monster.Fight.IsDead) return;

        long updateInterval = 1000;
        if (now - monster.Ai.LastUpdateTime < updateInterval) return;
        monster.Ai.LastUpdateTime = now;

        switch (monster.Ai.AiState)
        {
            case AiState.Idle:
                UpdateAiIdle(monster);
                break;

            case AiState.Patrol:
                UpdateAiPatrol(monster, now);
                break;

            case AiState.Chase:
                UpdateAiChase(monster, now);
                break;

            case AiState.Attack:
                UpdateAiAttack(monster, now);
                break;
        }
    }

    /// <summary>待机AI</summary>
    private void UpdateAiIdle(MonsterEntity monster)
    {
        if (monster.Info.Config != null && (monster.Info.Config.AiType == 2 || monster.Info.Config.AiType == 3))
        {
            // 主动或守卫AI，开始巡逻
            monster.Ai.AiState = AiState.Patrol;
            monster.Ai.PatrolTarget = GetRandomPatrolPoint(monster);
        }
        else
        {
            // 被动AI，查找附近目标
            var targetId = FindTarget(monster);
            if (targetId.HasValue)
            {
                monster.Ai.AiState = AiState.Chase;
                monster.Ai.ChaseTarget = targetId;
            }
        }
    }

    /// <summary>巡逻AI</summary>
    private void UpdateAiPatrol(MonsterEntity monster, long now)
    {
        // 检查是否有可攻击目标
        var targetId = FindTarget(monster);
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
                MoveObject(monster.ObjectId, moveX, moveY);
            }
        }
    }

    /// <summary>追击AI</summary>
    private void UpdateAiChase(MonsterEntity monster, long now)
    {
        if (monster.Ai.ChaseTarget == null) return;

        var targetPlayer = GetPlayer(monster.Ai.ChaseTarget.Value);
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
            MoveObject(monster.ObjectId, moveX, moveY);
        }
    }

    /// <summary>攻击AI</summary>
    private void UpdateAiAttack(MonsterEntity monster, long now)
    {
        if (monster.Ai.ChaseTarget == null) return;

        var targetPlayer = GetPlayer(monster.Ai.ChaseTarget.Value);
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
    private long? FindTarget(MonsterEntity monster)
    {
        var sightRange = monster.Info.Config?.SightRange ?? 5;

        foreach (var player in _players.Values)
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
    private (int X, int Y)? GetRandomPatrolPoint(MonsterEntity monster)
    {
        if (monster.Info.Config == null) return null;

        int x = _random.Next(monster.Info.Config.SpawnX1, monster.Info.Config.SpawnX2 + 1);
        int y = _random.Next(monster.Info.Config.SpawnY1, monster.Info.Config.SpawnY2 + 1);
        return (x, y);
    }

    // 怪物死亡

    /// <summary>怪物死亡</summary>
    public void OnMonsterKilled(long monsterId, long killerId)
    {
        if (!_monsters.TryGetValue(monsterId, out var monster)) return;

        // 从地图移除
        RemoveObject(monsterId);

        if (monster.Info.Config != null)
        {
            // 设置刷新定时器
            string timerKey = GetTimerKey(monster.CfgId);
            _spawnTimers[timerKey] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                + monster.Info.Config.SpawnInterval * 1000;

            // 计算并发放掉落
            _ = CalculateAndGrantDropAsync(monster, killerId);
        }

        Log.Info($"怪物死亡 MonsterId:{monsterId} KillerId:{killerId}");
    }

    /// <summary>计算并发放掉落</summary>
    private async Task CalculateAndGrantDropAsync(MonsterEntity monster, long killerId)
    {
        if (monster.Info.Config == null) return;

        var player = Context.Players.GetByRoleId(killerId);
        if (player == null) return;

        // 发放经验
        await Context.Players.GrantExpAsync(killerId, monster.Info.Config.Exp);

        // 发放金币
        int gold = _random.Next(monster.Info.Config.GoldMin, monster.Info.Config.GoldMax + 1);
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
                    if (_random.Next(1, 10001) <= monster.Info.Config.DropRate)
                    {
                        await Context.Players.GrantItemAsync(killerId, itemId, 1);
                    }
                }
            }
        }

        Log.Info($"怪物掉落 KillerId:{killerId} Exp:{monster.Info.Config.Exp} Gold:{gold}");
    }

    // 刷新计时器

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
            if (ParseTimerKey(timerKey, out int cfgId))
            {
                var cfg = ConfigManager.Get<MonsterConfig>(cfgId);
                if (cfg != null)
                {
                    SpawnMonster(cfg);
                }
            }
        }
    }

    private string GetTimerKey(int cfgId) => $"{CfgId}_{Line}_{cfgId}";

    private static bool ParseTimerKey(string key, out int cfgId)
    {
        cfgId = 0;
        var parts = key.Split('_');
        if (parts.Length != 3) return false;
        return int.TryParse(parts[2], out cfgId);
    }
}
