using System;
using System.Collections.Generic;
using Game.Core.Entity;
using ProtoBuf;

namespace Game.Game.Mail;

/// <summary>
/// 角色邮件列表实体。每个角色一条记录，包含所有邮件。
/// 对应数据库表: p_role_mail_list (id=roleId, data=protobuf)
/// </summary>
[ProtoContract]
public class RoleMailList : IEntity
{
    [ProtoMember(1)]
    public long Id { get; set; }

    /// <summary>邮件列表</summary>
    [ProtoMember(2)]
    public List<RoleMail> Mails { get; set; } = [];

    /// <summary>运行时索引：mailId → RoleMail（不序列化）</summary>
    [ProtoIgnore]
    private Dictionary<long, RoleMail>? _mailMap;

    [ProtoAfterDeserialization]
    internal void AfterDeserialize()
    {
        RebuildIndex();
    }

    internal void RebuildIndex()
    {
        _mailMap = new Dictionary<long, RoleMail>(Mails.Count);
        foreach (var mail in Mails)
        {
            _mailMap[mail.Id] = mail;
        }
    }

    internal Dictionary<long, RoleMail> GetMailMap()
    {
        _mailMap ??= new Dictionary<long, RoleMail>(Mails.Count);
        return _mailMap;
    }
}

/// <summary>单封邮件数据</summary>
[ProtoContract]
public class RoleMail
{
    /// <summary>邮件ID</summary>
    [ProtoMember(1)]
    public long Id { get; set; }

    /// <summary>收件人角色ID</summary>
    [ProtoMember(2)]
    public long RoleId { get; set; }

    /// <summary>发件人名称</summary>
    [ProtoMember(3)]
    public string Sender { get; set; } = "";

    /// <summary>邮件标题</summary>
    [ProtoMember(4)]
    public string Title { get; set; } = "";

    /// <summary>邮件内容</summary>
    [ProtoMember(5)]
    public string Content { get; set; } = "";

    /// <summary>附件列表</summary>
    [ProtoMember(6)]
    public List<MailAttachment> Attachments { get; set; } = [];

    /// <summary>发送时间（Unix毫秒）</summary>
    [ProtoMember(7)]
    public long SendTime { get; set; }

    /// <summary>过期时间（Unix毫秒，0=永不过期）</summary>
    [ProtoMember(8)]
    public long ExpireTime { get; set; }

    /// <summary>邮件状态：0=未读, 1=已读, 2=附件已领取</summary>
    [ProtoMember(9)]
    public int State { get; set; }

    /// <summary>是否已读</summary>
    public bool IsRead => State >= 1;

    /// <summary>附件是否已领取</summary>
    public bool IsClaimed => State >= 2;

    /// <summary>是否已过期</summary>
    public bool IsExpired => ExpireTime > 0 && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > ExpireTime;

    /// <summary>是否有未领取的附件</summary>
    public bool HasUnclaimedAttachment => !IsClaimed && Attachments.Count > 0;
}

/// <summary>邮件附件</summary>
[ProtoContract]
public class MailAttachment
{
    /// <summary>物品配置ID</summary>
    [ProtoMember(1)]
    public int ConfigId { get; set; }

    /// <summary>物品数量</summary>
    [ProtoMember(2)]
    public int Count { get; set; }
}
