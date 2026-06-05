using System.Threading.Tasks;
using System.Collections.Generic;
using Shared.Common.Protos;
using Shared.Core.Event;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class MapObjectsNtfHandler : NtfMessageHandler<MapObjectsNtf>
    {
        protected override Task ExecuteAsync(TcpSession session, MapObjectsNtf ntf)
        {
            var objects = ntf.Objects ?? new List<MapObjectInfo>();
            var removeObjectIds = ntf.RemoveObjectIds ?? new List<long>();
            if (objects.Count == 0 && removeObjectIds.Count == 0)
                return Task.CompletedTask;

            var controller = MapController.Instance;
            if (!controller.MapViewReady)
            {
                controller.PendingObjects.AddRange(objects);
                controller.PendingRemoveObjectIds.AddRange(removeObjectIds);
                return Task.CompletedTask;
            }

            EventBus.Instance.Publish(new EventType.MapObjectsChangedEvent { Objects = objects, RemoveObjectIds = removeObjectIds });
            return Task.CompletedTask;
        }
    }
}
