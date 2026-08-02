using System.Collections.Generic;
using Shared.Common.Protos;

namespace Shared.Core.Event
{
    public static class EventType
    {
        public struct ClientToastRequestedEvent : IEvent
        {
            public string Message;
        }

        public struct LoginCompletedEvent : IEvent
        {
            public bool Success;
            public string ErrorMessage;
        }

        public struct RoleSelectDataChangedEvent : IEvent
        {
        }

        public struct RoleEnteredEvent : IEvent
        {
            public long RoleId;
        }

        public struct BagDataChangedEvent : IEvent
        {
        }

        public struct MailDataChangedEvent : IEvent
        {
        }

        public struct TaskDataChangedEvent : IEvent
        {
        }

        public struct ChatMessageAddedEvent : IEvent
        {
        }

        public struct ChatChannelChangedEvent : IEvent
        {
        }

        public struct FightSkillCastedEvent : IEvent
        {
            public UseSkillAck Ack;
        }

        public struct FightHpChangedEvent : IEvent
        {
            public HpChangedNtf Notification;
        }

        public struct FightPkModeChangedEvent : IEvent
        {
        }

        public struct FightKilledByEvent : IEvent
        {
            public KilledByNtf Notification;
        }

        public struct NetConnectedEvent : IEvent
        {
        }

        public struct NetDisconnectedEvent : IEvent
        {
        }

        public struct NetErrorEvent : IEvent
        {
            public string Reason;
        }

        public struct NetReconnectedEvent : IEvent
        {
        }

        public struct NetReconnectFailedEvent : IEvent
        {
            public string Message;
        }

        public struct NetKickedEvent : IEvent
        {
            public int Reason;
        }

        public struct MapEnterMapReadyEvent : IEvent
        {
        }

        public struct MapObjectsChangedEvent : IEvent
        {
            public List<MapObjectInfo> Objects;
            public List<long> RemoveObjectIds;
        }

        public struct MapObjectMoveEvent : IEvent
        {
            public MapObjectMoveNtf Notification;
        }

        public struct MapMoveCorrectedEvent : IEvent
        {
            public int X;
            public int Y;
        }

        public struct MapRebuildEvent : IEvent
        {
            public int MapCfgId;
            public int SpawnX;
            public int SpawnY;
        }

        public struct ChangeMapFailedEvent : IEvent
        {
            public string ErrorMessage;
        }

        public struct MapKickedEvent : IEvent
        {
            public int Reason;
        }

        public struct MapReconnectedEvent : IEvent
        {
        }

        public struct MapReconnectFailedEvent : IEvent
        {
            public string Message;
        }
    }
}
