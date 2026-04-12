using System.Collections.Generic;
using ProtoBuf;

namespace Client.Shared.Messages
{
    // ─── 地图错误码 ───

    public static class MapErrorCode
    {
        public const int Ok = 0;
        public const int NotLoggedIn = 1;
        public const int MapNotFound = 2;
        public const int AlreadyInMap = 3;
        public const int NotInMap = 4;
        public const int InvalidPosition = 5;
    }

    // ─── 进入地图 ───

    [ProtoContract]
    public class EnterMapReq : IMessage
    {
        public ushort MsgId => MsgId.EnterMapReq;
        [ProtoMember(1)] public int MapCfgId { get; set; }
        [ProtoMember(2)] public int Line { get; set; }
    }

    [ProtoContract]
    public class EnterMapAck : IMessage
    {
        public ushort MsgId => MsgId.EnterMapAck;
        [ProtoMember(1)] public int Code { get; set; }
        [ProtoMember(2)] public int MapCfgId { get; set; }
        [ProtoMember(3)] public int Line { get; set; }
        [ProtoMember(4)] public int X { get; set; }
        [ProtoMember(5)] public int Y { get; set; }
    }

    // ─── 离开地图 ───

    [ProtoContract]
    public class LeaveMapReq : IMessage
    {
        public ushort MsgId => MsgId.LeaveMapReq;
    }

    [ProtoContract]
    public class LeaveMapAck : IMessage
    {
        public ushort MsgId => MsgId.LeaveMapAck;
        [ProtoMember(1)] public int Code { get; set; }
    }

    // ─── 切换地图 ───

    [ProtoContract]
    public class ChangeMapReq : IMessage
    {
        public ushort MsgId => MsgId.ChangeMapReq;
        [ProtoMember(1)] public int TargetMapCfgId { get; set; }
        [ProtoMember(2)] public int TargetLine { get; set; }
        [ProtoMember(3)] public int X { get; set; }
        [ProtoMember(4)] public int Y { get; set; }
    }

    [ProtoContract]
    public class ChangeMapAck : IMessage
    {
        public ushort MsgId => MsgId.ChangeMapAck;
        [ProtoMember(1)] public int Code { get; set; }
        [ProtoMember(2)] public int MapCfgId { get; set; }
        [ProtoMember(3)] public int Line { get; set; }
        [ProtoMember(4)] public int X { get; set; }
        [ProtoMember(5)] public int Y { get; set; }
    }

    // ─── 地图移动 ───

    [ProtoContract]
    public class MapMoveReq : IMessage
    {
        public ushort MsgId => MsgId.MapMoveReq;
        [ProtoMember(1)] public int X { get; set; }
        [ProtoMember(2)] public int Y { get; set; }
    }

    [ProtoContract]
    public class MapMoveAck : IMessage
    {
        public ushort MsgId => MsgId.MapMoveAck;
        [ProtoMember(1)] public int Code { get; set; }
        [ProtoMember(2)] public int X { get; set; }
        [ProtoMember(3)] public int Y { get; set; }
    }

    // ─── 地图对象信息 ───

    [ProtoContract]
    public class MapObjectInfo
    {
        [ProtoMember(1)] public long ObjectId { get; set; }
        [ProtoMember(2)] public int ObjectType { get; set; }
        [ProtoMember(3)] public string Name { get; set; } = "";
        [ProtoMember(4)] public int X { get; set; }
        [ProtoMember(5)] public int Y { get; set; }
    }

    // ─── 地图通知消息 ───

    [ProtoContract]
    public class MapObjectsNtf : IMessage
    {
        public ushort MsgId => MsgId.MapObjectsNtf;
        [ProtoMember(1)] public List<MapObjectInfo> Objects { get; set; } = new List<MapObjectInfo>();
    }

    [ProtoContract]
    public class MapPlayerEnterNtf : IMessage
    {
        public ushort MsgId => MsgId.MapPlayerEnterNtf;
        [ProtoMember(1)] public MapObjectInfo Player { get; set; } = new MapObjectInfo();
    }

    [ProtoContract]
    public class MapPlayerLeaveNtf : IMessage
    {
        public ushort MsgId => MsgId.MapPlayerLeaveNtf;
        [ProtoMember(1)] public long RoleId { get; set; }
    }

    [ProtoContract]
    public class MapPlayerMoveNtf : IMessage
    {
        public ushort MsgId => MsgId.MapPlayerMoveNtf;
        [ProtoMember(1)] public long RoleId { get; set; }
        [ProtoMember(2)] public int X { get; set; }
        [ProtoMember(3)] public int Y { get; set; }
    }
}
