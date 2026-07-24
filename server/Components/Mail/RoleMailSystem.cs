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
    private const int MaxMailCount = 100;

    /// <summary>发送邮件给指定角色</summary>
    public static MailResult SendMail(this RoleMailComponent self, string sender, string title, string content,
        List<MailAttachment>? attachments = null, long expireTime = 0)
    {
        if (self.Mails.Count >= MaxMailCount)
            return MailResult.Fail("邮箱已满");

        var mail = new RoleMail
        {
            Id = IdGenerator.Instance.NextId(),
            RoleId = self.Id,
            Sender = sender,
            Title = title,
            Content = content,
            Attachments = attachments ?? [],
            SendTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ExpireTime = expireTime,
            State = 0,
        };

        self.Mails[mail.Id] = mail;
        self.MarkDirty();

        return MailResult.Ok(mail.Id);
    }

    /// <summary>获取邮件列表（按发送时间倒序）</summary>
    public static List<RoleMail> GetMailList(this RoleMailComponent self)
    {
        return self.Mails.Values
            .Where(m => !m.IsExpired)
            .OrderByDescending(m => m.SendTime)
            .ToList();
    }

    /// <summary>标记邮件已读</summary>
    public static MarkResult MarkRead(this RoleMailComponent self, List<long> mailIds)
    {
        var readIds = new List<long>();

        foreach (var mailId in mailIds)
        {
            if (self.Mails.TryGetValue(mailId, out var mail) && !mail.IsRead)
            {
                mail.State = 1;
                readIds.Add(mailId);
            }
        }

        return readIds.Count > 0 ? MarkResult.Ok(readIds) : MarkResult.None();
    }

    /// <summary>删除邮件</summary>
    public static DeleteResult DeleteMails(this RoleMailComponent self, List<long> mailIds)
    {
        var deletedIds = new List<long>();

        foreach (var mailId in mailIds)
        {
            if (!self.Mails.TryGetValue(mailId, out var mail))
                continue;

            if (mail.HasUnclaimedAttachment)
                continue;

            self.Mails.Remove(mailId);
            deletedIds.Add(mailId);
        }

        return deletedIds.Count > 0 ? DeleteResult.Ok(deletedIds) : DeleteResult.None();
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
            Attachments = mail.Attachments.Select(a => new MailAttachmentInfo
            {
                ConfigId = a.ConfigId,
                Count = a.Count,
            }).ToList(),
            SendTime = mail.SendTime,
            State = mail.State,
            ExpireTime = mail.ExpireTime,
        };
    }

}


public record MailResult(bool Success, string ErrorMessage = "", long MailId = 0)
{
    public static MailResult Ok(long mailId) => new(true, "", mailId);
    public static MailResult Fail(string message) => new(false, message, 0);
}

public record MarkResult(bool Changed, List<long> ReadIds)
{
    public static MarkResult Ok(List<long> ids) => new(true, ids);
    public static MarkResult None() => new(false, []);
}

public record DeleteResult(bool Changed, List<long> DeletedIds)
{
    public static DeleteResult Ok(List<long> ids) => new(true, ids);
    public static DeleteResult None() => new(false, []);
}
