using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class MapPlayerMoveNtfHandler : NtfMessageHandler<MapPlayerMoveNtf>
    {
        protected override Task ExecuteAsync(TcpSession session, MapPlayerMoveNtf ntf)
        {
            MapController.Instance.HandleMapPlayerMoveNtf(ntf);
            return Task.CompletedTask;
        }
    }
}
