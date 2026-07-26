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
        var mailComponent = player.GetComponent<RoleMailComponent>();
        var bagComponent = player.GetComponent<RoleBagComponent>();

        var attachmentCount = 0;

        foreach (var mailId in req.MailIds.Distinct())
        {
            if (!mailComponent.Mails.TryGetValue(mailId, out var mail))
                continue;
            if (!mail.HasUnclaimedAttachment)
                continue;

            var addItems = mail.Attachments.Select(item => new BagItemAddData
            {
                ConfigId = item.ConfigId,
                Count = item.Count,
                Bind = false,
                ExpireTime = 0,
            }).ToArray();
            if (!RoleBagHelper.TryValidateAddItems(addItems, out var errorMessage))
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

            bagComponent.AddItems(addItems);

            attachmentCount += mail.Attachments.Count;
        }

        Log.Info($"领取邮件附件 RoleId:{player.Id} MailCount:{res.ClaimedMailIds.Count} AttachmentCount:{attachmentCount}");

        await Task.CompletedTask;
    }
}
