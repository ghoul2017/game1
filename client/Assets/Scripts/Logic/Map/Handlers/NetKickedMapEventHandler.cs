using Shared.Core.Event;

namespace Client.Logic
{
    public sealed class NetKickedMapEventHandler : ISyncEventHandler<EventType.NetKickedEvent>
    {
        public void Handle(EventType.NetKickedEvent e)
        {
            EventBus.Instance.Publish(new EventType.MapKickedEvent { Reason = e.Reason });
        }
    }
}
