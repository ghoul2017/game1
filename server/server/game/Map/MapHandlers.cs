using Server.Core.Log;
using Server.Game.Player;
using Server.Network;
using server.common.protos;

namespace Server.Game.Map;

/// <summary>
/// 进入地图 Handler
/// </summary>
public class EnterMapHandler : MessageHandler<EnterMapReq>
{
    private static readonly ILog Log = LogManager.GetLogger<EnterMapHandler>();

    protected override Task<IMessage?> ExecuteAsync(TcpSession session, EnterMapReq req)
    {
        var resp = new EnterMapAck();

        if (session.RoleId == 0)
        {
            resp.Code = MapErrorCode.NotLoggedIn;
            return Task.FromResult<IMessage?>(resp);
        }

        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            resp.Code = MapErrorCode.NotLoggedIn;
            return Task.FromResult<IMessage?>(resp);
        }

        // 检查是否已在地图中
        var currentPos = MapManager.Instance.GetPlayerPosition(session.RoleId);
        if (currentPos != null)
        {
            resp.Code = MapErrorCode.AlreadyInMap;
            return Task.FromResult<IMessage?>(resp);
        }

        // 获取目标地图，不存在则用默认坐标
        var map = MapManager.Instance.GetMap(req.MapCfgId, req.Line);
        if (map == null)
        {
            resp.Code = MapErrorCode.MapNotFound;
            return Task.FromResult<IMessage?>(resp);
        }

        int x = req.Line == 0 ? map.BirthX : map.BirthX;
        int y = req.Line == 0 ? map.BirthY : map.BirthY;

        if (!MapManager.Instance.EnterMap(session.RoleId, player.RoleName, req.MapCfgId, req.Line, x, y))
        {
            resp.Code = MapErrorCode.MapNotFound;
            return Task.FromResult<IMessage?>(resp);
        }

        resp.Code = MapErrorCode.Ok;
        resp.MapCfgId = req.MapCfgId;
        resp.Line = req.Line;
        resp.X = x;
        resp.Y = y;

        // 发送视野内对象列表
        SendVisibleObjects(session.RoleId, map, x, y);

        Log.Info($"玩家进入地图 RoleId:{session.RoleId} MapId:{req.MapCfgId}");
        return Task.FromResult<IMessage?>(resp);
    }

    private void SendVisibleObjects(long roleId, GameMap map, int x, int y)
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
                    Y = o.Y
                }).ToList()
        };

        if (ntf.Objects.Count > 0)
        {
            PlayerManager.Instance.SendToPlayer(roleId, ntf);
        }
    }
}

/// <summary>
/// 离开地图 Handler
/// </summary>
public class LeaveMapHandler : MessageHandler<LeaveMapReq>
{
    protected override Task<IMessage?> ExecuteAsync(TcpSession session, LeaveMapReq req)
    {
        var resp = new LeaveMapAck();

        if (session.RoleId == 0)
        {
            resp.Code = MapErrorCode.NotLoggedIn;
            return Task.FromResult<IMessage?>(resp);
        }

        if (!MapManager.Instance.QuitMap(session.RoleId))
        {
            resp.Code = MapErrorCode.NotInMap;
            return Task.FromResult<IMessage?>(resp);
        }

        resp.Code = MapErrorCode.Ok;
        return Task.FromResult<IMessage?>(resp);
    }
}

/// <summary>
/// 切换地图 Handler
/// </summary>
public class ChangeMapHandler : MessageHandler<ChangeMapReq>
{
    private static readonly ILog Log = LogManager.GetLogger<ChangeMapHandler>();

    protected override Task<IMessage?> ExecuteAsync(TcpSession session, ChangeMapReq req)
    {
        var resp = new ChangeMapAck();

        if (session.RoleId == 0)
        {
            resp.Code = MapErrorCode.NotLoggedIn;
            return Task.FromResult<IMessage?>(resp);
        }

        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            resp.Code = MapErrorCode.NotLoggedIn;
            return Task.FromResult<IMessage?>(resp);
        }

        var targetMap = MapManager.Instance.GetMap(req.TargetMapCfgId, req.TargetLine);
        if (targetMap == null)
        {
            resp.Code = MapErrorCode.MapNotFound;
            return Task.FromResult<IMessage?>(resp);
        }

        int x = req.X > 0 ? req.X : targetMap.BirthX;
        int y = req.Y > 0 ? req.Y : targetMap.BirthY;

        if (!MapManager.Instance.ChangeMap(session.RoleId, player.RoleName, req.TargetMapCfgId, req.TargetLine, x, y))
        {
            resp.Code = MapErrorCode.MapNotFound;
            return Task.FromResult<IMessage?>(resp);
        }

        resp.Code = MapErrorCode.Ok;
        resp.MapCfgId = req.TargetMapCfgId;
        resp.Line = req.TargetLine;
        resp.X = x;
        resp.Y = y;

        // 发送视野内对象列表
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
        return Task.FromResult<IMessage?>(resp);
    }
}

/// <summary>
/// 地图移动 Handler
/// </summary>
public class MapMoveHandler : MessageHandler<MapMoveReq>
{
    protected override Task<IMessage?> ExecuteAsync(TcpSession session, MapMoveReq req)
    {
        var resp = new MapMoveAck();

        if (session.RoleId == 0)
        {
            resp.Code = MapErrorCode.NotLoggedIn;
            return Task.FromResult<IMessage?>(resp);
        }

        if (!MapManager.Instance.Move(session.RoleId, req.X, req.Y))
        {
            resp.Code = MapErrorCode.NotInMap;
            return Task.FromResult<IMessage?>(resp);
        }

        resp.Code = MapErrorCode.Ok;
        resp.X = req.X;
        resp.Y = req.Y;

        // 广播移动给同地图其他玩家
        var mapObj = MapManager.Instance.GetPlayerMapObj(session.RoleId);
        if (mapObj != null)
        {
            var moveNtf = new MapPlayerMoveNtf
            {
                RoleId = session.RoleId,
                X = req.X,
                Y = req.Y
            };

            var map = MapManager.Instance.GetPlayerMap(session.RoleId);
            if (map != null)
            {
                foreach (var otherPlayer in map.GetAllPlayers())
                {
                    if (otherPlayer.RoleId != session.RoleId)
                    {
                        PlayerManager.Instance.SendToPlayer(otherPlayer.RoleId, moveNtf);
                    }
                }
            }
        }

        return Task.FromResult<IMessage?>(resp);
    }
}