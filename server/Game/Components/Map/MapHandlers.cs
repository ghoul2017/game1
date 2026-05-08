using Game.Core.Aoi;
using Game.Components.Map;
using Game.Components.Player;
using System.Linq;
using System.Threading.Tasks;
using Game.Core.Network;
using Shared.Core.Event;
using Shared.Core.Log;
using Shared.Core.Network;
using Shared.Common.Protos;

namespace Game.Components.Map;

public static class MapHandlers
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(MapHandlers));

    [MessageHandler(typeof(RpcMethodHandler))]
    public static Task<IMessage> EnterMapHandler(TcpSession session, EnterMapReq req, EnterMapAck res)
    {
        if (session.RoleId == 0)
        {
            res.Code = MapErrorCode.NotLoggedIn;
            return Task.FromResult<IMessage>(res);
        }

        var player = Context.Players.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = MapErrorCode.NotLoggedIn;
            return Task.FromResult<IMessage>(res);
        }

        // 检查是否已在地图中（重进：返回当前位置）
        var currentPos = Context.Map.GetPlayerPosition(session.RoleId);
        if (currentPos != null)
        {
            var (mapId, line, px, py) = currentPos.Value;
            res.Code = MapErrorCode.Ok;
            res.MapCfgId = mapId;
            res.Line = line;
            res.X = px;
            res.Y = py;
            session.Send(res);
            // 重新发送视野内对象
            var currentMap = Context.Map.GetMap(mapId, line);
            if (currentMap != null)
                SendVisibleObjects(session.RoleId, currentMap, px, py);
            Log.Info($"玩家重进地图 RoleId:{session.RoleId} MapId:{mapId}");
            return Task.FromResult<IMessage>(null!);
        }

        // 获取目标地图
        var map = Context.Map.GetMap(req.MapCfgId, req.Line);
        if (map == null)
        {
            res.Code = MapErrorCode.MapNotFound;
            return Task.FromResult<IMessage>(res);
        }

        int x = map.BirthX;
        int y = map.BirthY;

        if (!Context.Map.EnterMap(session.RoleId, player.RoleData.Role?.Name ?? "", req.MapCfgId, req.Line, x, y))
        {
            res.Code = MapErrorCode.MapNotFound;
            return Task.FromResult<IMessage>(res);
        }

        res.Code = MapErrorCode.Ok;
        res.MapCfgId = req.MapCfgId;
        res.Line = req.Line;
        res.X = x;
        res.Y = y;

        session.Send(res);
        SendVisibleObjects(session.RoleId, map, x, y);

        EventBus.Instance.Publish(new EventType.PlayerEnterMapEvent
        {
            RoleId = session.RoleId,
            MapCfgId = req.MapCfgId,
            Line = req.Line,
        });

        Log.Info($"玩家进入地图 RoleId:{session.RoleId} MapId:{req.MapCfgId}");
        return Task.FromResult<IMessage>(null!);
    }

    [MessageHandler(typeof(RpcMethodHandler))]
    public static Task<IMessage> LeaveMapHandler(TcpSession session, LeaveMapReq req, LeaveMapAck res)
    {
        if (session.RoleId == 0)
        {
            res.Code = MapErrorCode.NotLoggedIn;
            return Task.FromResult<IMessage>(res);
        }

        if (!Context.Map.QuitMap(session.RoleId))
        {
            res.Code = MapErrorCode.NotInMap;
            return Task.FromResult<IMessage>(res);
        }

        res.Code = MapErrorCode.Ok;
        return Task.FromResult<IMessage>(res);
    }

    [MessageHandler(typeof(RpcMethodHandler))]
    public static Task<IMessage> ChangeMapHandler(TcpSession session, ChangeMapReq req, ChangeMapAck res)
    {
        if (session.RoleId == 0)
        {
            res.Code = MapErrorCode.NotLoggedIn;
            return Task.FromResult<IMessage>(res);
        }

        var player = Context.Players.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = MapErrorCode.NotLoggedIn;
            return Task.FromResult<IMessage>(res);
        }

        var targetMap = Context.Map.GetMap(req.TargetMapCfgId, req.TargetLine);
        if (targetMap == null)
        {
            res.Code = MapErrorCode.MapNotFound;
            return Task.FromResult<IMessage>(res);
        }

        int x = req.X > 0 ? req.X : targetMap.BirthX;
        int y = req.Y > 0 ? req.Y : targetMap.BirthY;

        if (!Context.Map.ChangeMap(session.RoleId, player.RoleData.Role?.Name ?? "", req.TargetMapCfgId, req.TargetLine, x, y))
        {
            res.Code = MapErrorCode.MapNotFound;
            return Task.FromResult<IMessage>(res);
        }

        res.Code = MapErrorCode.Ok;
        res.MapCfgId = req.TargetMapCfgId;
        res.Line = req.TargetLine;
        res.X = x;
        res.Y = y;

        session.Send(res);

        var objects = targetMap.GetObjectsInRange(x, y, 4);
        var ntf = new MapObjectsNtf
        {
            Objects = objects
                .Where(o => o.ObjectId != session.RoleId)
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
            session.Send(ntf);
        }

        Log.Info($"玩家切图 RoleId:{session.RoleId} → MapId:{req.TargetMapCfgId}");
        return Task.FromResult<IMessage>(null!);
    }

    [MessageHandler(typeof(RpcMethodHandler))]
    public static Task<IMessage> MapMoveHandler(TcpSession session, MapMoveReq req, MapMoveAck res)
    {
        if (session.RoleId == 0)
        {
            res.Code = MapErrorCode.NotLoggedIn;
            return Task.FromResult<IMessage>(res);
        }

        if (!Context.Map.Move(session.RoleId, req.X, req.Y))
        {
            res.Code = MapErrorCode.NotInMap;
            return Task.FromResult<IMessage>(res);
        }

        res.Code = MapErrorCode.Ok;
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
                    NpcId = GetMapObjectNpcId(o)
                }).ToList()
        };

        if (ntf.Objects.Count > 0)
        {
            Context.Players.SendToPlayer(roleId, ntf);
        }
    }

    private static int GetMapObjectNpcId(IAoiObject obj)
    {
        if (obj is MapNpc npc)
            return npc.CfgId;
        return 0;
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
