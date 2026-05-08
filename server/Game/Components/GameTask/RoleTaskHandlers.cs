using Game.Components.Player;
using System.Linq;
using System.Threading.Tasks;
using Game.Core.Network;
using Game.Network;
using Shared.Core.Event;
using Shared.Core.Log;
using Shared.Core.Network;
using Shared.Common.Protos;

namespace Game.Components.GameTask;

public static class RoleTaskHandlers
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(RoleTaskHandlers));

    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> QueryTaskListHandler(TcpSession session, QueryTaskListReq req, QueryTaskListAck res)
    {
        var player = Context.Players.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = TaskErrorCode.InvalidParam;
            return res;
        }

        var taskComp = await player.RoleTask.LoadTasksAsync(player.Id);
        var tasks = player.RoleTask.GetTaskList();

        res.Code = TaskErrorCode.Ok;
        res.Tasks = tasks.Select(player.RoleTask.ToTaskInfo).ToList();

        return res;
    }

    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> TaskAcceptHandler(TcpSession session, TaskAcceptReq req, TaskAcceptAck res)
    {
        var player = Context.Players.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = TaskErrorCode.InvalidParam;
            return res;
        }

        var taskComp = await player.RoleTask.LoadTasksAsync(player.Id);
        var result = player.RoleTask.AcceptTask(req.TaskId);

        if (result.Success)
        {
            await player.RoleTask.SaveTasksAsync();
            var task = taskComp.GetByTaskId(req.TaskId);
            res.Task = player.RoleTask.ToTaskInfo(task!);
            Log.Info($"接取任务 RoleId:{session.RoleId} TaskId:{req.TaskId}");
        }

        res.Code = result.Success ? TaskErrorCode.Ok : result.ErrorCode;

        return res;
    }

    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> TaskSubmitHandler(TcpSession session, TaskSubmitReq req, TaskSubmitAck res)
    {
        var player = Context.Players.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = TaskErrorCode.InvalidParam;
            return res;
        }

        var taskComp = await player.RoleTask.LoadTasksAsync(player.Id);
        var result = await player.RoleTask.SubmitTaskAsync(session.RoleId, req.TaskId);

        if (result.Success)
        {
            await player.RoleTask.SaveTasksAsync();
            Log.Info($"提交任务领奖 RoleId:{session.RoleId} TaskId:{req.TaskId}");
        }

        res.Code = result.Success ? TaskErrorCode.Ok : result.ErrorCode;
        res.TaskId = req.TaskId;

        return res;
    }

    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> TaskGiveUpHandler(TcpSession session, TaskGiveUpReq req, TaskGiveUpAck res)
    {
        var player = Context.Players.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = TaskErrorCode.InvalidParam;
            return res;
        }

        var taskComp = await player.RoleTask.LoadTasksAsync(player.Id);
        var result = player.RoleTask.GiveUpTask(req.TaskId);

        if (result.Success)
        {
            await player.RoleTask.SaveTasksAsync();
            Log.Info($"放弃任务 RoleId:{session.RoleId} TaskId:{req.TaskId}");
        }

        res.Code = result.Success ? TaskErrorCode.Ok : result.ErrorCode;
        res.TaskId = req.TaskId;

        return res;
    }

    #region 事件订阅

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
