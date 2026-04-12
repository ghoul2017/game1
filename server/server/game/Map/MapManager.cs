using Server.Core.Log;
using Server.Game.Player;

namespace Server.Game.Map;

/// <summary>
/// 地图管理器。管理所有地图实例，处理玩家切图、位置同步等。
/// 参考: Java 项目的 MapManager。
/// </summary>
public class MapManager
{
    private static readonly ILog Log = LogManager.GetLogger<MapManager>();

    public static MapManager Instance { get; } = new();

    /// <summary>所有地图实例 key = MapKey (cfgId*1000+line)</summary>
    private readonly Dictionary<long, GameMap> _maps = new();

    /// <summary>玩家所在的地图 key = roleId</summary>
    private readonly Dictionary<long, GameMap> _playerMapMap = new();

    /// <summary>玩家的地图对象 key = roleId</summary>
    private readonly Dictionary<long, MapPlayer> _playerMapObjMap = new();

    /// <summary>默认地图配置ID</summary>
    private const int DefaultMapCfgId = 1;

    private readonly object _lock = new();

    private MapManager() { }

    #region 地图管理

    /// <summary>创建地图实例</summary>
    public GameMap CreateMap(int cfgId, int line, string name, int width, int height, int birthX, int birthY)
    {
        var map = new GameMap(cfgId, line, name, width, height, birthX, birthY);
        lock (_lock)
        {
            _maps[map.MapKey] = map;
        }
        Log.Info($"创建地图 CfgId:{cfgId} Line:{line} Name:{name} Size:{width}x{height}");
        return map;
    }

    /// <summary>获取地图实例</summary>
    public GameMap? GetMap(int cfgId, int line)
    {
        long key = (long)cfgId * 1000 + line;
        lock (_lock)
        {
            return _maps.TryGetValue(key, out var map) ? map : null;
        }
    }

    /// <summary>获取玩家所在的地图</summary>
    public GameMap? GetPlayerMap(long roleId)
    {
        lock (_lock)
        {
            return _playerMapMap.TryGetValue(roleId, out var map) ? map : null;
        }
    }

    /// <summary>获取玩家的地图对象</summary>
    public MapPlayer? GetPlayerMapObj(long roleId)
    {
        lock (_lock)
        {
            return _playerMapObjMap.TryGetValue(roleId, out var obj) ? obj : null;
        }
    }

    /// <summary>获取所有地图（快照）</summary>
    public List<GameMap> GetAllMaps()
    {
        lock (_lock)
        {
            return new List<GameMap>(_maps.Values);
        }
    }

    #endregion

    #region 玩家进入/离开/切图

    /// <summary>玩家进入地图</summary>
    public bool EnterMap(long roleId, string roleName, int cfgId, int line, int x, int y)
    {
        var map = GetMap(cfgId, line);
        if (map == null)
        {
            Log.Warn($"地图不存在 CfgId:{cfgId} Line:{line}");
            return false;
        }

        lock (_lock)
        {
            // 如果玩家已在某张地图，先退出
            if (_playerMapMap.ContainsKey(roleId))
            {
                QuitMapLocked(roleId);
            }

            var mapPlayer = new MapPlayer(roleId, roleName, cfgId, line, x, y);
            if (!map.AddObject(mapPlayer))
            {
                Log.Warn($"玩家进入地图失败 RoleId:{roleId}");
                return false;
            }

            _playerMapMap[roleId] = map;
            _playerMapObjMap[roleId] = mapPlayer;
        }

        Log.Info($"玩家进入地图 RoleId:{roleId} MapId:{cfgId} Line:{line} Pos:({x},{y})");
        return true;
    }

    /// <summary>玩家离开地图</summary>
    public bool QuitMap(long roleId)
    {
        lock (_lock)
        {
            return QuitMapLocked(roleId);
        }
    }

    private bool QuitMapLocked(long roleId)
    {
        if (!_playerMapMap.Remove(roleId, out var map))
            return false;

        _playerMapObjMap.Remove(roleId);
        map.RemoveObject(roleId);

        Log.Info($"玩家离开地图 RoleId:{roleId} MapId:{map.CfgId} Line:{map.Line}");
        return true;
    }

    /// <summary>玩家切图（从当前地图移动到新地图）</summary>
    public bool ChangeMap(long roleId, string roleName, int newCfgId, int newLine, int newX, int newY)
    {
        lock (_lock)
        {
            QuitMapLocked(roleId);
            return EnterMapUnlocked(roleId, roleName, newCfgId, newLine, newX, newY);
        }
    }

    private bool EnterMapUnlocked(long roleId, string roleName, int cfgId, int line, int x, int y)
    {
        var map = GetMap(cfgId, line);
        if (map == null) return false;

        var mapPlayer = new MapPlayer(roleId, roleName, cfgId, line, x, y);
        if (!map.AddObject(mapPlayer)) return false;

        _playerMapMap[roleId] = map;
        _playerMapObjMap[roleId] = mapPlayer;
        return true;
    }

    /// <summary>玩家移动（同地图内）</summary>
    public bool Move(long roleId, int newX, int newY)
    {
        lock (_lock)
        {
            if (!_playerMapMap.TryGetValue(roleId, out var map))
                return false;

            return map.MoveObject(roleId, newX, newY);
        }
    }

    /// <summary>获取玩家位置信息</summary>
    public (int mapId, int line, int x, int y)? GetPlayerPosition(long roleId)
    {
        lock (_lock)
        {
            if (!_playerMapObjMap.TryGetValue(roleId, out var obj))
                return null;
            return (obj.MapId, obj.Line, obj.X, obj.Y);
        }
    }

    /// <summary>获取同地图的玩家列表（排除自己）</summary>
    public List<MapPlayer> GetNearbyPlayers(long roleId)
    {
        lock (_lock)
        {
            if (!_playerMapObjMap.TryGetValue(roleId, out var obj))
                return new List<MapPlayer>();

            if (!_playerMapMap.TryGetValue(roleId, out var map))
                return new List<MapPlayer>();

            return map.GetAllPlayers().Where(p => p.RoleId != roleId).ToList();
        }
    }

    #endregion

    #region 初始化

    /// <summary>初始化默认地图（服务器启动时调用）</summary>
    public void InitDefaultMaps()
    {
        // 创建默认的新手村地图
        CreateMap(1, 1, "新手村", 200, 200, 100, 100);
        CreateMap(2, 1, "主城", 300, 300, 150, 150);
        CreateMap(3, 1, "野外", 500, 500, 250, 250);

        Log.Info("默认地图初始化完成");
    }

    /// <summary>移除所有玩家（服务器关闭时调用）</summary>
    public void RemoveAll()
    {
        lock (_lock)
        {
            _playerMapMap.Clear();
            _playerMapObjMap.Clear();
            _maps.Clear();
        }
        Log.Info("地图管理器已清空");
    }

    #endregion
}
