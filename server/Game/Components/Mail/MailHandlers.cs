using Game.Components.Mail;
using Game.Components.Player;
using System.Linq;
using System.Threading.Tasks;
using Game.Core.Network;
using Game.Network;
using Shared.Core.Log;
using Shared.Core.Network;
using Shared.Common.Protos;

namespace Game.Components.Mail;

public static class MailHandlers
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(MailHandlers));

    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> QueryMailListHandler(TcpSession session, QueryMailListReq req, QueryMailListAck res)
    {
        var player = Context.Players.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = MailErrorCode.InvalidParam;
            return res;
        }

        var mailList = await player.Mail.LoadMailsAsync(player.Id);
        var mails = player.Mail.GetMailList(mailList);

        res.Code = MailErrorCode.Ok;
        res.Mails = mails.Select(player.Mail.ToMailInfo).ToList();

        return res;
    }

    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> MailReadHandler(TcpSession session, MailReadReq req, MailReadAck res)
    {
        var player = Context.Players.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = MailErrorCode.InvalidParam;
            return res;
        }

        var mailList = await player.Mail.LoadMailsAsync(player.Id);
        var result = player.Mail.MarkRead(mailList, req.MailIds);

        if (result.Changed)
        {
            await player.Mail.SaveMailsAsync(mailList);
        }

        res.Code = MailErrorCode.Ok;
        res.ReadMailIds = result.ReadIds;

        return res;
    }

    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> MailGetItemHandler(TcpSession session, MailGetItemReq req, MailGetItemAck res)
    {
        var player = Context.Players.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = MailErrorCode.InvalidParam;
            return res;
        }

        var mailList = await player.Mail.LoadMailsAsync(player.Id);
        var result = await player.Mail.ClaimAttachmentsAsync(mailList, req.MailIds);

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

    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> MailDeleteHandler(TcpSession session, MailDeleteReq req, MailDeleteAck res)
    {
        var player = Context.Players.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = MailErrorCode.InvalidParam;
            return res;
        }

        var mailList = await player.Mail.LoadMailsAsync(player.Id);
        var result = player.Mail.DeleteMails(mailList, req.MailIds);

        if (result.Changed)
        {
            await player.Mail.SaveMailsAsync(mailList);
        }

        res.Code = MailErrorCode.Ok;
        res.DeletedMailIds = result.DeletedIds;

        return res;
    }
}
