using System;
using System.Collections.Generic;
using System.Linq;
using Game.Components.Fight;
using Game.Components.Monster;
using Game.Components.Player;
using Game.Entities;
using Game.Core.Aoi;
using Shared.Core;
using Shared.Core.Log;

namespace Game.Components.Map;

/// <summary>
/// 地图实例。管理一张地图上的所有对象（玩家投影/怪物），集成 AOI 系统。
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
    public readonly Dictionary<string, long> SpawnTimers = new();

    /// <summary>AI 更新定时器</summary>
    private GameTimerHandle _aiTimer;

    /// <summary>共享随机数</summary>
    public readonly Random Random = new();

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

    /// <summary>获取地图上的怪物</summary>
    public MonsterEntity? GetMonster(long monsterId)
    {
        return _monsters.TryGetValue(monsterId, out var m) ? m : null;
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

    /// <summary>AI更新入口（由 GameTimer 每秒触发，委托给 MonsterHelper）</summary>
    private void UpdateAllMonsters()
    {
        MonsterHelper.UpdateAllMonsters(this);
    }
}
