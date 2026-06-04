using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class MapPlayerLeaveNtfHandler : NtfMessageHandler<MapPlayerLeaveNtf>
    {
        protected override Task ExecuteAsync(TcpSession session, MapPlayerLeaveNtf ntf)
        {
            MapController.Instance.HandleMapPlayerLeaveNtf(ntf);
            return Task.CompletedTask;
        }
    }
}
