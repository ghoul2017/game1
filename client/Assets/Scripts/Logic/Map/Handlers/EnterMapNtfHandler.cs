using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Event;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class EnterMapNtfHandler : NtfMessageHandler<EnterMapNtf>
    {
        protected override Task ExecuteAsync(TcpSession session, EnterMapNtf ntf)
        {
            var controller = MapController.Instance;
            controller.PendingObjects.Clear();
            controller.PendingRemoveObjectIds.Clear();

            GameData.CurrentMap = new EnterMapData
            {
                MapCfgId = ntf.MapCfgId,
                Line = ntf.Line,
                SpawnX = ntf.X,
                SpawnY = ntf.Y
            };

            if (controller.MapViewReady)
                EventBus.Instance.Publish(new EventType.MapRebuildEvent { MapCfgId = ntf.MapCfgId, SpawnX = ntf.X, SpawnY = ntf.Y });

            EventBus.Instance.Publish(new EventType.MapEnterMapReadyEvent());
            return Task.CompletedTask;
        }
    }
}
