using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class MapObjectMoveNtfHandler : NtfMessageHandler<MapObjectMoveNtf>
    {
        protected override Task ExecuteAsync(TcpSession session, MapObjectMoveNtf ntf)
        {
            MapController.Instance.HandleMapObjectMoveNtf(ntf);
            return Task.CompletedTask;
        }
    }
}
