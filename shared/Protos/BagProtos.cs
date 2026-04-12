using ProtoBuf;
using GameShared.Network;
using System.Collections.Generic;

namespace GameShared.Protos
{
    #region 查询背包

    [ProtoContract]
    public class QueryBagReq : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.QueryBagReq;
    }

    [ProtoContract]
    public class QueryBagAck : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.QueryBagAck;
        [ProtoMember(1)] public int Code { get; set; }
        [ProtoMember(2)] public List<BagItemInfo> Items { get; set; } = new List<BagItemInfo>();
        [ProtoMember(3)] public int Capacity { get; set; }
    }

    #endregion

    #region 整理背包

    [ProtoContract]
    public class BagTidyReq : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.BagTidyReq;
    }

    [ProtoContract]
    public class BagTidyAck : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.BagTidyAck;
        [ProtoMember(1)] public int Code { get; set; }
        [ProtoMember(2)] public List<BagItemInfo> Items { get; set; } = new List<BagItemInfo>();
    }

    #endregion

    #region 交换物品

    [ProtoContract]
    public class BagSwapReq : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.BagSwapReq;
        [ProtoMember(1)] public int Index1 { get; set; }
        [ProtoMember(2)] public int Index2 { get; set; }
    }

    [ProtoContract]
    public class BagSwapAck : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.BagSwapAck;
        [ProtoMember(1)] public int Code { get; set; }
    }

    #endregion

    #region 使用物品

    [ProtoContract]
    public class BagUseItemReq : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.BagUseItemReq;
        [ProtoMember(1)] public int Index { get; set; }
        [ProtoMember(2)] public int Count { get; set; }
    }

    [ProtoContract]
    public class BagUseItemAck : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.BagUseItemAck;
        [ProtoMember(1)] public int Code { get; set; }
        [ProtoMember(2)] public int ConfigId { get; set; }
    }

    #endregion

    #region 物品变化通知

    [ProtoContract]
    public class BagItemChangedNtf : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.BagItemChangedNtf;
        [ProtoMember(1)] public List<BagItemInfo> ChangedItems { get; set; } = new List<BagItemInfo>();
        [ProtoMember(2)] public List<int> RemovedIndices { get; set; } = new List<int>();
    }

    #endregion

    #region 通用物品信息

    [ProtoContract]
    public class BagItemInfo
    {
        [ProtoMember(1)] public long Uid { get; set; }
        [ProtoMember(2)] public int ConfigId { get; set; }
        [ProtoMember(3)] public int Count { get; set; }
        [ProtoMember(4)] public int Index { get; set; }
        [ProtoMember(5)] public bool Bind { get; set; }
    }

    #endregion
}
