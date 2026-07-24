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
        RoleMailComponent? mailComponent = player.GetComponent<RoleMailComponent>();
        if (mailComponent == null)
        {
            res.ErrorMessage = "邮件数据未初始化";
            Log.Error($"邮件组件未初始化 RoleId:{player.Id}");
            return;
        }

        RoleBagComponent? bagComponent = player.GetComponent<RoleBagComponent>();
        if (bagComponent == null)
        {
            res.ErrorMessage = "背包数据未初始化";
            Log.Error($"背包组件未初始化 RoleId:{player.Id}");
            return;
        }

        var attachmentCount = 0;

        foreach (var mailId in req.MailIds.Distinct())
        {
            if (!mailComponent.Mails.TryGetValue(mailId, out var mail))
                continue;
            if (!mail.HasUnclaimedAttachment)
                continue;

            var configIds = mail.Attachments.Select(item => item.ConfigId).ToArray();
            var counts = mail.Attachments.Select(item => item.Count).ToArray();
            if (!bagComponent.CanAddItems(configIds, counts, out var errorMessage))
            {
                res.ErrorMessage = errorMessage;
                break;
            }

            mail.State = 2;
            res.ClaimedMailIds.Add(mail.Id);
            mailComponent.MarkDirty();

            var addError = bagComponent.AddItems(configIds, counts);
            if (!string.IsNullOrEmpty(addError))
            {
                var message = $"附件预检通过后添加失败 RoleId:{player.Id} {addError}";
                Log.Error(message);
                throw new InvalidOperationException(message);
            }

            attachmentCount += mail.Attachments.Count;
        }

        Log.Info($"领取邮件附件 RoleId:{player.Id} MailCount:{res.ClaimedMailIds.Count} AttachmentCount:{attachmentCount}");

        await Task.CompletedTask;
    }
}
