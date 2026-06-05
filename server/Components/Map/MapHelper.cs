using System.Collections.Generic;
using Game.Components.Player;
using Shared.Common.Protos;

namespace Game.Components.Map;

public static class MapHelper
{
    internal static void SendEnterMap(long roleId, int mapCfgId, int line, int x, int y)
    {
        Context.Players.SendToPlayer(roleId, new EnterMapNtf
        {
            MapCfgId = mapCfgId,
            Line = line,
            X = x,
            Y = y
        });
    }

    internal static void SendVisibleObjectList(long roleId, GameMap map, int x, int y)
    {
        var objects = map.GetObjectsInRange(x, y, 4);
        SendObjectChanges(roleId, objects.Where(o => o.ObjectId != roleId), null);
    }

    internal static void SendVisibleObject(long roleId, IMapObject obj)
    {
        SendObjectChanges(roleId, new[] { obj }, null);
    }

    internal static void SendObjectRemove(long roleId, long objectId)
    {
        SendObjectChanges(roleId, null, new[] { objectId });
    }

    private static void SendObjectChanges(long roleId, IEnumerable<IMapObject>? objects, IEnumerable<long>? removeObjectIds)
    {
        var ntf = new MapObjectsNtf
        {
            Objects = objects?.Select(ToMapObjectInfo).ToList() ?? new List<MapObjectInfo>(),
            RemoveObjectIds = removeObjectIds?.ToList() ?? new List<long>()
        };

        if (ntf.Objects.Count == 0 && ntf.RemoveObjectIds.Count == 0)
        {
            return;
        }

        Context.Players.SendToPlayer(roleId, ntf);
    }

    internal static MapObjectInfo ToMapObjectInfo(IMapObject obj)
    {
        return new MapObjectInfo
        {
            ObjectId = obj.ObjectId,
            ObjectType = obj.ObjectType,
            Name = obj.Name ?? "",
            X = obj.X,
            Y = obj.Y,
            CfgId = obj.CfgId
        };
    }
}
