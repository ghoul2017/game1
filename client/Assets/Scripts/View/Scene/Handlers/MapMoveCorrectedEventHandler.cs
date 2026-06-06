using Shared.Core.Event;
using UnityEngine;
using EventType = Shared.Core.Event.EventType;

namespace Client.View
{
    public sealed class MapMoveCorrectedEventHandler : ISyncEventHandler<EventType.MapMoveCorrectedEvent>
    {
        public void Handle(EventType.MapMoveCorrectedEvent e)
        {
            if (GameSceneEntry.Instance == null)
                return;

            if (GameSceneEntry.PlayerController != null)
            {
                GameSceneEntry.PlayerController.CorrectPosition(e.X, e.Y);
                Debug.Log(string.Format("[GameSceneEntry] 移动被服务端纠正: ({0},{1})", e.X, e.Y));
            }
        }
    }
}
