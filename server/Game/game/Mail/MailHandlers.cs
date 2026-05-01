using System.Linq;
using System.Threading.Tasks;
using Game.Game.Player;
using Game.Network;
using Shared.Core.Log;
using Shared.Core.Network;
using Shared.Common.Protos;

namespace Game.Game.Mail;

/// <summary>
/// 查询邮件列表 Handler
/// </summary>
public class QueryMailListHandler : MessageHandler<QueryMailListReq>
{
    private static readonly ILog Log = LogManager.GetLogger<QueryMailListHandler>();

    protected override async Task<IMessage?> ExecuteAsync(TcpSession session, QueryMailListReq req)
    {
        var res = new QueryMailListAck();

        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = MailErrorCode.InvalidParam;
            return res;
        }

        var mailList = await MailManager.Instance.LoadMailsAsync(player.RoleId);
        var mails = MailManager.Instance.GetMailList(mailList);

        res.Code = MailErrorCode.Ok;
        res.Mails = mails.Select(MailManager.Instance.ToMailInfo).ToList();

        return res;
    }
}

/// <summary>
/// 标记邮件已读 Handler
/// </summary>
public class MailReadHandler : MessageHandler<MailReadReq>
{
    protected override async Task<IMessage?> ExecuteAsync(TcpSession session, MailReadReq req)
    {
        var res = new MailReadAck();

        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = MailErrorCode.InvalidParam;
            return res;
        }

        var mailList = await MailManager.Instance.LoadMailsAsync(player.RoleId);
        var result = MailManager.Instance.MarkRead(mailList, req.MailIds);

        if (result.Changed)
        {
            await MailManager.Instance.SaveMailsAsync(mailList);
        }

        res.Code = MailErrorCode.Ok;
        res.ReadMailIds = result.ReadIds;

        return res;
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
        var res = new MailGetItemAck();

        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = MailErrorCode.InvalidParam;
            return res;
        }

        var mailList = await MailManager.Instance.LoadMailsAsync(player.RoleId);
        var result = await MailManager.Instance.ClaimAttachmentsAsync(mailList, req.MailIds);

        res.Code = MailErrorCode.Ok;
        res.ClaimedMailIds = result.ClaimedIds;
        res.Items = result.Items.Select(a => new MailAttachmentInfo
        {
            ConfigId = a.ConfigId,
            Count = a.Count,
        }).ToList();

        if (result.Success)
        {
            Log.Info($"领取邮件附件 RoleId:{session.RoleId} Count:{result.Items.Count}");
        }

        return res;
    }
}

/// <summary>
/// 删除邮件 Handler
/// </summary>
public class MailDeleteHandler : MessageHandler<MailDeleteReq>
{
    protected override async Task<IMessage?> ExecuteAsync(TcpSession session, MailDeleteReq req)
    {
        var res = new MailDeleteAck();

        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = MailErrorCode.InvalidParam;
            return res;
        }

        var mailList = await MailManager.Instance.LoadMailsAsync(player.RoleId);
        var result = MailManager.Instance.DeleteMails(mailList, req.MailIds);

        if (result.Changed)
        {
            await MailManager.Instance.SaveMailsAsync(mailList);
        }

        res.Code = MailErrorCode.Ok;
        res.DeletedMailIds = result.DeletedIds;

        return res;
    }
}
