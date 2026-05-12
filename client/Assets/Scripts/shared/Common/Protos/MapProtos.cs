using ProtoBuf;
using Shared.Core.Network;
using System.Collections.Generic;

namespace Shared.Common.Protos
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
        public ushort MsgId => MsgIdConst.EnterMapReq;
        [ProtoMember(1)] public int MapCfgId { get; set; }
        [ProtoMember(2)] public int Line { get; set; }
    }

    [ProtoContract]
    public class EnterMapAck : IMessage
    {
        public ushort MsgId => MsgIdConst.EnterMapAck;
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
        public ushort MsgId => MsgIdConst.LeaveMapReq;
    }

    [ProtoContract]
    public class LeaveMapAck : IMessage
    {
        public ushort MsgId => MsgIdConst.LeaveMapAck;
        [ProtoMember(1)] public int Code { get; set; }
    }

    #endregion

    #region 切换地图

    [ProtoContract]
    public class ChangeMapReq : IMessage
    {
        public ushort MsgId => MsgIdConst.ChangeMapReq;
        [ProtoMember(1)] public int TargetMapCfgId { get; set; }
        [ProtoMember(2)] public int TargetLine { get; set; }
        [ProtoMember(3)] public int X { get; set; }
        [ProtoMember(4)] public int Y { get; set; }
    }

    [ProtoContract]
    public class ChangeMapAck : IMessage
    {
        public ushort MsgId => MsgIdConst.ChangeMapAck;
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
        public ushort MsgId => MsgIdConst.MapMoveReq;
        [ProtoMember(1)] public int X { get; set; }
        [ProtoMember(2)] public int Y { get; set; }
    }

    [ProtoContract]
    public class MapMoveAck : IMessage
    {
        public ushort MsgId => MsgIdConst.MapMoveAck;
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
        [ProtoMember(6)] public int CfgId { get; set; }  // 怪物/NPC配置ID
    }

    #endregion

    #region 地图通知消息

    [ProtoContract]
    public class MapObjectsNtf : IMessage
    {
        public ushort MsgId => MsgIdConst.MapObjectsNtf;
        [ProtoMember(1)] public List<MapObjectInfo> Objects { get; set; } = new List<MapObjectInfo>();
    }

    [ProtoContract]
    public class MapPlayerEnterNtf : IMessage
    {
        public ushort MsgId => MsgIdConst.MapPlayerEnterNtf;
        [ProtoMember(1)] public MapObjectInfo Player { get; set; } = new MapObjectInfo();
    }

    [ProtoContract]
    public class MapPlayerLeaveNtf : IMessage
    {
        public ushort MsgId => MsgIdConst.MapPlayerLeaveNtf;
        [ProtoMember(1)] public long RoleId { get; set; }
    }

    [ProtoContract]
    public class MapPlayerMoveNtf : IMessage
    {
        public ushort MsgId => MsgIdConst.MapPlayerMoveNtf;
        [ProtoMember(1)] public long RoleId { get; set; }
        [ProtoMember(2)] public int X { get; set; }
        [ProtoMember(3)] public int Y { get; set; }
    }

    #endregion

    #region NPC通知消息

    [ProtoContract]
    public class MapObjectEnterNtf : IMessage
    {
        public ushort MsgId => MsgIdConst.MapObjectEnterNtf;
        [ProtoMember(1)] public long ObjectId { get; set; }
        [ProtoMember(2)] public int CfgId { get; set; }
        [ProtoMember(3)] public string Name { get; set; } = "";
        [ProtoMember(4)] public int X { get; set; }
        [ProtoMember(5)] public int Y { get; set; }
        [ProtoMember(6)] public int ObjectType { get; set; }
    }

    [ProtoContract]
    public class MapObjectLeaveNtf : IMessage
    {
        public ushort MsgId => MsgIdConst.MapObjectLeaveNtf;
        [ProtoMember(1)] public long ObjectId { get; set; }
    }

    [ProtoContract]
    public class MapObjectMoveNtf : IMessage
    {
        public ushort MsgId => MsgIdConst.MapObjectMoveNtf;
        [ProtoMember(1)] public long ObjectId { get; set; }
        [ProtoMember(2)] public int X { get; set; }
        [ProtoMember(3)] public int Y { get; set; }
    }

    #endregion

    #region 怪物掉落通知

    [ProtoContract]
    public class MonsterDropNtf : IMessage
    {
        public ushort MsgId => MsgIdConst.MonsterDropNtf;
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
