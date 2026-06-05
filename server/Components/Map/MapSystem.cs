using System.Collections.Generic;
using System.Linq;
using Game.Components.Fight;
using Game.Components.Monster;
using Game.Components.Player;
using Shared.Core.Config;
using Game.Entities;
using Shared.Common.Config;
using Shared.Core.Entities;
using Shared.Core.Log;
using Shared.Core.Network;

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
        if (!self.PlayerMapEntities.TryGetValue(roleId, out var mapPlayer))
            return null;

        return self.GetMap(mapPlayer.MapId, mapPlayer.Line);
    }

    /// <summary>获取玩家的地图投影实体</summary>
    public static MapPlayerEntity? GetPlayerMapEntity(this MapComponent self, long roleId)
    {
        return self.PlayerMapEntities.TryGetValue(roleId, out var entity) ? entity : null;
    }

    /// <summary>获取所有地图</summary>
    public static List<GameMap> GetAllMaps(this MapComponent self) => new(self.Maps.Values);

    /// <summary>玩家进入地图</summary>
    public static bool EnterMap(this MapComponent self, int cfgId, int line, int x, int y, PlayerEntity playerEntity)
    {
        var map = self.GetMap(cfgId, line);
        if (map == null)
        {
            Log.Warn($"地图不存在 CfgId:{cfgId} Line:{line}");
            return false;
        }

        // 如果玩家已在某张地图，先退出
        if (self.PlayerMapEntities.ContainsKey(playerEntity.Id))
        {
            QuitMapInternal(self, playerEntity.Id);
        }

        var mapPlayer = MapPlayerEntity.Create(cfgId, line, x, y, playerEntity);

        if (!map.AddObject(mapPlayer))
        {
            Log.Warn($"玩家进入地图失败 RoleId:{playerEntity.Id}");
            return false;
        }

        self.PlayerMapEntities[playerEntity.Id] = mapPlayer;

        Log.Info($"玩家进入地图 RoleId:{playerEntity.Id} MapId:{cfgId} Line:{line} Pos:({x},{y})");
        return true;
    }

    /// <summary>玩家进入地图并通知客户端完成地图切换。被动传送也应走这个入口。</summary>
    public static bool EnterMapAndNotify(this MapComponent self, int cfgId, int line, int x, int y, PlayerEntity playerEntity)
    {
        if (!self.EnterMap(cfgId, line, x, y, playerEntity))
            return false;

        self.SendEnterMapSnapshot(playerEntity.Id, cfgId, line, x, y);
        return true;
    }

    /// <summary>玩家切图并通知客户端完成地图切换。被动传送也应走这个入口。</summary>
    public static bool ChangeMapAndNotify(this MapComponent self, int newCfgId, int newLine, int newX, int newY, PlayerEntity playerEntity)
    {
        if (!self.ChangeMap(newCfgId, newLine, newX, newY, playerEntity))
            return false;

        self.SendEnterMapSnapshot(playerEntity.Id, newCfgId, newLine, newX, newY);
        return true;
    }

    /// <summary>玩家已在地图内时，重新发送地图切换通知和当前视野快照。</summary>
    public static bool NotifyCurrentMap(this MapComponent self, long roleId)
    {
        var currentPos = self.GetPlayerPosition(roleId);
        if (currentPos == null)
            return false;

        var (mapId, line, x, y) = currentPos.Value;
        self.SendEnterMapSnapshot(roleId, mapId, line, x, y);
        return true;
    }

    /// <summary>玩家离开地图</summary>
    public static bool QuitMap(this MapComponent self, long roleId)
    {
        return QuitMapInternal(self, roleId);
    }

    private static bool QuitMapInternal(MapComponent self, long roleId)
    {
        if (!self.PlayerMapEntities.Remove(roleId, out var mapPlayer))
            return false;

        var map = self.GetMap(mapPlayer.MapId, mapPlayer.Line);
        map?.RemoveObject(roleId);

        Log.Info($"玩家离开地图 RoleId:{roleId}");
        return true;
    }

    /// <summary>玩家切图（从当前地图移动到新地图）</summary>
    public static bool ChangeMap(this MapComponent self, int newCfgId, int newLine, int newX, int newY, PlayerEntity playerEntity)
    {
        var map = self.GetMap(newCfgId, newLine);
        if (map == null)
        {
            Log.Warn($"地图不存在 CfgId:{newCfgId} Line:{newLine}");
            return false;
        }

        QuitMapInternal(self, playerEntity.Id);
        return EnterMapInternal(self, newCfgId, newLine, newX, newY, playerEntity);
    }

    private static bool EnterMapInternal(MapComponent self, int cfgId, int line, int x, int y, PlayerEntity playerEntity)
    {
        var map = self.GetMap(cfgId, line);
        if (map == null) return false;

        var mapPlayer = MapPlayerEntity.Create(cfgId, line, x, y, playerEntity);

        if (!map.AddObject(mapPlayer)) return false;

        self.PlayerMapEntities[playerEntity.Id] = mapPlayer;
        return true;
    }

    private static void SendEnterMapSnapshot(this MapComponent self, long roleId, int mapCfgId, int line, int x, int y)
    {
        MapHelper.SendEnterMap(roleId, mapCfgId, line, x, y);

        var map = self.GetMap(mapCfgId, line);
        if (map != null)
            MapHelper.SendVisibleObjectList(roleId, map, x, y);
    }

    /// <summary>玩家移动（同地图内）</summary>
    public static bool Move(this MapComponent self, long roleId, int newX, int newY)
    {
        if (!self.PlayerMapEntities.TryGetValue(roleId, out var mapPlayer))
            return false;

        var map = self.GetMap(mapPlayer.MapId, mapPlayer.Line);
        if (map == null) return false;

        return map.MoveObject(roleId, newX, newY);
    }

    /// <summary>获取玩家位置信息</summary>
    public static (int mapId, int line, int x, int y)? GetPlayerPosition(this MapComponent self, long roleId)
    {
        if (!self.PlayerMapEntities.TryGetValue(roleId, out var mapPlayer))
            return null;
        return (mapPlayer.MapId, mapPlayer.Line, mapPlayer.X, mapPlayer.Y);
    }

    /// <summary>获取同地图的玩家列表（排除自己）</summary>
    public static List<MapPlayerEntity> GetNearbyPlayers(this MapComponent self, long roleId)
    {
        if (!self.PlayerMapEntities.TryGetValue(roleId, out var mapPlayer))
            return new List<MapPlayerEntity>();

        var map = self.GetMap(mapPlayer.MapId, mapPlayer.Line);
        if (map == null)
            return new List<MapPlayerEntity>();

        return map.GetAllPlayers().Where(p => p.RoleId != roleId).ToList();
    }

    /// <summary>向指定对象的AOI视野内玩家广播消息</summary>
    public static void BroadcastToAoiWatchers(this MapComponent self, long objectId, IMessage msg)
    {
        var map = self.GetPlayerMap(objectId);
        if (map == null) return;

        var mapObj = map.GetObject(objectId);
        if (mapObj == null) return;

        var watchers = map.Aoi.GetObjectsByRange(mapObj.X, mapObj.Y, 4);
        foreach (var watcher in watchers)
        {
            if (watcher.ObjectId != objectId)
            {
                Context.Players.SendToPlayer(watcher.ObjectId, msg);
            }
        }
    }

    /// <summary>初始化默认地图（服务器启动时调用）</summary>
    public static void InitDefaultMaps(this MapComponent self)
    {
        // 创建默认地图
        self.CreateMap(1, 1, "新手村", 200, 200, 100, 100);
        self.CreateMap(2, 1, "主城", 300, 300, 150, 150);
        self.CreateMap(3, 1, "野外", 500, 500, 250, 250);

        // 初始化每张地图的怪物和 AI 定时器
        foreach (var map in self.Maps.Values)
        {
            map.InitMonsters();
            map.StartAiTimer();
        }

        Log.Info("默认地图初始化完成");
    }

    /// <summary>移除所有玩家（服务器关闭时调用）</summary>
    public static void RemoveAll(this MapComponent self)
    {
        foreach (var map in self.Maps.Values)
        {
            map.StopAiTimer();
        }
        self.PlayerMapEntities.Clear();
        self.Maps.Clear();
        Log.Info("地图管理器已清空");
    }
}
