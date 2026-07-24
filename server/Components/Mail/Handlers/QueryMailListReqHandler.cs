using System.Linq;
using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Components.Mail;

public sealed class QueryMailListReqHandler : PlayerRpcMessageHandler<QueryMailListReq, QueryMailListAck>
{
    protected override async Task ExecuteAsync(PlayerEntity player, QueryMailListReq req, QueryMailListAck res)
    {
        var mailComponent = player.GetComponent<RoleMailComponent>();

        res.Mails = mailComponent.Mails.Values
            .Where(mail => !mail.IsExpired)
            .OrderByDescending(mail => mail.SendTime)
            .Take(RoleMailSystem.MaxVisibleMailCount)
            .Select(mailComponent.ToMailInfo)
            .ToList();

        await Task.CompletedTask;
    }
}
