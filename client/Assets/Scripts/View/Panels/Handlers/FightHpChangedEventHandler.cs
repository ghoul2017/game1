using Shared.Core.Event;
using Client.Logic;
using Shared.Common.Protos;

namespace Client.View
{
    public sealed class FightHpChangedEventHandler : ISyncEventHandler<EventType.FightHpChangedEvent>
    {
        public void Handle(EventType.FightHpChangedEvent e)
        {
            var panel = UIManager.Instance != null ? UIManager.Instance.GetPanel<MainPanel>() : null;
            if (panel == null || !panel.IsShown || e.Notification == null)
                return;

            var ntf = e.Notification;
            long selfId = GameData.CurrentRole != null ? GameData.CurrentRole.RoleId : 0;

            if (ntf.ObjectId == selfId)
            {
                panel.UpdateHpDisplay();

                var pv = GameSceneEntry.PlayerController != null
                    ? GameSceneEntry.PlayerController.GetComponent<PlayerView>() : null;
                if (pv != null)
                    pv.UpdateHp(ntf.Hp, GameData.MaxHp);
            }
            else
            {
                var monsterView = MonsterEntityManager.Instance.GetView(ntf.ObjectId);
                if (monsterView != null)
                {
                    monsterView.UpdateHp(ntf.Hp, 100);

                    if (ntf.DeltaHp != 0)
                    {
                        monsterView.ShowDamageNumber(-ntf.DeltaHp, false);
                        if (ntf.DeltaHp < 0)
                            monsterView.PlayHitFlash();
                    }

                    if (ntf.Reason == HpChangeReason.Killed && ntf.Hp <= 0)
                        monsterView.PlayDeath();
                    else if (ntf.Reason == HpChangeReason.Revive)
                        monsterView.PlayRevive();
                }
                else
                {
                    OtherPlayerManager.Instance.UpdatePlayerHp(ntf.ObjectId, ntf.Hp, 100);

                    if (ntf.Reason == HpChangeReason.Killed && ntf.Hp <= 0)
                        OtherPlayerManager.Instance.PlayDeath(ntf.ObjectId);
                    else if (ntf.Reason == HpChangeReason.Revive)
                        OtherPlayerManager.Instance.PlayRevive(ntf.ObjectId);
                }
            }
        }
    }
}
