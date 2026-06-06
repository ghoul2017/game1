using Shared.Core.Event;

namespace Client.View
{
    public sealed class FightPkModeChangedEventHandler : ISyncEventHandler<EventType.FightPkModeChangedEvent>
    {
        public void Handle(EventType.FightPkModeChangedEvent e)
        {
            var panel = UIManager.Instance != null ? UIManager.Instance.GetPanel<MainPanel>() : null;
            if (panel == null || !panel.IsShown)
                return;

            panel.UpdatePkModeDisplay();
        }
    }
}
