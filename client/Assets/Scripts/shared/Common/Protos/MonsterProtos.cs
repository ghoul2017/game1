using ProtoBuf;
using Shared.Core.Network;
using System.Collections.Generic;

namespace Shared.Common.Protos
{

    /// <summary>
    /// 经验变化通知
    /// </summary>
    [ProtoContract]
    public class ExpChangedNtf : IMessage
    {
        ushort IMessage.MsgId => MsgIdConst.ExpChangedNtf;
        [ProtoMember(1)] public int Exp { get; set; }

        [ProtoMember(2)] public int Level { get; set; }
    }

    /// <summary>
    /// 金币变化通知
    /// </summary>
    [ProtoContract]
    public class GoldChangedNtf : IMessage
    {
        ushort IMessage.MsgId => MsgIdConst.GoldChangedNtf;
        [ProtoMember(1)] public int Gold { get; set; }
    }

    /// <summary>
    /// 升级通知
    /// </summary>
    [ProtoContract]
    public class LevelUpNtf : IMessage
    {
        ushort IMessage.MsgId => MsgIdConst.LevelUpNtf;
        [ProtoMember(1)] public int Level { get; set; }

        [ProtoMember(2)] public int MaxHp { get; set; }

        [ProtoMember(3)] public int MaxMp { get; set; }
    }

    /// <summary>
    /// 物品获得通知
    /// </summary>
    [ProtoContract]
    public class ItemGainNtf : IMessage
    {
        ushort IMessage.MsgId => MsgIdConst.ItemGainNtf;
        [ProtoMember(1)] public int ConfigId { get; set; }

        [ProtoMember(2)] public int Count { get; set; }
    }

    /// <summary>
    /// MP变化通知
    /// </summary>
    [ProtoContract]
    public class MpChangedNtf : IMessage
    {
        ushort IMessage.MsgId => MsgIdConst.MpChangedNtf;
        [ProtoMember(1)] public long ObjectId { get; set; }

        [ProtoMember(2)] public int DeltaMp { get; set; }

        [ProtoMember(3)] public int Mp { get; set; }

        [ProtoMember(4)] public int Reason { get; set; }
    }
}