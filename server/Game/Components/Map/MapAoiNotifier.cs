using Game.Components.Player;
using System.Collections.Generic;
using Game.Core.Aoi;
using Shared.Core.Log;
using Shared.Common.Protos;

namespace Game.Components.Map;

/// <summary>
/// AOI 事件通知器。将 AOI 事件转换为网络消息发送给客户端。
/// 处理：OnObjectEnter → MapPlayerEnterNtf，OnObjectLeave → MapPlayerLeaveNtf，
/// OnObjectMove → MapPlayerMoveNtf。
/// 不处理 OnWatcherUpdate（初始视野由 Handler 中的 SendVisibleObjects 负责）。
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
        // 不处理。初始视野由 EnterMapHandler/ChangeMapHandler 的 SendVisibleObjects 负责。
        // 跨塔移动的视野更新暂不处理（GetTowerChange 对 watcher range 的计算有已知局限）。
    }

    private static MapObjectInfo ToMapObjectInfo(IMapObject obj)
    {
        return new MapObjectInfo
        {
            ObjectId = obj.ObjectId,
            ObjectType = obj.ObjectType,
            Name = obj.Name ?? "",
            X = obj.X,
            Y = obj.Y
        };
    }
}
