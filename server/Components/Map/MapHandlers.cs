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

    internal static void SendVisibleObjects(long roleId, GameMap map, int x, int y)
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
}
