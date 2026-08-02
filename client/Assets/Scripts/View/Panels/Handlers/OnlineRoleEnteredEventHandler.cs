using Client.Logic;
using Client.View.OnlineTechnical;
using Shared.Core.Event;
using EventType = Shared.Core.Event.EventType;

namespace Client.View
{
    /// <summary>
    /// 在线远征入口在角色身份就绪后留在空技术场景，清掉旧 RPG 面板并挂载新玩法宿主。
    /// </summary>
    public sealed class OnlineRoleEnteredEventHandler : ISyncEventHandler<EventType.RoleEnteredEvent>
    {
        public void Handle(EventType.RoleEnteredEvent e)
        {
            if (!ClientRuntimeMode.UsesOnlineExpeditionEntry || !OnlineTechnicalEntry.TryEnterGameplay(e.RoleId))
            {
                return;
            }

            UIManager manager = UIManager.Instance;
            manager?.ClearAll();
        }
    }
}
