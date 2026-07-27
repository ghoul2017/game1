using System.Linq;
using System.Threading.Tasks;
using Game.Components.Bag;
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
        RoleMailComponent mailComponent = player.GetComponent<RoleMailComponent>();
        RoleBagComponent bagComponent = player.GetComponent<RoleBagComponent>();

        int attachmentCount = 0;

        foreach (var mailId in req.MailIds.Distinct())
        {
            if (!mailComponent.Mails.TryGetValue(mailId, out RoleMail? mail))
                continue;
            if (!mail.HasUnclaimedAttachment)
                continue;

            if (!RoleBagHelper.TryValidateNormalizedItems(mail.Attachments, out string errorMessage))
            {
                res.ErrorMessage = errorMessage;
                break;
            }

            if (bagComponent.IsFull())
            {
                res.ErrorMessage = "背包已满";
                break;
            }

            mail.State = 2;
            res.ClaimedMailIds.Add(mail.Id);
            mailComponent.MarkDirty();

            bagComponent.AddItems(mail.Attachments);

            attachmentCount += mail.Attachments.Count;
        }

        Log.Info($"[邮件] 领取邮件附件 RoleId:{player.Id} MailCount:{res.ClaimedMailIds.Count} AttachmentCount:{attachmentCount}");

        await Task.CompletedTask;
    }
}
