using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Event;

namespace Game.Components.GameTask;

public sealed class MonsterKilledTaskProgressEventHandler : ISyncEventHandler<EventType.MonsterKilledEvent>
{
    public void Handle(EventType.MonsterKilledEvent e)
    {
        var player = Context.Players.GetByRoleId(e.KillerId);
        if (player == null)
        {
            return;
        }

        var taskComponent = player.GetComponent<RoleTaskComponent>();
        var changed = taskComponent.UpdateProgress(TaskGoalType.KillMonster, e.MonsterCfgId, 1);
        if (changed.Count > 0)
        {
            taskComponent.NotifyTaskChanged(e.KillerId, changed);
        }
    }
}
