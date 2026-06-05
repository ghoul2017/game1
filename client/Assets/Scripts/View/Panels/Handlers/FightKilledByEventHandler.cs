using Shared.Core.Event;
using Client.Logic;

namespace Client.View
{
    public sealed class FightKilledByEventHandler : ISyncEventHandler<EventType.FightKilledByEvent>
    {
        public void Handle(EventType.FightKilledByEvent e)
        {
            var panel = UIManager.Instance != null ? UIManager.Instance.GetPanel<MainPanel>() : null;
            if (panel == null || !panel.IsShown || e.Notification == null) return;

            var ntf = e.Notification;
            string killerDesc = ntf.IsMonster ? "怪物" : ntf.KillerName;
            UIMessageBox.Show("阵亡", string.Format("你被 {0} 击杀了！", killerDesc),
                () =>
                {
                    _ = FightController.Instance.RespawnAtCurrentMap();
                }, showCancel: false, confirmText: "回城复活");
        }
    }
}
