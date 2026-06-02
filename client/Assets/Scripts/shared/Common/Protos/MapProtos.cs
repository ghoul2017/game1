using ProtoBuf;
using Shared.Core.Network;
using System.Collections.Generic;

namespace Shared.Common.Protos
{
    #region 进入地图

    [ProtoContract]
    [Message("Map.EnterMapReq")]
    public class EnterMapReq : IMessage
    {
        [ProtoMember(1)] public int MapCfgId { get; set; }
        [ProtoMember(2)] public int Line { get; set; }
    }

    [ProtoContract]
    [Message("Map.EnterMapAck")]
    public class EnterMapAck : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
        [ProtoMember(2)] public int MapCfgId { get; set; }
        [ProtoMember(3)] public int Line { get; set; }
        [ProtoMember(4)] public int X { get; set; }
        [ProtoMember(5)] public int Y { get; set; }
    }

    #endregion

    #region 离开地图

    [ProtoContract]
    [Message("Map.LeaveMapReq")]
    public class LeaveMapReq : IMessage
    {
    }

    [ProtoContract]
    [Message("Map.LeaveMapAck")]
    public class LeaveMapAck : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
    }

    #endregion

    #region 切换地图

    [ProtoContract]
    [Message("Map.ChangeMapReq")]
    public class ChangeMapReq : IMessage
    {
        [ProtoMember(1)] public int TargetMapCfgId { get; set; }
        [ProtoMember(2)] public int TargetLine { get; set; }
        [ProtoMember(3)] public int X { get; set; }
        [ProtoMember(4)] public int Y { get; set; }
    }

    [ProtoContract]
    [Message("Map.ChangeMapAck")]
    public class ChangeMapAck : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
        [ProtoMember(2)] public int MapCfgId { get; set; }
        [ProtoMember(3)] public int Line { get; set; }
        [ProtoMember(4)] public int X { get; set; }
        [ProtoMember(5)] public int Y { get; set; }
    }

    #endregion

    #region 地图移动

    [ProtoContract]
    [Message("Map.MapMoveReq")]
    public class MapMoveReq : IMessage
    {
        [ProtoMember(1)] public int X { get; set; }
        [ProtoMember(2)] public int Y { get; set; }
    }

    [ProtoContract]
    [Message("Map.MapMoveAck")]
    public class MapMoveAck : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
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
    [Message("Map.MapObjectsNtf")]
    public class MapObjectsNtf : IMessage
    {
        [ProtoMember(1)] public List<MapObjectInfo> Objects { get; set; } = new List<MapObjectInfo>();
    }

    [ProtoContract]
    [Message("Map.MapPlayerEnterNtf")]
    public class MapPlayerEnterNtf : IMessage
    {
        [ProtoMember(1)] public MapObjectInfo Player { get; set; } = new MapObjectInfo();
    }

    [ProtoContract]
    [Message("Map.MapPlayerLeaveNtf")]
    public class MapPlayerLeaveNtf : IMessage
    {
        [ProtoMember(1)] public long RoleId { get; set; }
    }

    [ProtoContract]
    [Message("Map.MapPlayerMoveNtf")]
    public class MapPlayerMoveNtf : IMessage
    {
        [ProtoMember(1)] public long RoleId { get; set; }
        [ProtoMember(2)] public int X { get; set; }
        [ProtoMember(3)] public int Y { get; set; }
    }

    #endregion

    #region NPC通知消息

    [ProtoContract]
    [Message("Map.MapObjectEnterNtf")]
    public class MapObjectEnterNtf : IMessage
    {
        [ProtoMember(1)] public long ObjectId { get; set; }
        [ProtoMember(2)] public int CfgId { get; set; }
        [ProtoMember(3)] public string Name { get; set; } = "";
        [ProtoMember(4)] public int X { get; set; }
        [ProtoMember(5)] public int Y { get; set; }
        [ProtoMember(6)] public int ObjectType { get; set; }
    }

    [ProtoContract]
    [Message("Map.MapObjectLeaveNtf")]
    public class MapObjectLeaveNtf : IMessage
    {
        [ProtoMember(1)] public long ObjectId { get; set; }
    }

    [ProtoContract]
    [Message("Map.MapObjectMoveNtf")]
    public class MapObjectMoveNtf : IMessage
    {
        [ProtoMember(1)] public long ObjectId { get; set; }
        [ProtoMember(2)] public int X { get; set; }
        [ProtoMember(3)] public int Y { get; set; }
    }

    #endregion

    #region 怪物掉落通知

    [ProtoContract]
    [Message("Map.MonsterDropNtf")]
    public class MonsterDropNtf : IMessage
    {
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
