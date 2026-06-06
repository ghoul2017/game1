using Client.Logic;
using Shared.Core.Event;

namespace Client.View
{
    public sealed class NetErrorLoginPanelEventHandler : ISyncEventHandler<EventType.NetErrorEvent>
    {
        public void Handle(EventType.NetErrorEvent e)
        {
            var panel = GetPanel();
            if (panel == null || !panel.ConnectEventsActive)
                return;

            panel.SetErrorText(LoginController.GetConnectionErrorText(e.Reason));
            panel.SetButtonEnabled(true);
            panel.CleanupConnectEvents();
        }

        private static LoginPanel GetPanel()
        {
            var panel = UIManager.Instance != null ? UIManager.Instance.GetPanel<LoginPanel>() : null;
            return panel != null && panel.IsShown ? panel : null;
        }
    }
}
