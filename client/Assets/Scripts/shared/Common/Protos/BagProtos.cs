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
    [Message("Bag.BagUseItemReq")]
    public class BagUseItemReq : IMessage
    {
        [ProtoMember(1)] public long Uid { get; set; }
    }

    [ProtoContract]
    [Message("Bag.BagUseItemAck")]
    public class BagUseItemAck : IMessage
    {
        [ProtoMember(1)] public string Error { get; set; } = "";
    }



    [ProtoContract]
    [Message("Bag.BagItemChangedNtf")]
    public class BagItemChangedNtf : IMessage
    {
        [ProtoMember(1)] public List<BagItemInfo> Items { get; set; } = new List<BagItemInfo>();
        [ProtoMember(2)] public List<long> RemovedUids { get; set; } = new List<long>();
    }



    [ProtoContract]
    public class BagItemInfo
    {
        [ProtoMember(1)] public long Uid { get; set; }
        [ProtoMember(2)] public int ConfigId { get; set; }
        [ProtoMember(3)] public long Count { get; set; }
        [ProtoMember(5)] public bool Bind { get; set; }
        [ProtoMember(6)] public long ExpireTime { get; set; }
    }

    [ProtoContract]
    public class BagItemAddInfo
    {
        [ProtoMember(1)] public int ConfigId { get; set; }
        [ProtoMember(2)] public long Count { get; set; }
        [ProtoMember(3)] public bool Bind { get; set; }
        [ProtoMember(4)] public long ExpireTime { get; set; }
    }

    [ProtoContract]
    public class ItemTipsData
    {
        [ProtoMember(1)] public int ConfigId { get; set; }
        [ProtoMember(2)] public long Count { get; set; }
    }

}
