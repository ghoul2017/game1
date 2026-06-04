using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class MailNewNtfHandler : NtfMessageHandler<MailNewNtf>
    {
        protected override Task ExecuteAsync(TcpSession session, MailNewNtf ntf)
        {
            MailController.Instance.HandleMailNewNtf(ntf);
            return Task.CompletedTask;
        }
    }
}
