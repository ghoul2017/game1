using Client.Logic;
using Shared.Core.Event;

namespace Client.View
{
    public sealed class ClientToastRequestedEventHandler : ISyncEventHandler<EventType.ClientToastRequestedEvent>
    {
        public void Handle(EventType.ClientToastRequestedEvent e)
        {
            if (UIManager.Instance == null || string.IsNullOrEmpty(e.Message))
                return;

            UIToast.Show(e.Message);
        }
    }
}
