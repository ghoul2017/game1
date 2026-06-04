using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class KickNtfHandler : NtfMessageHandler<KickNtf>
    {
        protected override Task ExecuteAsync(TcpSession session, KickNtf ntf)
        {
            NetClient.Instance?.HandleKickNtf(ntf);
            return Task.CompletedTask;
        }
    }
}
