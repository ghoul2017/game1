using ProtoBuf;
using Shared.Core.Network;
using System.Collections.Generic;

namespace Shared.Common.Protos
{

    [ProtoContract]
    [Message("Mail.QueryMailListReq")]
    public class QueryMailListReq : IMessage
    {
    }

    [ProtoContract]
    [Message("Mail.QueryMailListAck")]
    public class QueryMailListAck : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
        [ProtoMember(2)] public List<MailInfo> Mails { get; set; } = new List<MailInfo>();
    }



    [ProtoContract]
    [Message("Mail.MailReadReq")]
    public class MailReadReq : IMessage
    {
        [ProtoMember(1)] public List<long> MailIds { get; set; } = new List<long>();
    }

    [ProtoContract]
    [Message("Mail.MailReadAck")]
    public class MailReadAck : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
        [ProtoMember(2)] public List<long> ReadMailIds { get; set; } = new List<long>();
    }



    [ProtoContract]
    [Message("Mail.MailGetItemReq")]
    public class MailGetItemReq : IMessage
    {
        [ProtoMember(1)] public List<long> MailIds { get; set; } = new List<long>();
    }

    [ProtoContract]
    [Message("Mail.MailGetItemAck")]
    public class MailGetItemAck : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
        [ProtoMember(2)] public List<long> ClaimedMailIds { get; set; } = new List<long>();
        [ProtoMember(3)] public List<MailAttachmentInfo> Items { get; set; } = new List<MailAttachmentInfo>();
    }



    [ProtoContract]
    [Message("Mail.MailDeleteReq")]
    public class MailDeleteReq : IMessage
    {
        [ProtoMember(1)] public List<long> MailIds { get; set; } = new List<long>();
    }

    [ProtoContract]
    [Message("Mail.MailDeleteAck")]
    public class MailDeleteAck : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
        [ProtoMember(2)] public List<long> DeletedMailIds { get; set; } = new List<long>();
    }



    [ProtoContract]
    [Message("Mail.MailNewNtf")]
    public class MailNewNtf : IMessage
    {
        [ProtoMember(1)] public MailInfo Mail { get; set; } = new MailInfo();
    }



    [ProtoContract]
    public class MailInfo
    {
        [ProtoMember(1)] public long MailId { get; set; }
        [ProtoMember(2)] public string Sender { get; set; } = "";
        [ProtoMember(3)] public string Title { get; set; } = "";
        [ProtoMember(4)] public string Content { get; set; } = "";
        [ProtoMember(5)] public List<MailAttachmentInfo> Attachments { get; set; } = new List<MailAttachmentInfo>();
        [ProtoMember(6)] public long SendTime { get; set; }
        [ProtoMember(7)] public int State { get; set; }
        [ProtoMember(8)] public long ExpireTime { get; set; }
    }

    [ProtoContract]
    public class MailAttachmentInfo
    {
        [ProtoMember(1)] public int ConfigId { get; set; }
        [ProtoMember(2)] public int Count { get; set; }
    }

}
