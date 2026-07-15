using System.Linq;
using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.Mail;

public sealed class MailGetItemReqHandler : PlayerRpcMessageHandler<MailGetItemReq, MailGetItemAck>
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(MailGetItemReqHandler));

    protected override async Task ExecuteAsync(PlayerEntity player, MailGetItemReq req, MailGetItemAck res)
    {
        var result = player.RoleMail.ClaimAttachments(req.MailIds);

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
        await Task.CompletedTask;
    }
}
