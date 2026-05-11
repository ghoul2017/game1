using Game.Components.Monster;
using Game.Components.Player;
using System.Collections.Generic;
using Shared.Core.Aoi;
using Shared.Core.Log;
using Shared.Common.Protos;

namespace Game.Components.Map;

/// <summary>
/// AOI 事件通知器。将 AOI 事件转换为网络消息发送给客户端。
/// 处理：OnObjectEnter → MapPlayerEnterNtf，OnObjectLeave → MapPlayerLeaveNtf，
/// OnObjectMove → MapPlayerMoveNtf，OnWatcherUpdate → 视野变化自动推送。
/// </summary>
public class MapAoiNotifier : IAoiEventListener
{
    private static readonly ILog Log = LogManager.GetLogger<MapAoiNotifier>();

    public void OnObjectEnter(IAoiObject obj, IReadOnlyDictionary<long, IAoiObject> watchers)
    {
        if (obj is not IMapObject mapObj) return;

        var ntf = new MapPlayerEnterNtf
        {
            Player = ToMapObjectInfo(mapObj)
        };

        foreach (var watcher in watchers.Values)
        {
            if (watcher.ObjectId == obj.ObjectId) continue;
            Context.Players.SendToPlayer(watcher.ObjectId, ntf);
        }
    }

    public void OnObjectLeave(IAoiObject obj, IReadOnlyDictionary<long, IAoiObject> watchers)
    {
        if (obj is not IMapObject) return;

        var ntf = new MapPlayerLeaveNtf
        {
            RoleId = obj.ObjectId
        };

        foreach (var watcher in watchers.Values)
        {
            if (watcher.ObjectId == obj.ObjectId) continue;
            Context.Players.SendToPlayer(watcher.ObjectId, ntf);
        }
    }

    public void OnObjectMove(IAoiObject obj, IReadOnlyDictionary<long, IAoiObject> oldWatchers,
        IReadOnlyDictionary<long, IAoiObject> updateWatchers, IReadOnlyDictionary<long, IAoiObject> newWatchers)
    {
        if (obj is not IMapObject) return;

        var ntf = new MapPlayerMoveNtf
        {
            RoleId = obj.ObjectId,
            X = obj.X,
            Y = obj.Y
        };

        foreach (var watcher in updateWatchers.Values)
        {
            if (watcher.ObjectId == obj.ObjectId) continue;
            Context.Players.SendToPlayer(watcher.ObjectId, ntf);
        }
    }

    public void OnWatcherUpdate(IAoiObject watcher, IReadOnlyList<IAoiObject> addedObjects, IReadOnlyList<IAoiObject> removedObjects)
    {
        // 新视野中出现的对象
        foreach (var obj in addedObjects)
        {
            if (obj is not IMapObject mapObj) continue;
            if (mapObj.ObjectId == watcher.ObjectId) continue;

            if (mapObj.ObjectType == MapObjectType.Player)
            {
                Context.Players.SendToPlayer(watcher.ObjectId, new MapPlayerEnterNtf
                {
                    Player = ToMapObjectInfo(mapObj)
                });
            }
            else if (mapObj.ObjectType == MapObjectType.Monster && obj is MonsterEntity monster)
            {
                Context.Players.SendToPlayer(watcher.ObjectId, new MapNpcEnterNtf
                {
                    ObjectId = monster.ObjectId,
                    NpcId = monster.CfgId,
                    Name = monster.Name ?? "",
                    X = monster.X,
                    Y = monster.Y,
                });
            }
        }

        // 旧视野中消失的对象
        foreach (var obj in removedObjects)
        {
            if (obj.ObjectId == watcher.ObjectId) continue;
            Context.Players.SendToPlayer(watcher.ObjectId, new MapPlayerLeaveNtf
            {
                RoleId = obj.ObjectId
            });
        }
    }

    private static MapObjectInfo ToMapObjectInfo(IMapObject obj)
    {
        var info = new MapObjectInfo
        {
            ObjectId = obj.ObjectId,
            ObjectType = obj.ObjectType,
            Name = obj.Name ?? "",
            X = obj.X,
            Y = obj.Y
        };

        if (obj is MonsterEntity monster)
            info.NpcId = monster.CfgId;

        return info;
    }
}
