using Shared.Core.Event;

namespace Client.Logic
{
    public sealed class NetReconnectedMapEventHandler : ISyncEventHandler<EventType.NetReconnectedEvent>
    {
        public void Handle(EventType.NetReconnectedEvent e)
        {
            if (ClientRuntimeMode.UsesOnlineExpeditionEntry)
            {
                // 重连 EnterGameReq 会再次产生 EnterGameNtf，由唯一角色入口恢复远征。
                return;
            }

            _ = ReenterCurrentMap();
        }

        private static async System.Threading.Tasks.Task ReenterCurrentMap()
        {
            EnterMapData mapData = GameData.CurrentMap;
            if (mapData != null)
            {
                await MapController.Instance.EnterMap(mapData.MapCfgId, mapData.Line);
            }

            EventBus.Instance.Publish(new EventType.MapReconnectedEvent());
        }
    }
}
