using Shared.Core.Event;
using Shared.Core.Log;

namespace Game.Components.Player;

public static class PlayerHandlers
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(PlayerHandlers));

    [EventSubscriber]
    public static async void OnPlayerOnline(EventType.PlayerOnlineEvent e)
    {
        var player = Context.Players.GetByRoleId(e.RoleId);
        if (player == null)
        {
            return;
        }

        var attrComp = player.AddComponent<RoleAttributeComponent>();
        await attrComp.LoadAttributeAsync(e.RoleId);
    }
}
