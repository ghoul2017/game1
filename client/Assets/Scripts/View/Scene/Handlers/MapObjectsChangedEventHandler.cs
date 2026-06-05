using Shared.Common.Const;
using Shared.Core.Event;

namespace Client.View
{
    public sealed class MapObjectsChangedEventHandler : ISyncEventHandler<EventType.MapObjectsChangedEvent>
    {
        public void Handle(EventType.MapObjectsChangedEvent e)
        {
            if (GameSceneEntry.Instance == null) return;

            if (e.RemoveObjectIds != null)
            {
                foreach (var objectId in e.RemoveObjectIds)
                {
                    OtherPlayerManager.Instance.RemovePlayer(objectId);
                    MonsterEntityManager.Instance.Remove(objectId);
                }
            }

            if (e.Objects == null) return;

            foreach (var obj in e.Objects)
            {
                if (obj.ObjectType == MapObjectType.Player)
                {
                    OtherPlayerManager.Instance.SpawnPlayer(obj);
                }
                else if (obj.ObjectType == MapObjectType.Monster || obj.ObjectType == MapObjectType.Npc)
                {
                    MonsterEntityManager.Instance.Spawn(obj.ObjectId, obj.CfgId, obj.ObjectType, obj.Name, obj.X, obj.Y);
                }
            }
        }
    }
}
