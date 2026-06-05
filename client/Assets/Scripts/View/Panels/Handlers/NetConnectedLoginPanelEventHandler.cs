using Shared.Core.Event;

namespace Client.View
{
    public sealed class NetConnectedLoginPanelEventHandler : ISyncEventHandler<EventType.NetConnectedEvent>
    {
        public void Handle(EventType.NetConnectedEvent e)
        {
            var panel = GetPanel();
            if (panel == null || !panel.ConnectEventsActive) return;

            panel.CleanupConnectEvents();
        }

        private static LoginPanel GetPanel()
        {
            var panel = UIManager.Instance != null ? UIManager.Instance.GetPanel<LoginPanel>() : null;
            return panel != null && panel.IsShown ? panel : null;
        }
    }
}
