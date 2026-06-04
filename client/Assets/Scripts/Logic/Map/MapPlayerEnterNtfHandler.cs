using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class MapPlayerEnterNtfHandler : NtfMessageHandler<MapPlayerEnterNtf>
    {
        protected override Task ExecuteAsync(TcpSession session, MapPlayerEnterNtf ntf)
        {
            MapController.Instance.HandleMapPlayerEnterNtf(ntf);
            return Task.CompletedTask;
        }
    }
}
