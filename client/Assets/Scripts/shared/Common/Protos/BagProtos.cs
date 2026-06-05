using ProtoBuf;
using Shared.Core.Network;
using System.Collections.Generic;


namespace Shared.Common.Protos
{

    [ProtoContract]
    [Message("Bag.QueryBagReq")]
    public class QueryBagReq : IMessage
    {
    }

    [ProtoContract]
    [Message("Bag.QueryBagAck")]
    public class QueryBagAck : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
        [ProtoMember(2)] public List<BagItemInfo> Items { get; set; } = new List<BagItemInfo>();
        [ProtoMember(3)] public int Capacity { get; set; }
    }



    [ProtoContract]
    [Message("Bag.BagTidyReq")]
    public class BagTidyReq : IMessage
    {
    }

    [ProtoContract]
    [Message("Bag.BagTidyAck")]
    public class BagTidyAck : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
        [ProtoMember(2)] public List<BagItemInfo> Items { get; set; } = new List<BagItemInfo>();
    }



    [ProtoContract]
    [Message("Bag.BagSwapReq")]
    public class BagSwapReq : IMessage
    {
        [ProtoMember(1)] public int Index1 { get; set; }
        [ProtoMember(2)] public int Index2 { get; set; }
    }

    [ProtoContract]
    [Message("Bag.BagSwapAck")]
    public class BagSwapAck : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
    }



    [ProtoContract]
    [Message("Bag.BagUseItemReq")]
    public class BagUseItemReq : IMessage
    {
        [ProtoMember(1)] public int Index { get; set; }
        [ProtoMember(2)] public int Count { get; set; }
    }

    [ProtoContract]
    [Message("Bag.BagUseItemAck")]
    public class BagUseItemAck : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
        [ProtoMember(2)] public int ConfigId { get; set; }
    }



    [ProtoContract]
    [Message("Bag.BagItemChangedNtf")]
    public class BagItemChangedNtf : IMessage
    {
        [ProtoMember(1)] public List<BagItemInfo> ChangedItems { get; set; } = new List<BagItemInfo>();
        [ProtoMember(2)] public List<int> RemovedIndices { get; set; } = new List<int>();
    }



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
