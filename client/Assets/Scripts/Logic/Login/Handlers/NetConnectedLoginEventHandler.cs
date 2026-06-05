using Shared.Core.Event;

namespace Client.Logic
{
    public sealed class NetConnectedLoginEventHandler : ISyncEventHandler<EventType.NetConnectedEvent>
    {
        public void Handle(EventType.NetConnectedEvent e)
        {
            LoginController.Instance.ContinuePendingLoginAfterConnection();
        }
    }
}
