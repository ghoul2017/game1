using Shared.Core.Event;

namespace Client.Logic
{
    public sealed class NetReconnectedMapEventHandler : ISyncEventHandler<EventType.NetReconnectedEvent>
    {
        public void Handle(EventType.NetReconnectedEvent e)
        {
            _ = ReenterCurrentMap();
        }

        private static async System.Threading.Tasks.Task ReenterCurrentMap()
        {
            var mapData = GameData.CurrentMap;
            if (mapData != null)
            {
                await MapController.Instance.EnterMap(mapData.MapCfgId, mapData.Line);
            }

            EventBus.Instance.Publish(new EventType.MapReconnectedEvent());
        }
    }
}
