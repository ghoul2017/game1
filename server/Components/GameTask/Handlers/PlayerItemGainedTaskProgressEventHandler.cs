using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Event;

namespace Game.Components.GameTask;

public sealed class PlayerItemGainedTaskProgressEventHandler : ISyncEventHandler<EventType.PlayerItemGainedEvent>
{
    public void Handle(EventType.PlayerItemGainedEvent e)
    {
        var player = Context.Players.GetByRoleId(e.RoleId);
        if (player == null)
        {
            return;
        }

        var taskComponent = player.GetComponent<RoleTaskComponent>();
        var changed = taskComponent.UpdateProgress(TaskGoalType.CollectItem, e.ConfigId, e.Count);
        if (changed.Count > 0)
        {
            taskComponent.NotifyTaskChanged(e.RoleId, changed);
        }
    }
}
