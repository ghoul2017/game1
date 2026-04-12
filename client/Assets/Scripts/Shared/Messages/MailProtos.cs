using System.Collections.Generic;
using ProtoBuf;

namespace Client.Shared.Messages
{
    // ─── 查询邮件列表 ───

    [ProtoContract]
    public class QueryMailListReq : IMessage
    {
        public ushort MsgId => MsgId.QueryMailListReq;
    }

    [ProtoContract]
    public class QueryMailListAck : IMessage
    {
        public ushort MsgId => MsgId.QueryMailListAck;
        [ProtoMember(1)] public int Code { get; set; }
        [ProtoMember(2)] public List<MailInfo> Mails { get; set; } = new List<MailInfo>();
    }

    // ─── 标记已读 ───

    [ProtoContract]
    public class MailReadReq : IMessage
    {
        public ushort MsgId => MsgId.MailReadReq;
        [ProtoMember(1)] public List<long> MailIds { get; set; } = new List<long>();
    }

    [ProtoContract]
    public class MailReadAck : IMessage
    {
        public ushort MsgId => MsgId.MailReadAck;
        [ProtoMember(1)] public int Code { get; set; }
        [ProtoMember(2)] public List<long> ReadMailIds { get; set; } = new List<long>();
    }

    // ─── 领取附件 ───

    [ProtoContract]
    public class MailGetItemReq : IMessage
    {
        public ushort MsgId => MsgId.MailGetItemReq;
        [ProtoMember(1)] public List<long> MailIds { get; set; } = new List<long>();
    }

    [ProtoContract]
    public class MailGetItemAck : IMessage
    {
        public ushort MsgId => MsgId.MailGetItemAck;
        [ProtoMember(1)] public int Code { get; set; }
        [ProtoMember(2)] public List<long> ClaimedMailIds { get; set; } = new List<long>();
        [ProtoMember(3)] public List<MailAttachmentInfo> Items { get; set; } = new List<MailAttachmentInfo>();
    }

    // ─── 删除邮件 ───

    [ProtoContract]
    public class MailDeleteReq : IMessage
    {
        public ushort MsgId => MsgId.MailDeleteReq;
        [ProtoMember(1)] public List<long> MailIds { get; set; } = new List<long>();
    }

    [ProtoContract]
    public class MailDeleteAck : IMessage
    {
        public ushort MsgId => MsgId.MailDeleteAck;
        [ProtoMember(1)] public int Code { get; set; }
        [ProtoMember(2)] public List<long> DeletedMailIds { get; set; } = new List<long>();
    }

    // ─── 新邮件通知 ───

    [ProtoContract]
    public class MailNewNtf : IMessage
    {
        public ushort MsgId => MsgId.MailNewNtf;
        [ProtoMember(1)] public MailInfo Mail { get; set; } = new MailInfo();
    }

    // ─── 通用邮件信息 ───

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
