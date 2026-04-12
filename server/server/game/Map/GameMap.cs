using Server.Core.Log;
using Server.Game.Aoi;

namespace Server.Game.Map;

/// <summary>
/// 地图实例。管理一张地图上的所有对象（玩家/NPC/Monster），集成 AOI 系统。
/// 参考: Java 项目的 GameMap。
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
    private readonly Dictionary<long, IMapObject> _objects = new();

    /// <summary>地图上的玩家</summary>
    private readonly Dictionary<long, MapPlayer> _players = new();

    /// <summary>地图上的 NPC</summary>
    private readonly Dictionary<long, MapNpc> _npcs = new();

    private readonly object _lock = new();

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
    public int PlayerCount
    {
        get { lock (_lock) { return _players.Count; } }
    }

    /// <summary>当前地图对象总数</summary>
    public int ObjectCount
    {
        get { lock (_lock) { return _objects.Count; } }
    }

    #region 对象管理

    /// <summary>添加对象到地图</summary>
    public bool AddObject(IMapObject obj)
    {
        lock (_lock)
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
                case MapPlayer player:
                    _players[player.RoleId] = player;
                    // 玩家既是对象也是观察者
                    Aoi.AddObject(obj);
                    Aoi.AddWatcher(obj, 4);
                    break;
                case MapNpc:
                    _npcs[obj.ObjectId] = (MapNpc)obj;
                    Aoi.AddObject(obj);
                    break;
                default:
                    Aoi.AddObject(obj);
                    break;
            }
        }

        Log.Debug($"对象进入地图 ObjectId:{obj.ObjectId} MapId:{CfgId} Line:{Line} Pos:({obj.X},{obj.Y})");
        return true;
    }

    /// <summary>从地图移除对象</summary>
    public bool RemoveObject(long objectId)
    {
        lock (_lock)
        {
            if (!_objects.Remove(objectId, out var obj))
                return false;

            switch (obj)
            {
                case MapPlayer:
                    _players.Remove(objectId);
                    Aoi.RemoveWatcher(obj);
                    Aoi.RemoveObject(obj);
                    break;
                default:
                    Aoi.RemoveObject(obj);
                    break;
            }

            obj.MapId = 0;
            obj.Line = 0;
        }

        Log.Debug($"对象离开地图 ObjectId:{objectId} MapId:{CfgId} Line:{Line}");
        return true;
    }

    /// <summary>获取地图上的对象</summary>
    public IMapObject? GetObject(long objectId)
    {
        lock (_lock)
        {
            return _objects.TryGetValue(objectId, out var obj) ? obj : null;
        }
    }

    /// <summary>获取地图上的玩家</summary>
    public MapPlayer? GetPlayer(long roleId)
    {
        lock (_lock)
        {
            return _players.TryGetValue(roleId, out var p) ? p : null;
        }
    }

    /// <summary>获取地图上所有玩家（快照）</summary>
    public List<MapPlayer> GetAllPlayers()
    {
        lock (_lock)
        {
            return new List<MapPlayer>(_players.Values);
        }
    }

    /// <summary>获取地图上所有 NPC</summary>
    public List<MapNpc> GetAllNpcs()
    {
        lock (_lock)
        {
            return new List<MapNpc>(_npcs.Values);
        }
    }

    /// <summary>获取视野范围内的对象</summary>
    public List<IMapObject> GetObjectsInRange(int x, int y, int range)
    {
        return Aoi.GetObjectsByRange(x, y, range)
            .Cast<IMapObject>()
            .ToList();
    }

    #endregion

    #region 移动

    /// <summary>更新对象位置（移动）</summary>
    public bool MoveObject(long objectId, int newX, int newY)
    {
        lock (_lock)
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

            if (obj is MapPlayer)
            {
                Aoi.UpdateWatcher(obj, oldX, oldY, newX, newY);
            }
            Aoi.UpdateObject(obj, oldX, oldY, newX, newY);
        }

        return true;
    }

    #endregion

    #region NPC 管理

    /// <summary>添加 NPC 到地图</summary>
    public bool AddNpc(int npcId, int cfgId, string name, int x, int y)
    {
        var npc = new MapNpc(npcId, cfgId, name, CfgId, Line, x, y);
        return AddObject(npc);
    }

    /// <summary>移除 NPC</summary>
    public bool RemoveNpc(int npcId)
    {
        return RemoveObject(npcId);
    }

    #endregion
}
