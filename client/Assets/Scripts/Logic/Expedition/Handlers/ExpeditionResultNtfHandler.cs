using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class ExpeditionResultNtfHandler : NtfMessageHandler<ExpeditionResultNtf>
    {
        protected override async Task ExecuteAsync(TcpSession session, ExpeditionResultNtf notification)
        {
            ExpeditionController.Instance.ApplyResult(notification);
            await Task.CompletedTask;
        }
    }
}
