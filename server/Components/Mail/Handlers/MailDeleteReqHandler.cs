using System.Linq;
using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.Mail;

public sealed class MailDeleteReqHandler : PlayerRpcMessageHandler<MailDeleteReq, MailDeleteAck>
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(MailDeleteReqHandler));

    protected override async Task ExecuteAsync(PlayerEntity player, MailDeleteReq req, MailDeleteAck res)
    {
        var mailComponent = player.GetComponent<RoleMailComponent>();

        foreach (var mailId in req.MailIds.Distinct())
        {
            if (!mailComponent.Mails.TryGetValue(mailId, out var mail))
                continue;
            if (mail.HasUnclaimedAttachment)
                continue;

            mailComponent.Mails.Remove(mailId);
            res.DeletedMailIds.Add(mailId);
        }

        if (res.DeletedMailIds.Count > 0)
            mailComponent.MarkDirty();

        Log.Info($"删除邮件 RoleId:{player.Id} MailCount:{res.DeletedMailIds.Count}");
        await Task.CompletedTask;
    }
}
