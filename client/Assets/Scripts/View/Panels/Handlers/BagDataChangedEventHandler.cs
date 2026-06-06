using Shared.Core.Event;

namespace Client.View
{
    public sealed class BagDataChangedEventHandler : ISyncEventHandler<EventType.BagDataChangedEvent>
    {
        public void Handle(EventType.BagDataChangedEvent e)
        {
            var panel = UIManager.Instance != null ? UIManager.Instance.GetPanel<BagPanel>() : null;
            if (panel == null || !panel.IsShown)
                return;

            panel.RefreshGrid();
        }
    }
}
