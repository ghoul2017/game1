using Game.Components.Player;
using System.Collections.Generic;
using Shared.Core.Aoi;
using Shared.Core.Log;
using Shared.Common.Protos;

namespace Game.Components.Map;

/// <summary>
/// AOI 事件通知器。将 AOI 事件转换为网络消息发送给客户端。
/// 按 ObjectType 分发：Player 走 MapPlayerXxxNtf，其他走 MapObjectXxxNtf。
/// </summary>
public class MapAoiNotifier : IAoiEventListener
{
    private static readonly ILog Log = LogManager.GetLogger<MapAoiNotifier>();

    public void OnObjectEnter(IAoiObject obj, IReadOnlyDictionary<long, IAoiObject> watchers)
    {
        if (obj is not IMapObject mapObj) return;

        foreach (var watcher in watchers.Values)
        {
            if (watcher.ObjectId == obj.ObjectId) continue;
            SendEnterNotification(watcher.ObjectId, mapObj);
        }
    }

    public void OnObjectLeave(IAoiObject obj, IReadOnlyDictionary<long, IAoiObject> watchers)
    {
        if (obj is not IMapObject mapObj) return;

        foreach (var watcher in watchers.Values)
        {
            if (watcher.ObjectId == obj.ObjectId) continue;
            SendLeaveNotification(watcher.ObjectId, mapObj);
        }
    }

    public void OnObjectMove(IAoiObject obj, IReadOnlyDictionary<long, IAoiObject> oldWatchers,
        IReadOnlyDictionary<long, IAoiObject> updateWatchers, IReadOnlyDictionary<long, IAoiObject> newWatchers)
    {
        if (obj is not IMapObject mapObj) return;

        foreach (var watcher in updateWatchers.Values)
        {
            if (watcher.ObjectId == obj.ObjectId) continue;
            SendMoveNotification(watcher.ObjectId, mapObj);
        }
    }

    public void OnWatcherUpdate(IAoiObject watcher, IReadOnlyList<IAoiObject> addedObjects, IReadOnlyList<IAoiObject> removedObjects)
    {
        foreach (var obj in addedObjects)
        {
            if (obj is not IMapObject mapObj) continue;
            if (mapObj.ObjectId == watcher.ObjectId) continue;
            SendEnterNotification(watcher.ObjectId, mapObj);
        }

        foreach (var obj in removedObjects)
        {
            if (obj.ObjectId == watcher.ObjectId) continue;
            if (obj is not IMapObject mapObj) continue;
            SendLeaveNotification(watcher.ObjectId, mapObj);
        }
    }

    #region 通知分发

    private static void SendEnterNotification(long watcherId, IMapObject mapObj)
    {
        if (mapObj.ObjectType == MapObjectType.Player)
        {
            Context.Players.SendToPlayer(watcherId, new MapPlayerEnterNtf
            {
                Player = ToMapObjectInfo(mapObj)
            });
        }
        else
        {
            Context.Players.SendToPlayer(watcherId, new MapObjectEnterNtf
            {
                ObjectId = mapObj.ObjectId,
                CfgId = mapObj.CfgId,
                Name = mapObj.Name ?? "",
                X = mapObj.X,
                Y = mapObj.Y,
                ObjectType = mapObj.ObjectType,
            });
        }
    }

    private static void SendLeaveNotification(long watcherId, IMapObject mapObj)
    {
        if (mapObj.ObjectType == MapObjectType.Player)
        {
            Context.Players.SendToPlayer(watcherId, new MapPlayerLeaveNtf
            {
                RoleId = mapObj.ObjectId
            });
        }
        else
        {
            Context.Players.SendToPlayer(watcherId, new MapObjectLeaveNtf
            {
                ObjectId = mapObj.ObjectId
            });
        }
    }

    private static void SendMoveNotification(long watcherId, IMapObject mapObj)
    {
        if (mapObj.ObjectType == MapObjectType.Player)
        {
            Context.Players.SendToPlayer(watcherId, new MapPlayerMoveNtf
            {
                RoleId = mapObj.ObjectId,
                X = mapObj.X,
                Y = mapObj.Y
            });
        }
        else
        {
            Context.Players.SendToPlayer(watcherId, new MapObjectMoveNtf
            {
                ObjectId = mapObj.ObjectId,
                X = mapObj.X,
                Y = mapObj.Y
            });
        }
    }

    #endregion

    private static MapObjectInfo ToMapObjectInfo(IMapObject obj)
    {
        return new MapObjectInfo
        {
            ObjectId = obj.ObjectId,
            ObjectType = obj.ObjectType,
            Name = obj.Name ?? "",
            X = obj.X,
            Y = obj.Y,
            CfgId = obj.CfgId,
        };
    }
}
