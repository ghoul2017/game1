using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Event;

namespace Game.Components.GameTask;

public sealed class PlayerLevelUpTaskProgressEventHandler : ISyncEventHandler<EventType.PlayerLevelUpEvent>
{
    public void Handle(EventType.PlayerLevelUpEvent e)
    {
        var player = Context.Players.GetByRoleId(e.RoleId);
        if (player == null)
        {
            return;
        }

        var changed = player.RoleTask.UpdateProgress(TaskGoalType.ReachLevel, e.NewLevel, 1);
        if (changed.Count > 0)
        {
            player.RoleTask.NotifyTaskChanged(e.RoleId, changed);
        }
    }
}
