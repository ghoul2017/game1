using System.Linq;
using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Components.Mail;

public sealed class QueryMailListReqHandler : PlayerRpcMessageHandler<QueryMailListReq, QueryMailListAck>
{
    protected override Task ExecuteAsync(PlayerEntity player, QueryMailListReq req, QueryMailListAck res)
    {
        var mails = player.RoleMail.GetMailList();
        res.Mails = mails.Select(player.RoleMail.ToMailInfo).ToList();
        return Task.CompletedTask;
    }
}
