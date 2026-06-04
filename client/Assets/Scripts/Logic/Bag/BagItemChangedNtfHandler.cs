using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class BagItemChangedNtfHandler : NtfMessageHandler<BagItemChangedNtf>
    {
        protected override Task ExecuteAsync(TcpSession session, BagItemChangedNtf ntf)
        {
            BagController.Instance.HandleBagItemChangedNtf(ntf);
            return Task.CompletedTask;
        }
    }
}
