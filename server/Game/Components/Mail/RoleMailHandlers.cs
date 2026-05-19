using Game.Components.Player;
using System.Linq;
using System.Threading.Tasks;
using Game.Database;
using Shared.Core.Event;
using Shared.Core.Log;
using Shared.Core.Network;
using Shared.Common.Protos;

namespace Game.Components.Mail;

public static class RoleMailHandlers
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(RoleMailHandlers));

    [MessageHandler(typeof(PlayerRpcMethodHandler))]
    public static async Task<IMessage> QueryMailListHandler(PlayerEntity player, QueryMailListReq req, QueryMailListAck res)
    {
        await Task.CompletedTask;
        var mails = player.RoleMail.GetMailList();

        res.Mails = mails.Select(player.RoleMail.ToMailInfo).ToList();

        return res;
    }

    [MessageHandler(typeof(PlayerRpcMethodHandler))]
    public static async Task<IMessage> MailReadHandler(PlayerEntity player, MailReadReq req, MailReadAck res)
    {
        var result = player.RoleMail.MarkRead(req.MailIds);

        if (result.Changed)
        {
            await player.RoleMail.SaveMailsAsync();
        }
        res.ReadMailIds = result.ReadIds;

        return res;
    }

    [MessageHandler(typeof(PlayerRpcMethodHandler))]
    public static async Task<IMessage> MailGetItemHandler(PlayerEntity player, MailGetItemReq req, MailGetItemAck res)
    {
        var result = await player.RoleMail.ClaimAttachmentsAsync(req.MailIds);

        res.ClaimedMailIds = result.ClaimedIds;
        res.Items = result.Items.Select(a => new MailAttachmentInfo
        {
            ConfigId = a.ConfigId,
            Count = a.Count,
        }).ToList();

        if (result.Success)
        {
            Log.Info($"领取邮件附件 RoleId:{player.Id} Count:{result.Items.Count}");
        }

        return res;
    }

    [MessageHandler(typeof(PlayerRpcMethodHandler))]
    public static async Task<IMessage> MailDeleteHandler(PlayerEntity player, MailDeleteReq req, MailDeleteAck res)
    {
        var result = player.RoleMail.DeleteMails(req.MailIds);

        if (result.Changed)
        {
            await player.RoleMail.SaveMailsAsync();
        }

        res.DeletedMailIds = result.DeletedIds;

        return res;
    }

    #region 事件订阅

    [EventSubscriber]
    public static async Task OnPlayerOnline(EventType.PlayerOnlineEvent e)
    {
        var player = e.Player;

        var mail = await MongoDb.Instance.LoadAsync<RoleMailComponent>(player.Id);
        if (mail == null)
        {
            mail = new RoleMailComponent { Id = player.Id };
            await MongoDb.Instance.InsertAsync(mail);
            Log.Info($"创建新邮件 RoleId:{player.Id}");
        }
        player.AddComponent(mail);
    }

    #endregion
}
