using Shared.Core.Event;

namespace Client.Logic
{
    public sealed class NetReconnectFailedMapEventHandler : ISyncEventHandler<EventType.NetReconnectFailedEvent>
    {
        public void Handle(EventType.NetReconnectFailedEvent e)
        {
            EventBus.Instance.Publish(new EventType.MapReconnectFailedEvent { Message = e.Message });
        }
    }
}
