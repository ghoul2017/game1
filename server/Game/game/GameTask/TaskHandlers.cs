using System.Linq;
using System.Threading.Tasks;
using Game.Core.Network;
using Game.Game.Player;
using Game.Network;
using Shared.Core.Log;
using Shared.Core.Network;
using Shared.Common.Protos;

namespace Game.Game.GameTask;

public static class TaskHandlers
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(TaskHandlers));

    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> QueryTaskListHandler(TcpSession session, QueryTaskListReq req, QueryTaskListAck res)
    {
        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = TaskErrorCode.InvalidParam;
            return res;
        }

        var taskList = await TaskManager.Instance.LoadTasksAsync(player.Id);
        var tasks = TaskManager.Instance.GetTaskList(taskList);

        res.Code = TaskErrorCode.Ok;
        res.Tasks = tasks.Select(TaskManager.Instance.ToTaskInfo).ToList();

        return res;
    }

    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> TaskAcceptHandler(TcpSession session, TaskAcceptReq req, TaskAcceptAck res)
    {
        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = TaskErrorCode.InvalidParam;
            return res;
        }

        var taskList = await TaskManager.Instance.LoadTasksAsync(player.Id);
        var result = TaskManager.Instance.AcceptTask(taskList, req.TaskId);

        if (result.Success)
        {
            await TaskManager.Instance.SaveTasksAsync(taskList);
            var task = taskList.GetByTaskId(req.TaskId);
            res.Task = TaskManager.Instance.ToTaskInfo(task!);
            Log.Info($"接取任务 RoleId:{session.RoleId} TaskId:{req.TaskId}");
        }

        res.Code = result.Success ? TaskErrorCode.Ok : result.ErrorCode;

        return res;
    }

    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> TaskSubmitHandler(TcpSession session, TaskSubmitReq req, TaskSubmitAck res)
    {
        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = TaskErrorCode.InvalidParam;
            return res;
        }

        var taskList = await TaskManager.Instance.LoadTasksAsync(player.Id);
        var result = await TaskManager.Instance.SubmitTaskAsync(taskList, session.RoleId, req.TaskId);

        if (result.Success)
        {
            await TaskManager.Instance.SaveTasksAsync(taskList);
            Log.Info($"提交任务领奖 RoleId:{session.RoleId} TaskId:{req.TaskId}");
        }

        res.Code = result.Success ? TaskErrorCode.Ok : result.ErrorCode;
        res.TaskId = req.TaskId;

        return res;
    }

    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> TaskGiveUpHandler(TcpSession session, TaskGiveUpReq req, TaskGiveUpAck res)
    {
        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = TaskErrorCode.InvalidParam;
            return res;
        }

        var taskList = await TaskManager.Instance.LoadTasksAsync(player.Id);
        var result = TaskManager.Instance.GiveUpTask(taskList, req.TaskId);

        if (result.Success)
        {
            await TaskManager.Instance.SaveTasksAsync(taskList);
            Log.Info($"放弃任务 RoleId:{session.RoleId} TaskId:{req.TaskId}");
        }

        res.Code = result.Success ? TaskErrorCode.Ok : result.ErrorCode;
        res.TaskId = req.TaskId;

        return res;
    }
}
