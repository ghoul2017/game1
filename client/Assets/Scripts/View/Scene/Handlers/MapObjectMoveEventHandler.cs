using Shared.Common.Const;
using Shared.Core.Event;
using UnityEngine;
using EventType = Shared.Core.Event.EventType;

namespace Client.View
{
    public sealed class MapObjectMoveEventHandler : ISyncEventHandler<EventType.MapObjectMoveEvent>
    {
        public void Handle(EventType.MapObjectMoveEvent e)
        {
            var ntf = e.Notification;
            if (GameSceneEntry.Instance == null || ntf == null) return;

            if (ntf.ObjectType == MapObjectType.Player)
            {
                OtherPlayerManager.Instance.MovePlayer(ntf.ObjectId, ntf.X, ntf.Y);
            }
            else if (ntf.ObjectType == MapObjectType.Monster || ntf.ObjectType == MapObjectType.Npc)
            {
                MonsterEntityManager.Instance.Move(ntf.ObjectId, ntf.X, ntf.Y);
            }
            else
            {
                Debug.LogWarning(string.Format("[GameSceneEntry] 未知地图对象类型，忽略移动 ObjectId:{0} ObjectType:{1}",
                    ntf.ObjectId, ntf.ObjectType));
            }
        }
    }
}
