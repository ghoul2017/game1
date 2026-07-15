using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Event;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class MapObjectMoveNtfHandler : NtfMessageHandler<MapObjectMoveNtf>
    {
        protected override async Task ExecuteAsync(TcpSession session, MapObjectMoveNtf ntf)
        {
            EventBus.Instance.Publish(new EventType.MapObjectMoveEvent { Notification = ntf });
            await Task.CompletedTask;
        }
    }
}
