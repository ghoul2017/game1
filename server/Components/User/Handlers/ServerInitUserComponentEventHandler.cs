using Shared.Core.Event;

namespace Game.Components.User;

public sealed class ServerInitUserComponentEventHandler : ISyncEventHandler<EventType.ServerInitEvent>
{
    public void Handle(EventType.ServerInitEvent e)
    {
        Context.Server.AddComponent<UserComponent>();
    }
}
