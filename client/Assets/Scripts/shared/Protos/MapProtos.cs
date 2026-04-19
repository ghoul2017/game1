using ProtoBuf;
using GameShared.Network;
using System.Collections.Generic;

namespace GameShared.Protos
{
    #region 地图错误码

    public static class MapErrorCode
    {
        public const int Ok = 0;
        public const int NotLoggedIn = 1;
        public const int MapNotFound = 2;
        public const int AlreadyInMap = 3;
        public const int NotInMap = 4;
        public const int InvalidPosition = 5;
    }

    #endregion

    #region 进入地图

    [ProtoContract]
    public class EnterMapReq : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.EnterMapReq;
        [ProtoMember(1)] public int MapCfgId { get; set; }
        [ProtoMember(2)] public int Line { get; set; }
    }

    [ProtoContract]
    public class EnterMapAck : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.EnterMapAck;
        [ProtoMember(1)] public int Code { get; set; }
        [ProtoMember(2)] public int MapCfgId { get; set; }
        [ProtoMember(3)] public int Line { get; set; }
        [ProtoMember(4)] public int X { get; set; }
        [ProtoMember(5)] public int Y { get; set; }
    }

    #endregion

    #region 离开地图

    [ProtoContract]
    public class LeaveMapReq : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.LeaveMapReq;
    }

    [ProtoContract]
    public class LeaveMapAck : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.LeaveMapAck;
        [ProtoMember(1)] public int Code { get; set; }
    }

    #endregion

    #region 切换地图

    [ProtoContract]
    public class ChangeMapReq : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.ChangeMapReq;
        [ProtoMember(1)] public int TargetMapCfgId { get; set; }
        [ProtoMember(2)] public int TargetLine { get; set; }
        [ProtoMember(3)] public int X { get; set; }
        [ProtoMember(4)] public int Y { get; set; }
    }

    [ProtoContract]
    public class ChangeMapAck : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.ChangeMapAck;
        [ProtoMember(1)] public int Code { get; set; }
        [ProtoMember(2)] public int MapCfgId { get; set; }
        [ProtoMember(3)] public int Line { get; set; }
        [ProtoMember(4)] public int X { get; set; }
        [ProtoMember(5)] public int Y { get; set; }
    }

    #endregion

    #region 地图移动

    [ProtoContract]
    public class MapMoveReq : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.MapMoveReq;
        [ProtoMember(1)] public int X { get; set; }
        [ProtoMember(2)] public int Y { get; set; }
    }

    [ProtoContract]
    public class MapMoveAck : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.MapMoveAck;
        [ProtoMember(1)] public int Code { get; set; }
        [ProtoMember(2)] public int X { get; set; }
        [ProtoMember(3)] public int Y { get; set; }
    }

    #endregion

    #region 地图对象信息

    [ProtoContract]
    public class MapObjectInfo
    {
        [ProtoMember(1)] public long ObjectId { get; set; }
        [ProtoMember(2)] public int ObjectType { get; set; }
        [ProtoMember(3)] public string Name { get; set; } = "";
        [ProtoMember(4)] public int X { get; set; }
        [ProtoMember(5)] public int Y { get; set; }
        [ProtoMember(6)] public int NpcId { get; set; }  // 怪物/NPC配置ID
    }

    #endregion

    #region 地图通知消息

    [ProtoContract]
    public class MapObjectsNtf : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.MapObjectsNtf;
        [ProtoMember(1)] public List<MapObjectInfo> Objects { get; set; } = new List<MapObjectInfo>();
    }

    [ProtoContract]
    public class MapPlayerEnterNtf : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.MapPlayerEnterNtf;
        [ProtoMember(1)] public MapObjectInfo Player { get; set; } = new MapObjectInfo();
    }

    [ProtoContract]
    public class MapPlayerLeaveNtf : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.MapPlayerLeaveNtf;
        [ProtoMember(1)] public long RoleId { get; set; }
    }

    [ProtoContract]
    public class MapPlayerMoveNtf : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.MapPlayerMoveNtf;
        [ProtoMember(1)] public long RoleId { get; set; }
        [ProtoMember(2)] public int X { get; set; }
        [ProtoMember(3)] public int Y { get; set; }
    }

    #endregion

    #region NPC通知消息

    [ProtoContract]
    public class MapNpcEnterNtf : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.MapNpcEnterNtf;
        [ProtoMember(1)] public long ObjectId { get; set; }
        [ProtoMember(2)] public int NpcId { get; set; }
        [ProtoMember(3)] public string Name { get; set; } = "";
        [ProtoMember(4)] public int X { get; set; }
        [ProtoMember(5)] public int Y { get; set; }
    }

    [ProtoContract]
    public class MapNpcLeaveNtf : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.MapNpcLeaveNtf;
        [ProtoMember(1)] public long ObjectId { get; set; }
    }

    [ProtoContract]
    public class MapNpcMoveNtf : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.MapNpcMoveNtf;
        [ProtoMember(1)] public long ObjectId { get; set; }
        [ProtoMember(2)] public int X { get; set; }
        [ProtoMember(3)] public int Y { get; set; }
    }

    #endregion

    #region 怪物掉落通知

    [ProtoContract]
    public class MonsterDropNtf : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.MonsterDropNtf;
        [ProtoMember(1)] public long MonsterId { get; set; }
        [ProtoMember(2)] public int Gold { get; set; }
        [ProtoMember(3)] public List<DropItemInfo> Items { get; set; } = new List<DropItemInfo>();
    }

    [ProtoContract]
    public class DropItemInfo
    {
        [ProtoMember(1)] public int ConfigId { get; set; }
        [ProtoMember(2)] public int Count { get; set; }
    }

    #endregion
}
