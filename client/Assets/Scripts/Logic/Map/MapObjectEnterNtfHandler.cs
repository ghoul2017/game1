using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class MapObjectEnterNtfHandler : NtfMessageHandler<MapObjectEnterNtf>
    {
        protected override Task ExecuteAsync(TcpSession session, MapObjectEnterNtf ntf)
        {
            MapController.Instance.HandleMapObjectEnterNtf(ntf);
            return Task.CompletedTask;
        }
    }
}
