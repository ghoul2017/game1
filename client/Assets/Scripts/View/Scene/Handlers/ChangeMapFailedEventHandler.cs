using Shared.Core.Event;
using UnityEngine;
using EventType = Shared.Core.Event.EventType;

namespace Client.View
{
    public sealed class ChangeMapFailedEventHandler : ISyncEventHandler<EventType.ChangeMapFailedEvent>
    {
        public void Handle(EventType.ChangeMapFailedEvent e)
        {
            if (GameSceneEntry.Instance == null)
                return;

            UIToast.Show("传送失败: " + e.ErrorMessage);
            Debug.Log("[GameSceneEntry] 切图失败: " + e.ErrorMessage);
        }
    }
}
