using Game.Components.Monster;
using Game.Components.Player;
using Game.Entities;
using System.Linq;
using System.Threading.Tasks;
using Shared.Core.Event;
using Shared.Core.Log;
using Shared.Core.Network;
using Shared.Common.Protos;

namespace Game.Components.Map;

public static class MapHandlers
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(MapHandlers));

    [MessageHandler(typeof(PlayerRpcMethodHandler))]
    public static Task<IMessage> EnterMapHandler(PlayerEntity player, EnterMapReq req, EnterMapAck res)
    {
        // 检查是否已在地图中（重进：返回当前位置）
        var currentPos = Context.Map.GetPlayerPosition(player.Id);
        if (currentPos != null)
        {
            var (mapId, line, px, py) = currentPos.Value;
            res.MapCfgId = mapId;
            res.Line = line;
            res.X = px;
            res.Y = py;
            player.Online.Session?.Send(res);
            // 重新发送视野内对象
            var currentMap = Context.Map.GetMap(mapId, line);
            if (currentMap != null)
                SendVisibleObjects(player.Id, currentMap, px, py);
            Log.Info($"玩家重进地图 RoleId:{player.Id} MapId:{mapId}");
            return Task.FromResult<IMessage>(null!);
        }

        // 获取目标地图
        var map = Context.Map.GetMap(req.MapCfgId, req.Line);
        if (map == null)
        {
            res.ErrorMessage = "目标地图不存在";
            return Task.FromResult<IMessage>(res);
        }

        int x = map.BirthX;
        int y = map.BirthY;

        if (!Context.Map.EnterMap(player.Id, player.RoleData.Name, req.MapCfgId, req.Line, x, y, player))
        {
            res.ErrorMessage = "目标地图不存在";
            return Task.FromResult<IMessage>(res);
        }

        res.MapCfgId = req.MapCfgId;
        res.Line = req.Line;
        res.X = x;
        res.Y = y;

        player.Online.Session?.Send(res);
        SendVisibleObjects(player.Id, map, x, y);

        EventBus.Instance.Publish(new EventType.PlayerEnterMapEvent
        {
            RoleId = player.Id,
            MapCfgId = req.MapCfgId,
            Line = req.Line,
        });

        Log.Info($"玩家进入地图 RoleId:{player.Id} MapId:{req.MapCfgId}");
        return Task.FromResult<IMessage>(null!);
    }

    [MessageHandler(typeof(PlayerRpcMethodHandler))]
    public static Task<IMessage> LeaveMapHandler(PlayerEntity player, LeaveMapReq req, LeaveMapAck res)
    {
        if (!Context.Map.QuitMap(player.Id))
        {
            res.ErrorMessage = "不在地图中";
            return Task.FromResult<IMessage>(res);
        }

        return Task.FromResult<IMessage>(res);
    }

    [MessageHandler(typeof(PlayerRpcMethodHandler))]
    public static Task<IMessage> ChangeMapHandler(PlayerEntity player, ChangeMapReq req, ChangeMapAck res)
    {
        var targetMap = Context.Map.GetMap(req.TargetMapCfgId, req.TargetLine);
        if (targetMap == null)
        {
            res.ErrorMessage = "目标地图不存在";
            return Task.FromResult<IMessage>(res);
        }

        int x = req.X > 0 ? req.X : targetMap.BirthX;
        int y = req.Y > 0 ? req.Y : targetMap.BirthY;

        if (!Context.Map.ChangeMap(player.Id, player.RoleData.Name, req.TargetMapCfgId, req.TargetLine, x, y, player))
        {
            res.ErrorMessage = "目标地图不存在";
            return Task.FromResult<IMessage>(res);
        }

        res.MapCfgId = req.TargetMapCfgId;
        res.Line = req.TargetLine;
        res.X = x;
        res.Y = y;

        player.Online.Session?.Send(res);

        var objects = targetMap.GetObjectsInRange(x, y, 4);
        var ntf = new MapObjectsNtf
        {
            Objects = objects
                .Where(o => o.ObjectId != player.Id)
                .Select(o => new MapObjectInfo
                {
                    ObjectId = o.ObjectId,
                    ObjectType = o.ObjectType,
                    Name = o.Name,
                    X = o.X,
                    Y = o.Y
                }).ToList()
        };

        if (ntf.Objects.Count > 0)
        {
            player.Online.Session?.Send(ntf);
        }

        Log.Info($"玩家切图 RoleId:{player.Id} → MapId:{req.TargetMapCfgId}");
        return Task.FromResult<IMessage>(null!);
    }

    [MessageHandler(typeof(PlayerRpcMethodHandler))]
    public static Task<IMessage> MapMoveHandler(PlayerEntity player, MapMoveReq req, MapMoveAck res)
    {
        if (!Context.Map.Move(player.Id, req.X, req.Y))
        {
            res.ErrorMessage = "不在地图中";
            return Task.FromResult<IMessage>(res);
        }

        res.X = req.X;
        res.Y = req.Y;

        return Task.FromResult<IMessage>(res);
    }

    private static void SendVisibleObjects(long roleId, GameMap map, int x, int y)
    {
        var objects = map.GetObjectsInRange(x, y, 4);
        var ntf = new MapObjectsNtf
        {
            Objects = objects
                .Where(o => o.ObjectId != roleId)
                .Select(o => new MapObjectInfo
                {
                    ObjectId = o.ObjectId,
                    ObjectType = o.ObjectType,
                    Name = o.Name,
                    X = o.X,
                    Y = o.Y,
                    CfgId = o.CfgId
                }).ToList()
        };

        if (ntf.Objects.Count > 0)
        {
            Context.Players.SendToPlayer(roleId, ntf);
        }
    }

    #region 事件订阅

    [EventSubscriber]
    public static void OnPlayerOffline(EventType.PlayerOfflineEvent e)
    {
        var map = Context.Map.GetPlayerMap(e.RoleId);
        if (map == null)
        {
            return;
        }

        Context.Map.QuitMap(e.RoleId);
        Log.Info($"玩家下线退出地图 RoleId:{e.RoleId} MapId:{map.CfgId}");
    }

    #endregion
}
