using Shared.Core.Event;

namespace Client.View
{
    public sealed class ChatMessageAddedEventHandler : ISyncEventHandler<EventType.ChatMessageAddedEvent>
    {
        public void Handle(EventType.ChatMessageAddedEvent e)
        {
            var panel = UIManager.Instance != null ? UIManager.Instance.GetPanel<MainPanel>() : null;
            if (panel == null || !panel.IsShown)
                return;

            panel.RefreshChatDisplay();
        }
    }
}
