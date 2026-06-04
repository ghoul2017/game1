using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class HpChangedNtfHandler : NtfMessageHandler<HpChangedNtf>
    {
        protected override Task ExecuteAsync(TcpSession session, HpChangedNtf ntf)
        {
            FightController.Instance.HandleHpChangedNtf(ntf);
            return Task.CompletedTask;
        }
    }
}