using System;
using System.Collections.Generic;

namespace Game.Components.Mail;

/// <summary>
/// 单封邮件数据
/// </summary>
public class RoleMail
{
    /// <summary>邮件ID</summary>
    public long Id { get; set; }

    /// <summary>收件人角色ID</summary>
    public long RoleId { get; set; }

    /// <summary>发件人名称</summary>
    public string Sender { get; set; } = "";

    /// <summary>邮件标题</summary>
    public string Title { get; set; } = "";

    /// <summary>邮件内容</summary>
    public string Content { get; set; } = "";

    /// <summary>附件列表</summary>
    public List<MailAttachment> Attachments { get; set; } = [];

    /// <summary>发送时间（Unix毫秒）</summary>
    public long SendTime { get; set; }

    /// <summary>过期时间（Unix毫秒，0=永不过期）</summary>
    public long ExpireTime { get; set; }

    /// <summary>邮件状态：0=未读, 1=已读, 2=附件已领取</summary>
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

/// <summary>
/// 邮件附件
/// </summary>
public class MailAttachment
{
    /// <summary>物品配置ID</summary>
    public int ConfigId { get; set; }

    /// <summary>物品数量</summary>
    public int Count { get; set; }
}
