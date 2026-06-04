using Game.Components.Player;
using System.Linq;
using System.Threading.Tasks;
using Game.Database;
using Shared.Core.Event;
using Shared.Core.Log;
using Shared.Core.Network;
using Shared.Common.Protos;

namespace Game.Components.GameTask;

public static class RoleTaskHandlers
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(RoleTaskHandlers));

    #region 事件订阅

    [EventSubscriber]
    public static async Task OnPlayerOnline(EventType.PlayerOnlineEvent e)
    {
        var player = e.Player;

        var task = await MongoDb.Instance.LoadAsync<RoleTaskComponent>(player.Id);
        if (task == null)
        {
            task = new RoleTaskComponent { Id = player.Id };
            await MongoDb.Instance.InsertAsync(task);
            Log.Info($"创建新任务列表 RoleId:{player.Id}");
        }
        player.AddComponent(task);
    }

    [EventSubscriber]
    public static void OnPlayerLevelUp(EventType.PlayerLevelUpEvent e)
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

    [EventSubscriber]
    public static void OnPlayerExpChanged(EventType.PlayerExpChangedEvent e)
    {
        // 经验变化作为通用进度事件，供 "累计获得 N 经验" 类任务消费
    }

    [EventSubscriber]
    public static void OnPlayerGoldChanged(EventType.PlayerGoldChangedEvent e)
    {
        // 金币变化供 "累计获得 N 金币" 类任务消费
    }

    [EventSubscriber]
    public static void OnPlayerItemGained(EventType.PlayerItemGainedEvent e)
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

    [EventSubscriber]
    public static void OnMonsterKilled(EventType.MonsterKilledEvent e)
    {
        var player = Context.Players.GetByRoleId(e.KillerId);
        if (player == null)
        {
            return;
        }

        var changed = player.RoleTask.UpdateProgress(TaskGoalType.KillMonster, e.MonsterCfgId, 1);
        if (changed.Count > 0)
        {
            player.RoleTask.NotifyTaskChanged(e.KillerId, changed);
        }
    }

    #endregion
}
