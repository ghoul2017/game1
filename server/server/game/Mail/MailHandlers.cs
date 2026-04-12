using Server.Core.Log;
using Server.Game.Player;
using Server.Network;
using GameShared.Network;
using GameShared.Protos;

namespace Server.Game.Mail;

/// <summary>
/// 查询邮件列表 Handler
/// </summary>
public class QueryMailListHandler : MessageHandler<QueryMailListReq>
{
    private static readonly ILog Log = LogManager.GetLogger<QueryMailListHandler>();

    protected override async Task<IMessage?> ExecuteAsync(TcpSession session, QueryMailListReq req)
    {
        var resp = new QueryMailListAck();

        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            resp.Code = MailErrorCode.InvalidParam;
            return resp;
        }

        var mailList = await MailManager.Instance.LoadMailsAsync(player.RoleId);
        var mails = MailManager.Instance.GetMailList(mailList);

        resp.Code = MailErrorCode.Ok;
        resp.Mails = mails.Select(MailManager.Instance.ToMailInfo).ToList();

        return resp;
    }
}

/// <summary>
/// 标记邮件已读 Handler
/// </summary>
public class MailReadHandler : MessageHandler<MailReadReq>
{
    protected override async Task<IMessage?> ExecuteAsync(TcpSession session, MailReadReq req)
    {
        var resp = new MailReadAck();

        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            resp.Code = MailErrorCode.InvalidParam;
            return resp;
        }

        var mailList = await MailManager.Instance.LoadMailsAsync(player.RoleId);
        var result = MailManager.Instance.MarkRead(mailList, req.MailIds);

        if (result.Changed)
        {
            await MailManager.Instance.SaveMailsAsync(mailList);
        }

        resp.Code = MailErrorCode.Ok;
        resp.ReadMailIds = result.ReadIds;

        return resp;
    }
}

/// <summary>
/// 领取邮件附件 Handler
/// </summary>
public class MailGetItemHandler : MessageHandler<MailGetItemReq>
{
    private static readonly ILog Log = LogManager.GetLogger<MailGetItemHandler>();

    protected override async Task<IMessage?> ExecuteAsync(TcpSession session, MailGetItemReq req)
    {
        var resp = new MailGetItemAck();

        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            resp.Code = MailErrorCode.InvalidParam;
            return resp;
        }

        var mailList = await MailManager.Instance.LoadMailsAsync(player.RoleId);
        var result = await MailManager.Instance.ClaimAttachmentsAsync(mailList, req.MailIds);

        resp.Code = MailErrorCode.Ok;
        resp.ClaimedMailIds = result.ClaimedIds;
        resp.Items = result.Items.Select(a => new MailAttachmentInfo
        {
            ConfigId = a.ConfigId,
            Count = a.Count,
        }).ToList();

        if (result.Success)
        {
            Log.Info($"领取邮件附件 RoleId:{session.RoleId} Count:{result.Items.Count}");
        }

        return resp;
    }
}

/// <summary>
/// 删除邮件 Handler
/// </summary>
public class MailDeleteHandler : MessageHandler<MailDeleteReq>
{
    protected override async Task<IMessage?> ExecuteAsync(TcpSession session, MailDeleteReq req)
    {
        var resp = new MailDeleteAck();

        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            resp.Code = MailErrorCode.InvalidParam;
            return resp;
        }

        var mailList = await MailManager.Instance.LoadMailsAsync(player.RoleId);
        var result = MailManager.Instance.DeleteMails(mailList, req.MailIds);

        if (result.Changed)
        {
            await MailManager.Instance.SaveMailsAsync(mailList);
        }

        resp.Code = MailErrorCode.Ok;
        resp.DeletedMailIds = result.DeletedIds;

        return resp;
    }
}
