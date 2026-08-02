using UnityEngine;
using UnityEngine.SceneManagement;
using Client.Logic;
using Client.View.OnlineTechnical;
using Shared.Core.Event;
using EventType = Shared.Core.Event.EventType;

namespace Client.View
{
    public sealed class MapEnterMapReadyEventHandler : ISyncEventHandler<EventType.MapEnterMapReadyEvent>
    {
        public void Handle(EventType.MapEnterMapReadyEvent e)
        {
            RoleSelectPanel panel = UIManager.Instance != null ? UIManager.Instance.GetPanel<RoleSelectPanel>() : null;
            if (panel == null || !panel.IsShown)
            {
                return;
            }

            Debug.Log("[RoleSelectPanel] 进图成功");
            UIManager.Instance.ClearAll();
            if (OnlineTechnicalEntry.TryEnterGameplay(GameData.CurrentRole?.RoleId ?? 0L))
            {
                return;
            }

            SceneManager.LoadScene("GameScene");
        }
    }
}
