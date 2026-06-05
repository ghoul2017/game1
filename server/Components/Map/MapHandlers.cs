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
