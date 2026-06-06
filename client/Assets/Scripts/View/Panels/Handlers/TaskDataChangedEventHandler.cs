using Shared.Core.Event;

namespace Client.View
{
    public sealed class TaskDataChangedEventHandler : ISyncEventHandler<EventType.TaskDataChangedEvent>
    {
        public void Handle(EventType.TaskDataChangedEvent e)
        {
            var panel = UIManager.Instance != null ? UIManager.Instance.GetPanel<TaskPanel>() : null;
            if (panel == null || !panel.IsShown)
                return;

            panel.RefreshList();
            panel.RefreshDetail();
        }
    }
}
