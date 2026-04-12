using ProtoBuf;
using Server.Core.Entity;
using Server.Core.Log;
using Server.Game.Bag;
using Server.Game.Player;
using Server.Network;
using GameShared.Network;
using GameShared.Protos;

namespace Server.Game.Mail;

/// <summary>
/// 邮件管理器。提供邮件收发、已读标记、附件领取、删除等功能。
/// 每个角色的邮件列表以单个实体 RoleMailList 存储，避免逐条操作。
/// </summary>
public class MailManager
{
    private static readonly ILog Log = LogManager.GetLogger<MailManager>();

    public static MailManager Instance { get; } = new();

    private static long _nextMailId;

    /// <summary>每个角色邮件数量上限</summary>
    private const int MaxMailCount = 100;

    private MailManager()
    {
    }

    /// <summary>加载角色邮件列表</summary>
    public async Task<RoleMailList> LoadMailsAsync(long roleId)
    {
        var mailList = await EntityFactory.Instance.GetAsync<RoleMailList>(roleId);
        if (mailList == null)
        {
            mailList = new RoleMailList { Id = roleId };
            await EntityFactory.Instance.InsertAsync(mailList);
            Log.Info($"创建新邮件列表 RoleId:{roleId}");
        }

        // 清理过期邮件
        CleanExpired(mailList);

        return mailList;
    }

    /// <summary>保存邮件列表（Write-Behind）</summary>
    public async Task SaveMailsAsync(RoleMailList mailList)
    {
        await EntityFactory.Instance.UpdateAsync(mailList);
    }

    /// <summary>发送邮件给指定角色</summary>
    public MailResult SendMail(RoleMailList mailList, string sender, string title, string content,
        List<MailAttachment>? attachments = null, long expireTime = 0)
    {
        if (mailList.Mails.Count >= MaxMailCount)
            return MailResult.Fail(MailErrorCode.MailFull);

        var mail = new RoleMail
        {
            Id = Interlocked.Increment(ref _nextMailId),
            RoleId = mailList.Id,
            Sender = sender,
            Title = title,
            Content = content,
            Attachments = attachments ?? [],
            SendTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ExpireTime = expireTime,
            State = 0,
        };

        mailList.Mails.Add(mail);
        mailList.GetMailMap()[mail.Id] = mail;

        return MailResult.Ok(mail.Id);
    }

    /// <summary>发送系统邮件（给在线玩家实时推送通知）</summary>
    public async Task SendSystemMailAsync(long roleId, string title, string content,
        List<MailAttachment>? attachments = null, long expireTime = 0)
    {
        var mailList = await LoadMailsAsync(roleId);
        var result = SendMail(mailList, "系统", title, content, attachments, expireTime);
        if (result.Success)
        {
            await SaveMailsAsync(mailList);

            // 如果玩家在线，推送新邮件通知
            var mail = mailList.Mails.First(m => m.Id == result.MailId);
            var ntf = new MailNewNtf { Mail = ToMailInfo(mail) };
            PlayerManager.Instance.SendToPlayer(roleId, ntf);

            Log.Info($"系统邮件已发送 RoleId:{roleId} Title:{title}");
        }
    }

    /// <summary>获取邮件列表（按发送时间倒序）</summary>
    public List<RoleMail> GetMailList(RoleMailList mailList)
    {
        return mailList.Mails
            .Where(m => !m.IsExpired)
            .OrderByDescending(m => m.SendTime)
            .ToList();
    }

    /// <summary>标记邮件已读</summary>
    public MarkResult MarkRead(RoleMailList mailList, List<long> mailIds)
    {
        var readIds = new List<long>();
        var map = mailList.GetMailMap();

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
    public async Task<ClaimResult> ClaimAttachmentsAsync(RoleMailList mailList, List<long> mailIds)
    {
        var claimedIds = new List<long>();
        var allItems = new List<MailAttachment>();
        var map = mailList.GetMailMap();

        // 先收集所有要领取的附件
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

        // 加载背包并添加物品
        var bag = await BagManager.Instance.LoadBagAsync(mailList.Id);
        foreach (var att in allItems)
        {
            BagManager.Instance.AddItem(bag, att.ConfigId, att.Count);
        }

        await BagManager.Instance.SaveBagAsync(bag);
        await SaveMailsAsync(mailList);

        return ClaimResult.Ok(claimedIds, allItems);
    }

    /// <summary>删除邮件（只能删除已读且无未领取附件的邮件）</summary>
    public DeleteResult DeleteMails(RoleMailList mailList, List<long> mailIds)
    {
        var deletedIds = new List<long>();
        var map = mailList.GetMailMap();

        foreach (var mailId in mailIds)
        {
            if (!map.TryGetValue(mailId, out var mail)) continue;

            // 有未领取附件的邮件不能删除
            if (mail.HasUnclaimedAttachment)
                continue;

            map.Remove(mailId);
            mailList.Mails.Remove(mail);
            deletedIds.Add(mailId);
        }

        return deletedIds.Count > 0 ? DeleteResult.Ok(deletedIds) : DeleteResult.None();
    }

    /// <summary>获取未读邮件数量</summary>
    public int GetUnreadCount(RoleMailList mailList)
    {
        return mailList.Mails.Count(m => !m.IsRead && !m.IsExpired);
    }

    /// <summary>转换为网络传输用的 MailInfo</summary>
    public MailInfo ToMailInfo(RoleMail mail)
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
    private void CleanExpired(RoleMailList mailList)
    {
        var expired = mailList.Mails.Where(m => m.IsExpired).ToList();
        if (expired.Count == 0) return;

        foreach (var mail in expired)
        {
            mailList.Mails.Remove(mail);
            mailList.GetMailMap().Remove(mail.Id);
        }

        Log.Info($"清理过期邮件 RoleId:{mailList.Id} Count:{expired.Count}");
    }
}

#region 结果类型

public record MailResult(bool Success, int ErrorCode, long MailId)
{
    public static MailResult Ok(long mailId) => new(true, 0, mailId);
    public static MailResult Fail(int error) => new(false, error, 0);
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

public static class MailErrorCode
{
    public const int Ok = 0;
    public const int InvalidParam = 2001;
    public const int MailNotFound = 2002;
    public const int MailFull = 2003;
    public const int AttachmentNotClaimed = 2004;
}

#endregion