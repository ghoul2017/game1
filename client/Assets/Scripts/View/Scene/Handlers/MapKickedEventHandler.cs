using Client.Logic;
using Shared.Core.Event;
using UnityEngine;
using UnityEngine.SceneManagement;
using EventType = Shared.Core.Event.EventType;

namespace Client.View
{
    public sealed class MapKickedEventHandler : ISyncEventHandler<EventType.MapKickedEvent>
    {
        public void Handle(EventType.MapKickedEvent e)
        {
            if (GameSceneEntry.Instance == null) return;

            Debug.Log("[GameSceneEntry] 被踢下线，reason=" + e.Reason);
            string reasonText = MapController.GetKickReasonText(e.Reason);
            UIMessageBox.Show("下线通知", reasonText, () =>
            {
                OtherPlayerManager.Instance.ClearAll();
                SceneManager.LoadScene("SampleScene");
            }, showCancel: false, confirmText: "确定");
        }
    }
}
