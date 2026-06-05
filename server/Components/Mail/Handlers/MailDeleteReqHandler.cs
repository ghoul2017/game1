using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Components.Mail;

public sealed class MailDeleteReqHandler : PlayerRpcMessageHandler<MailDeleteReq, MailDeleteAck>
{
    protected override async Task ExecuteAsync(PlayerEntity player, MailDeleteReq req, MailDeleteAck res)
    {
        var result = player.RoleMail.DeleteMails(req.MailIds);

        if (result.Changed)
        {
            await player.RoleMail.SaveMailsAsync();
        }

        res.DeletedMailIds = result.DeletedIds;
    }
}
