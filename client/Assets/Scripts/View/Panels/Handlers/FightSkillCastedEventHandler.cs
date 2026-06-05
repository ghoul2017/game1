using Shared.Core.Event;
using Client.Logic;
using UnityEngine;
using EventType = Shared.Core.Event.EventType;

namespace Client.View
{
    public sealed class FightSkillCastedEventHandler : ISyncEventHandler<EventType.FightSkillCastedEvent>
    {
        public void Handle(EventType.FightSkillCastedEvent e)
        {
            var panel = UIManager.Instance != null ? UIManager.Instance.GetPanel<MainPanel>() : null;
            if (panel == null || !panel.IsShown || e.Ack == null) return;

            var ack = e.Ack;
            if (!string.IsNullOrEmpty(ack.ErrorMessage))
            {
                UIToast.Show(ack.ErrorMessage);
                return;
            }

            long selfId = GameData.CurrentRole != null ? GameData.CurrentRole.RoleId : 0;

            if (ack.AttackerId != selfId)
            {
                OtherPlayerManager.Instance.PlayAttack(ack.AttackerId, ack.SkillId);
            }

            foreach (var target in ack.Targets)
            {
                if (target.TargetId == selfId)
                    continue;

                OtherPlayerManager.Instance.ShowDamage(
                    target.TargetId,
                    -target.DeltaHp,
                    target.IsCritical);
            }

            Debug.Log(string.Format("[MainPanel] 技能释放成功 Attacker={0} Skill={1} Target={2} Hits={3}",
                ack.AttackerId, ack.SkillId, ack.MainTargetId, ack.Targets.Count));
        }
    }
}
