using System.Linq;
using System.Threading.Tasks;
using Game.Game.Monster;
using Game.Game.Player;
using Game.Network;
using Shared.Core.Log;
using Shared.Core.Network;
using Shared.Common.Protos;

namespace Game.Game.Map;

/// <summary>
/// 进入地图 Handler
/// </summary>
public class EnterMapHandler : MessageHandler<EnterMapReq>
{
    private static readonly ILog Log = LogManager.GetLogger<EnterMapHandler>();

    protected override Task<IMessage?> ExecuteAsync(TcpSession session, EnterMapReq req)
    {
        var res = new EnterMapAck();

        if (session.RoleId == 0)
        {
            res.Code = MapErrorCode.NotLoggedIn;
            return Task.FromResult<IMessage?>(res);
        }

        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = MapErrorCode.NotLoggedIn;
            return Task.FromResult<IMessage?>(res);
        }

        // 检查是否已在地图中（重进：返回当前位置）
        var currentPos = MapManager.Instance.GetPlayerPosition(session.RoleId);
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
            var currentMap = MapManager.Instance.GetMap(mapId, line);
            if (currentMap != null)
                SendVisibleObjects(session.RoleId, currentMap, px, py);
            Log.Info($"玩家重进地图 RoleId:{session.RoleId} MapId:{mapId}");
            return Task.FromResult<IMessage?>(null);
        }

        // 获取目标地图，不存在则用默认坐标
        var map = MapManager.Instance.GetMap(req.MapCfgId, req.Line);
        if (map == null)
        {
            res.Code = MapErrorCode.MapNotFound;
            return Task.FromResult<IMessage?>(res);
        }

        int x = req.Line == 0 ? map.BirthX : map.BirthX;
        int y = req.Line == 0 ? map.BirthY : map.BirthY;

        if (!MapManager.Instance.EnterMap(session.RoleId, player.RoleName, req.MapCfgId, req.Line, x, y))
        {
            res.Code = MapErrorCode.MapNotFound;
            return Task.FromResult<IMessage?>(res);
        }

        res.Code = MapErrorCode.Ok;
        res.MapCfgId = req.MapCfgId;
        res.Line = req.Line;
        res.X = x;
        res.Y = y;

        // 先发送响应，让客户端设置场景，再发送视野内对象列表
        session.Send(res);
        SendVisibleObjects(session.RoleId, map, x, y);

        // 初始化地图怪物
        MonsterManager.Instance.InitMapMonsters(req.MapCfgId, req.Line);

        Log.Info($"玩家进入地图 RoleId:{session.RoleId} MapId:{req.MapCfgId}");
        return Task.FromResult<IMessage?>(null);
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
                    Y = o.Y,
                    // 对于怪物/NPC，需要获取其配置ID
                    NpcId = GetMapObjectNpcId(o)
                }).ToList()
        };

        if (ntf.Objects.Count > 0)
        {
            PlayerManager.Instance.SendToPlayer(roleId, ntf);
        }
    }

    /// <summary>获取地图对象的NPC配置ID</summary>
    private int GetMapObjectNpcId(Aoi.IAoiObject obj)
    {
        // MapNpc.CfgId 包含怪物/NPC的配置ID
        if (obj is MapNpc npc)
            return npc.CfgId;
        return 0;
    }
}

/// <summary>
/// 离开地图 Handler
/// </summary>
public class LeaveMapHandler : MessageHandler<LeaveMapReq>
{
    protected override Task<IMessage?> ExecuteAsync(TcpSession session, LeaveMapReq req)
    {
        var res = new LeaveMapAck();

        if (session.RoleId == 0)
        {
            res.Code = MapErrorCode.NotLoggedIn;
            return Task.FromResult<IMessage?>(res);
        }

        if (!MapManager.Instance.QuitMap(session.RoleId))
        {
            res.Code = MapErrorCode.NotInMap;
            return Task.FromResult<IMessage?>(res);
        }

        res.Code = MapErrorCode.Ok;
        return Task.FromResult<IMessage?>(res);
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
        var res = new ChangeMapAck();

        if (session.RoleId == 0)
        {
            res.Code = MapErrorCode.NotLoggedIn;
            return Task.FromResult<IMessage?>(res);
        }

        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = MapErrorCode.NotLoggedIn;
            return Task.FromResult<IMessage?>(res);
        }

        var targetMap = MapManager.Instance.GetMap(req.TargetMapCfgId, req.TargetLine);
        if (targetMap == null)
        {
            res.Code = MapErrorCode.MapNotFound;
            return Task.FromResult<IMessage?>(res);
        }

        int x = req.X > 0 ? req.X : targetMap.BirthX;
        int y = req.Y > 0 ? req.Y : targetMap.BirthY;

        if (!MapManager.Instance.ChangeMap(session.RoleId, player.RoleName, req.TargetMapCfgId, req.TargetLine, x, y))
        {
            res.Code = MapErrorCode.MapNotFound;
            return Task.FromResult<IMessage?>(res);
        }

        res.Code = MapErrorCode.Ok;
        res.MapCfgId = req.TargetMapCfgId;
        res.Line = req.TargetLine;
        res.X = x;
        res.Y = y;

        // 先发送响应，再发送视野内对象列表
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
        return Task.FromResult<IMessage?>(null);
    }
}

/// <summary>
/// 地图移动 Handler
/// AOI 事件通过 MapAoiNotifier 自动通知视野内玩家，此处不再手动广播。
/// </summary>
public class MapMoveHandler : MessageHandler<MapMoveReq>
{
    protected override Task<IMessage?> ExecuteAsync(TcpSession session, MapMoveReq req)
    {
        var res = new MapMoveAck();

        if (session.RoleId == 0)
        {
            res.Code = MapErrorCode.NotLoggedIn;
            return Task.FromResult<IMessage?>(res);
        }

        if (!MapManager.Instance.Move(session.RoleId, req.X, req.Y))
        {
            res.Code = MapErrorCode.NotInMap;
            return Task.FromResult<IMessage?>(res);
        }

        res.Code = MapErrorCode.Ok;
        res.X = req.X;
        res.Y = req.Y;

        return Task.FromResult<IMessage?>(res);
    }
}
