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

        var changed = player.RoleTask.UpdateProgress(TaskGoalType.CollectItem, e.ConfigId, e.Count);
        if (changed.Count > 0)
        {
            player.RoleTask.NotifyTaskChanged(e.RoleId, changed);
        }
    }
}
