using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class MapObjectLeaveNtfHandler : NtfMessageHandler<MapObjectLeaveNtf>
    {
        protected override Task ExecuteAsync(TcpSession session, MapObjectLeaveNtf ntf)
        {
            MapController.Instance.HandleMapObjectLeaveNtf(ntf);
            return Task.CompletedTask;
        }
    }
}
