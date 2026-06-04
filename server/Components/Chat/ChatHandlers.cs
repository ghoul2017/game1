using Shared.Core.Event;

namespace Game.Components.Chat;

public static class ChatHandlers
{
    [EventSubscriber]
    public static void OnPlayerOffline(EventType.PlayerOfflineEvent e)
    {
        Context.Chat.CleanCooldown(e.RoleId);
    }
}
