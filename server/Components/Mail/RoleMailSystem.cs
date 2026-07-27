using Game.Components.Bag;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Components.Mail;

/// <summary>
/// 邮件系统。提供邮件收发、已读标记和删除等功能。
/// </summary>
public static class RoleMailSystem
{
    public const int MaxVisibleMailCount = 100;

    /// <summary>发送邮件给指定角色</summary>
    public static void SendMail(this RoleMailComponent self, string sender, string title, string content, int[] rewardConfigs, int[] rewardCounts, long expireTime = 0)
    {
        long roleId = self.Parent!.Id;
        List<BagItemAddData> normalizedAttachments = BagItemAddDataGenerator.Generate(rewardConfigs, rewardCounts);
        RoleMail mail = new()
        {
            Id = IdGenerator.Instance.NextId(),
            RoleId = roleId,
            Sender = sender,
            Title = title,
            Content = content,
            Attachments = normalizedAttachments,
            SendTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ExpireTime = expireTime,
            State = 0,
        };

        self.Mails[mail.Id] = mail;
        self.MarkDirty();

        Context.Players.SendToPlayer(roleId, new MailNewNtf
        {
            Mail = self.ToMailInfo(mail),
        });
    }

    /// <summary>获取未读邮件数量</summary>
    public static int GetUnreadCount(this RoleMailComponent self)
    {
        return self.Mails.Values.Count(m => !m.IsRead && !m.IsExpired);
    }

    /// <summary>转换为网络传输用的 MailInfo</summary>
    public static MailInfo ToMailInfo(this RoleMailComponent self, RoleMail mail)
    {
        return new MailInfo
        {
            MailId = mail.Id,
            Sender = mail.Sender,
            Title = mail.Title,
            Content = mail.Content,
            Attachments = mail.Attachments.Select(a => new BagItemAddInfo
            {
                ConfigId = a.ConfigId,
                Count = a.Count,
                Bind = a.Bind,
                ExpireTime = a.ExpireTime,
            }).ToList(),
            SendTime = mail.SendTime,
            State = mail.State,
            ExpireTime = mail.ExpireTime,
        };
    }
}
