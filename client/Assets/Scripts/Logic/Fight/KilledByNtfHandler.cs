using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class KilledByNtfHandler : NtfMessageHandler<KilledByNtf>
    {
        protected override Task ExecuteAsync(TcpSession session, KilledByNtf ntf)
        {
            FightController.Instance.HandleKilledByNtf(ntf);
            return Task.CompletedTask;
        }
    }
}
