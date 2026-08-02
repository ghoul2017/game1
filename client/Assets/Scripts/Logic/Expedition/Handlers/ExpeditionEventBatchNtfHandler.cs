using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class ExpeditionEventBatchNtfHandler : NtfMessageHandler<ExpeditionEventBatchNtf>
    {
        protected override async Task ExecuteAsync(TcpSession session, ExpeditionEventBatchNtf notification)
        {
            ExpeditionController.Instance.ApplyEventBatch(notification);
            await Task.CompletedTask;
        }
    }
}
