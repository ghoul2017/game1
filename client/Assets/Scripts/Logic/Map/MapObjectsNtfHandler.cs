using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class EnterMapNtfHandler : NtfMessageHandler<EnterMapNtf>
    {
        protected override Task ExecuteAsync(TcpSession session, EnterMapNtf ntf)
        {
            MapController.Instance.HandleEnterMapNtf(ntf);
            return Task.CompletedTask;
        }
    }

    public sealed class MapObjectsNtfHandler : NtfMessageHandler<MapObjectsNtf>
    {
        protected override Task ExecuteAsync(TcpSession session, MapObjectsNtf ntf)
        {
            MapController.Instance.HandleMapObjectsNtf(ntf);
            return Task.CompletedTask;
        }
    }
}
