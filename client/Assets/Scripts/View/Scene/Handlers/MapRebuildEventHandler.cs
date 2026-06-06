using Shared.Common.Config;
using Shared.Core.Event;
using Shared.Core.Config;
using UnityEngine;
using EventType = Shared.Core.Event.EventType;

namespace Client.View
{
    public sealed class MapRebuildEventHandler : ISyncEventHandler<EventType.MapRebuildEvent>
    {
        public void Handle(EventType.MapRebuildEvent e)
        {
            if (GameSceneEntry.Instance == null)
                return;

            OtherPlayerManager.Instance.ClearAll();
            MonsterEntityManager.Instance.ClearAll();

            if (GameSceneEntry.MapGround != null)
            {
                Object.Destroy(GameSceneEntry.MapGround.gameObject);
                GameSceneEntry.MapGround = null;
            }

            var mapConfig = ConfigManager.Get<MapConfig>(e.MapCfgId);
            var mapObj = new GameObject("Map");
            var mapGround = mapObj.AddComponent<MapGround>();
            mapGround.Generate(mapConfig);
            GameSceneEntry.MapGround = mapGround;

            if (GameSceneEntry.PlayerController != null)
            {
                GameSceneEntry.PlayerController.Init(mapConfig, e.SpawnX, e.SpawnY);
            }

            Debug.Log(string.Format("[GameSceneEntry] 场景重建完成 Map={0} ({1}x{2})",
                mapConfig.Name, mapConfig.Width, mapConfig.Height));
        }
    }
}
