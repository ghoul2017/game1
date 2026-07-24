using System.Linq;
using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.Mail;

public sealed class MailReadReqHandler : PlayerRpcMessageHandler<MailReadReq, MailReadAck>
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(MailReadReqHandler));

    protected override async Task ExecuteAsync(PlayerEntity player, MailReadReq req, MailReadAck res)
    {
        var mailComponent = player.GetComponent<RoleMailComponent>();

        foreach (var mailId in req.MailIds.Distinct())
        {
            if (!mailComponent.Mails.TryGetValue(mailId, out var mail))
                continue;
            if (mail.IsRead)
                continue;

            mail.State = 1;
            res.ReadMailIds.Add(mailId);
        }

        if (res.ReadMailIds.Count > 0)
            mailComponent.MarkDirty();

        Log.Info($"读取邮件 RoleId:{player.Id} MailCount:{res.ReadMailIds.Count}");
        await Task.CompletedTask;
    }
}
