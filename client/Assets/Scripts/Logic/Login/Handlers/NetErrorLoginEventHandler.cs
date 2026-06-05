using Shared.Core.Event;

namespace Client.Logic
{
    public sealed class NetErrorLoginEventHandler : ISyncEventHandler<EventType.NetErrorEvent>
    {
        public void Handle(EventType.NetErrorEvent e)
        {
            LoginController.Instance.FailPendingLoginConnection(e.Reason);
        }
    }
}
