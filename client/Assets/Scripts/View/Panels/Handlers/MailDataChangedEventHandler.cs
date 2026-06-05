using Shared.Core.Event;

namespace Client.View
{
    public sealed class MailDataChangedEventHandler : ISyncEventHandler<EventType.MailDataChangedEvent>
    {
        public void Handle(EventType.MailDataChangedEvent e)
        {
            var panel = UIManager.Instance != null ? UIManager.Instance.GetPanel<MailPanel>() : null;
            if (panel == null || !panel.IsShown) return;

            panel.RefreshList();
            panel.RefreshDetail();
        }
    }
}
