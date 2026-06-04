using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Components.Mail;

public sealed class MailReadReqHandler : PlayerRpcMessageHandler<MailReadReq, MailReadAck>
{
    protected override async Task ExecuteAsync(PlayerEntity player, MailReadReq req, MailReadAck res)
    {
        var result = player.RoleMail.MarkRead(req.MailIds);

        if (result.Changed)
        {
            await player.RoleMail.SaveMailsAsync();
        }
        res.ReadMailIds = result.ReadIds;
    }
}
