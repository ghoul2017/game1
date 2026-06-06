using Shared.Core.Event;
using UnityEngine;
using UnityEngine.SceneManagement;
using EventType = Shared.Core.Event.EventType;

namespace Client.View
{
    public sealed class MapReconnectFailedEventHandler : ISyncEventHandler<EventType.MapReconnectFailedEvent>
    {
        public void Handle(EventType.MapReconnectFailedEvent e)
        {
            if (GameSceneEntry.Instance == null)
                return;

            Debug.Log("[GameSceneEntry] 重连失败: " + e.Message + "，返回登录");
            OtherPlayerManager.Instance.ClearAll();
            SceneManager.LoadScene("SampleScene");
        }
    }
}
