using Shared.Core.Event;

namespace Client.View
{
    public sealed class RoleSelectDataChangedEventHandler : ISyncEventHandler<EventType.RoleSelectDataChangedEvent>
    {
        public void Handle(EventType.RoleSelectDataChangedEvent e)
        {
            var panel = UIManager.Instance != null ? UIManager.Instance.GetPanel<RoleSelectPanel>() : null;
            if (panel == null || !panel.IsShown)
                return;

            panel.RefreshSlots();
        }
    }
}
