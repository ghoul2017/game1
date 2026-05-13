using Game.Components.Bag;
using Game.Components.Player;
using Game.Database;
using Shared.Common.Protos;
using Shared.Core;
using Shared.Core.Log;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Components.Mail;

/// <summary>
/// 邮件系统。提供邮件收发、已读标记、附件领取、删除等功能。
/// </summary>
public static class RoleMailSystem
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(RoleMailSystem));

    private const int MaxMailCount = 100;

    /// <summary>加载角色邮件列表</summary>
    public static async Task<RoleMailComponent> LoadMailsAsync(this RoleMailComponent self, long roleId)
    {
        var mailList = await MongoDb.Instance.LoadAsync<RoleMailComponent>(roleId);
        if (mailList == null)
        {
            mailList = new RoleMailComponent { Id = roleId };
            await MongoDb.Instance.InsertAsync(mailList);
            Log.Info($"创建新邮件列表 RoleId:{roleId}");
        }

        // 把数据库加载的数据拷贝到当前 Component
        self.Id = mailList.Id;
        self.Mails = mailList.Mails;
        self.RebuildIndex();

        // 清理过期邮件
        CleanExpired(self);

        return self;
    }

    /// <summary>保存邮件列表</summary>
    public static async Task SaveMailsAsync(this RoleMailComponent self)
    {
        await MongoDb.Instance.UpdateAsync(self);
    }

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

        self.Mails.Add(mail);
        self.GetMailMap()[mail.Id] = mail;

        return MailResult.Ok(mail.Id);
    }

    /// <summary>发送系统邮件（给在线玩家实时推送通知）</summary>
    public static async Task SendSystemMailAsync(this RoleMailComponent self, long roleId, string title, string content,
        List<MailAttachment>? attachments = null, long expireTime = 0)
    {
        var mailList = await self.LoadMailsAsync(roleId);
        var result = self.SendMail("系统", title, content, attachments, expireTime);
        if (result.Success)
        {
            await self.SaveMailsAsync();

            var mail = self.Mails.First(m => m.Id == result.MailId);
            var ntf = new MailNewNtf { Mail = self.ToMailInfo(mail) };
            Context.Players.SendToPlayer(roleId, ntf);

            Log.Info($"系统邮件已发送 RoleId:{roleId} Title:{title}");
        }
    }

    /// <summary>获取邮件列表（按发送时间倒序）</summary>
    public static List<RoleMail> GetMailList(this RoleMailComponent self)
    {
        return self.Mails
            .Where(m => !m.IsExpired)
            .OrderByDescending(m => m.SendTime)
            .ToList();
    }

    /// <summary>标记邮件已读</summary>
    public static MarkResult MarkRead(this RoleMailComponent self, List<long> mailIds)
    {
        var readIds = new List<long>();
        var map = self.GetMailMap();

        foreach (var mailId in mailIds)
        {
            if (map.TryGetValue(mailId, out var mail) && !mail.IsRead)
            {
                mail.State = 1;
                readIds.Add(mailId);
            }
        }

        return readIds.Count > 0 ? MarkResult.Ok(readIds) : MarkResult.None();
    }

    /// <summary>领取附件（将物品加入背包）</summary>
    public static async Task<ClaimResult> ClaimAttachmentsAsync(this RoleMailComponent self, List<long> mailIds)
    {
        var claimedIds = new List<long>();
        var allItems = new List<MailAttachment>();
        var map = self.GetMailMap();

        foreach (var mailId in mailIds)
        {
            if (!map.TryGetValue(mailId, out var mail)) continue;
            if (mail.IsClaimed || !mail.HasUnclaimedAttachment) continue;

            allItems.AddRange(mail.Attachments);
            mail.State = 2;
            claimedIds.Add(mailId);
        }

        if (claimedIds.Count == 0)
            return ClaimResult.None();

        var player = Context.Players.GetByRoleId(self.Id);
        if (player == null) return ClaimResult.None();

        foreach (var att in allItems)
        {
            player.RoleBag.AddItem(att.ConfigId, att.Count);
        }

        await player.RoleBag.SaveBagAsync();
        await self.SaveMailsAsync();

        return ClaimResult.Ok(claimedIds, allItems);
    }

    /// <summary>删除邮件</summary>
    public static DeleteResult DeleteMails(this RoleMailComponent self, List<long> mailIds)
    {
        var deletedIds = new List<long>();
        var map = self.GetMailMap();

        foreach (var mailId in mailIds)
        {
            if (!map.TryGetValue(mailId, out var mail)) continue;

            if (mail.HasUnclaimedAttachment)
                continue;

            map.Remove(mailId);
            self.Mails.Remove(mail);
            deletedIds.Add(mailId);
        }

        return deletedIds.Count > 0 ? DeleteResult.Ok(deletedIds) : DeleteResult.None();
    }

    /// <summary>获取未读邮件数量</summary>
    public static int GetUnreadCount(this RoleMailComponent self)
    {
        return self.Mails.Count(m => !m.IsRead && !m.IsExpired);
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

    /// <summary>清理过期邮件</summary>
    private static void CleanExpired(RoleMailComponent self)
    {
        var expired = self.Mails.Where(m => m.IsExpired).ToList();
        if (expired.Count == 0) return;

        foreach (var mail in expired)
        {
            self.Mails.Remove(mail);
            self.GetMailMap().Remove(mail.Id);
        }

        Log.Info($"清理过期邮件 RoleId:{self.Id} Count:{expired.Count}");
    }
}

#region 结果类型

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

public record ClaimResult(bool Success, List<long> ClaimedIds, List<MailAttachment> Items)
{
    public static ClaimResult Ok(List<long> ids, List<MailAttachment> items) => new(true, ids, items);
    public static ClaimResult None() => new(false, [], []);
}

public record DeleteResult(bool Changed, List<long> DeletedIds)
{
    public static DeleteResult Ok(List<long> ids) => new(true, ids);
    public static DeleteResult None() => new(false, []);
}

#endregion
