using Shared.Core.Event;
using UnityEngine;
using EventType = Shared.Core.Event.EventType;

namespace Client.View
{
    public sealed class MapReconnectedEventHandler : ISyncEventHandler<EventType.MapReconnectedEvent>
    {
        public void Handle(EventType.MapReconnectedEvent e)
        {
            if (GameSceneEntry.Instance == null) return;

            Debug.Log("[GameSceneEntry] 重连成功");
        }
    }
}
