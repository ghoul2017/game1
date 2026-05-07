using System.Collections.Generic;
using System.Linq;
using Game.Entities;
using Game.Components.Map;
using Game.Components.Player;
using Shared.Core.Log;

namespace Game.Components.Map;

/// <summary>
/// 地图系统。提供地图管理、玩家进入/离开、位置同步等功能。
/// 所有操作通过 MapComponent 扩展方法实现，无状态设计。
/// </summary>
public static class MapSystem
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(MapSystem));

    /// <summary>默认地图配置ID</summary>
    private const int DefaultMapCfgId = 1;

    #region 地图管理

    /// <summary>创建地图实例</summary>
    public static GameMap CreateMap(this MapComponent self, int cfgId, int line, string name, int width, int height, int birthX, int birthY)
    {
        var map = new GameMap(cfgId, line, name, width, height, birthX, birthY);
        long key = (long)cfgId * 1000 + line;
        self.Maps[key] = map;
        Log.Info($"创建地图 CfgId:{cfgId} Line:{line} Name:{name} Size:{width}x{height}");
        return map;
    }

    /// <summary>获取地图实例</summary>
    public static GameMap? GetMap(this MapComponent self, int cfgId, int line)
    {
        long key = (long)cfgId * 1000 + line;
        return self.Maps.TryGetValue(key, out var map) ? map : null;
    }

    /// <summary>获取玩家所在的地图</summary>
    public static GameMap? GetPlayerMap(this MapComponent self, long roleId)
    {
        return self.PlayerMapMap.TryGetValue(roleId, out var map) ? map : null;
    }

    /// <summary>获取玩家的地图对象</summary>
    public static MapPlayer? GetPlayerMapObj(this MapComponent self, long roleId)
    {
        return self.PlayerMapObjMap.TryGetValue(roleId, out var obj) ? obj : null;
    }

    /// <summary>获取所有地图</summary>
    public static List<GameMap> GetAllMaps(this MapComponent self) => new(self.Maps.Values);

    #endregion

    #region 玩家进入/离开/切图

    /// <summary>玩家进入地图</summary>
    public static bool EnterMap(this MapComponent self, long roleId, string roleName, int cfgId, int line, int x, int y)
    {
        var map = self.GetMap(cfgId, line);
        if (map == null)
        {
            Log.Warn($"地图不存在 CfgId:{cfgId} Line:{line}");
            return false;
        }

        // 如果玩家已在某张地图，先退出
        if (self.PlayerMapMap.ContainsKey(roleId))
        {
            QuitMapInternal(self, roleId);
        }

        var mapPlayer = new MapPlayer(roleId, roleName, cfgId, line, x, y);
        if (!map.AddObject(mapPlayer))
        {
            Log.Warn($"玩家进入地图失败 RoleId:{roleId}");
            return false;
        }

        self.PlayerMapMap[roleId] = map;
        self.PlayerMapObjMap[roleId] = mapPlayer;

        Log.Info($"玩家进入地图 RoleId:{roleId} MapId:{cfgId} Line:{line} Pos:({x},{y})");
        return true;
    }

    /// <summary>玩家离开地图</summary>
    public static bool QuitMap(this MapComponent self, long roleId)
    {
        return QuitMapInternal(self, roleId);
    }

    private static bool QuitMapInternal(MapComponent self, long roleId)
    {
        if (!self.PlayerMapMap.Remove(roleId, out var map))
            return false;

        self.PlayerMapObjMap.Remove(roleId);
        map.RemoveObject(roleId);

        Log.Info($"玩家离开地图 RoleId:{roleId} MapId:{map.CfgId} Line:{map.Line}");
        return true;
    }

    /// <summary>玩家切图（从当前地图移动到新地图）</summary>
    public static bool ChangeMap(this MapComponent self, long roleId, string roleName, int newCfgId, int newLine, int newX, int newY)
    {
        QuitMapInternal(self, roleId);
        return EnterMapInternal(self, roleId, roleName, newCfgId, newLine, newX, newY);
    }

    private static bool EnterMapInternal(MapComponent self, long roleId, string roleName, int cfgId, int line, int x, int y)
    {
        var map = self.GetMap(cfgId, line);
        if (map == null) return false;

        var mapPlayer = new MapPlayer(roleId, roleName, cfgId, line, x, y);
        if (!map.AddObject(mapPlayer)) return false;

        self.PlayerMapMap[roleId] = map;
        self.PlayerMapObjMap[roleId] = mapPlayer;
        return true;
    }

    /// <summary>玩家移动（同地图内）</summary>
    public static bool Move(this MapComponent self, long roleId, int newX, int newY)
    {
        if (!self.PlayerMapMap.TryGetValue(roleId, out var map))
            return false;

        return map.MoveObject(roleId, newX, newY);
    }

    /// <summary>获取玩家位置信息</summary>
    public static (int mapId, int line, int x, int y)? GetPlayerPosition(this MapComponent self, long roleId)
    {
        if (!self.PlayerMapObjMap.TryGetValue(roleId, out var obj))
            return null;
        return (obj.MapId, obj.Line, obj.X, obj.Y);
    }

    /// <summary>获取同地图的玩家列表（排除自己）</summary>
    public static List<MapPlayer> GetNearbyPlayers(this MapComponent self, long roleId)
    {
        if (!self.PlayerMapObjMap.TryGetValue(roleId, out var obj))
            return new List<MapPlayer>();

        if (!self.PlayerMapMap.TryGetValue(roleId, out var map))
            return new List<MapPlayer>();

        return map.GetAllPlayers().Where(p => p.RoleId != roleId).ToList();
    }

    #endregion

    #region 初始化

    /// <summary>初始化默认地图（服务器启动时调用）</summary>
    public static void InitDefaultMaps(this MapComponent self)
    {
        // 创建默认的新手村地图
        self.CreateMap(1, 1, "新手村", 200, 200, 100, 100);
        self.CreateMap(2, 1, "主城", 300, 300, 150, 150);
        self.CreateMap(3, 1, "野外", 500, 500, 250, 250);

        Log.Info("默认地图初始化完成");
    }

    /// <summary>移除所有玩家（服务器关闭时调用）</summary>
    public static void RemoveAll(this MapComponent self)
    {
        self.PlayerMapMap.Clear();
        self.PlayerMapObjMap.Clear();
        self.Maps.Clear();
        Log.Info("地图管理器已清空");
    }

    #endregion
}
