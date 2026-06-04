using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class EnterGameNtfHandler : NtfMessageHandler<EnterGameNtf>
    {
        protected override Task ExecuteAsync(TcpSession session, EnterGameNtf ntf)
        {
            RoleSelectController.Instance.HandleEnterGameNtf(ntf);
            return Task.CompletedTask;
        }
    }
}
