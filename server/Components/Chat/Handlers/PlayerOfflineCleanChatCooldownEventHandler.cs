using Shared.Core.Event;

namespace Game.Components.Chat;

public sealed class PlayerOfflineCleanChatCooldownEventHandler : ISyncEventHandler<EventType.PlayerOfflineEvent>
{
    public void Handle(EventType.PlayerOfflineEvent e)
    {
        Context.Chat.CleanCooldown(e.RoleId);
    }
}
