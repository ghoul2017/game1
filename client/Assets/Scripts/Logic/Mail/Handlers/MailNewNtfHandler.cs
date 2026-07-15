using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Event;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class MailNewNtfHandler : NtfMessageHandler<MailNewNtf>
    {
        protected override async Task ExecuteAsync(TcpSession session, MailNewNtf ntf)
        {
            if (ntf.Mail != null)
            {
                MailController.Instance.Mails.Insert(0, ntf.Mail);
                EventBus.Instance.Publish(new EventType.MailDataChangedEvent());
                ClientUiEventPublisher.ShowToast("收到新邮件: " + ntf.Mail.Title);
            }

            await Task.CompletedTask;
        }
    }
}
