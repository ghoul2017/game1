using Shared.Core.Event;
using Shared.Core.Log;

namespace Game.Components.Map;

public sealed class PlayerOfflineQuitMapEventHandler : ISyncEventHandler<EventType.PlayerOfflineEvent>
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(PlayerOfflineQuitMapEventHandler));

    public void Handle(EventType.PlayerOfflineEvent e)
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
