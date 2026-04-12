using System.Collections.Generic;
using ProtoBuf;

namespace Client.Shared.Messages
{
    // ─── 查询背包 ───

    [ProtoContract]
    public class QueryBagReq : IMessage
    {
        public ushort MsgId => MsgId.QueryBagReq;
    }

    [ProtoContract]
    public class QueryBagAck : IMessage
    {
        public ushort MsgId => MsgId.QueryBagAck;
        [ProtoMember(1)] public int Code { get; set; }
        [ProtoMember(2)] public List<BagItemInfo> Items { get; set; } = new List<BagItemInfo>();
        [ProtoMember(3)] public int Capacity { get; set; }
    }

    // ─── 整理背包 ───

    [ProtoContract]
    public class BagTidyReq : IMessage
    {
        public ushort MsgId => MsgId.BagTidyReq;
    }

    [ProtoContract]
    public class BagTidyAck : IMessage
    {
        public ushort MsgId => MsgId.BagTidyAck;
        [ProtoMember(1)] public int Code { get; set; }
        [ProtoMember(2)] public List<BagItemInfo> Items { get; set; } = new List<BagItemInfo>();
    }

    // ─── 交换物品 ───

    [ProtoContract]
    public class BagSwapReq : IMessage
    {
        public ushort MsgId => MsgId.BagSwapReq;
        [ProtoMember(1)] public int Index1 { get; set; }
        [ProtoMember(2)] public int Index2 { get; set; }
    }

    [ProtoContract]
    public class BagSwapAck : IMessage
    {
        public ushort MsgId => MsgId.BagSwapAck;
        [ProtoMember(1)] public int Code { get; set; }
    }

    // ─── 使用物品 ───

    [ProtoContract]
    public class BagUseItemReq : IMessage
    {
        public ushort MsgId => MsgId.BagUseItemReq;
        [ProtoMember(1)] public int Index { get; set; }
        [ProtoMember(2)] public int Count { get; set; }
    }

    [ProtoContract]
    public class BagUseItemAck : IMessage
    {
        public ushort MsgId => MsgId.BagUseItemAck;
        [ProtoMember(1)] public int Code { get; set; }
        [ProtoMember(2)] public int ConfigId { get; set; }
    }

    // ─── 物品变化通知 ───

    [ProtoContract]
    public class BagItemChangedNtf : IMessage
    {
        public ushort MsgId => MsgId.BagItemChangedNtf;
        [ProtoMember(1)] public List<BagItemInfo> ChangedItems { get; set; } = new List<BagItemInfo>();
        [ProtoMember(2)] public List<int> RemovedIndices { get; set; } = new List<int>();
    }

    // ─── 通用物品信息 ───

    [ProtoContract]
    public class BagItemInfo
    {
        [ProtoMember(1)] public long Uid { get; set; }
        [ProtoMember(2)] public int ConfigId { get; set; }
        [ProtoMember(3)] public int Count { get; set; }
        [ProtoMember(4)] public int Index { get; set; }
        [ProtoMember(5)] public bool Bind { get; set; }
    }
}
