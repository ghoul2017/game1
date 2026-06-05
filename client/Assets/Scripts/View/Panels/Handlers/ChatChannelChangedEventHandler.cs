using Shared.Core.Event;

namespace Client.View
{
    public sealed class ChatChannelChangedEventHandler : ISyncEventHandler<EventType.ChatChannelChangedEvent>
    {
        public void Handle(EventType.ChatChannelChangedEvent e)
        {
            var panel = UIManager.Instance != null ? UIManager.Instance.GetPanel<MainPanel>() : null;
            if (panel == null || !panel.IsShown) return;

            panel.RefreshChannelDisplay();
        }
    }
}
