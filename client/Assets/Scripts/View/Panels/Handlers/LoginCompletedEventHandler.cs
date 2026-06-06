using Shared.Core.Event;
using Client.Logic;
using UnityEngine;
using EventType = Shared.Core.Event.EventType;

namespace Client.View
{
    public sealed class LoginCompletedEventHandler : ISyncEventHandler<EventType.LoginCompletedEvent>
    {
        public void Handle(EventType.LoginCompletedEvent e)
        {
            var panel = GetPanel();
            if (panel == null)
                return;

            if (e.Success)
            {
                Debug.Log("[LoginPanel] 登录成功 PlayerId=" + GameData.PlayerId + " 角色数=" + GameData.Roles.Count);
                UIManager.Instance.ReplacePanel<RoleSelectPanel>();
            }
            else
            {
                panel.SetErrorText(e.ErrorMessage);
                panel.SetButtonEnabled(true);
            }
        }

        private static LoginPanel GetPanel()
        {
            var panel = UIManager.Instance != null ? UIManager.Instance.GetPanel<LoginPanel>() : null;
            return panel != null && panel.IsShown ? panel : null;
        }
    }
}
