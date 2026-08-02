using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class ExpeditionSnapshotNtfHandler : NtfMessageHandler<ExpeditionSnapshotNtf>
    {
        protected override async Task ExecuteAsync(TcpSession session, ExpeditionSnapshotNtf notification)
        {
            ExpeditionController.Instance.ApplySnapshot(notification.Snapshot);
            await Task.CompletedTask;
        }
    }
}
