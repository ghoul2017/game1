using System.Linq;
using System.Threading.Tasks;
using Shared.Common.Protos;
using Server.Core.Log;
using Server.Game.Player;
using Server.Network;
using Shared.Core.Network;

namespace Server.Game.GameTask;

/// <summary>
/// 查询任务列表 Handler
/// </summary>
public class QueryTaskListHandler : MessageHandler<QueryTaskListReq>
{
    protected override async Task<IMessage?> ExecuteAsync(TcpSession session, QueryTaskListReq req)
    {
        var resp = new QueryTaskListAck();

        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            resp.Code = TaskErrorCode.InvalidParam;
            return resp;
        }

        var taskList = await TaskManager.Instance.LoadTasksAsync(player.RoleId);
        var tasks = TaskManager.Instance.GetTaskList(taskList);

        resp.Code = TaskErrorCode.Ok;
        resp.Tasks = tasks.Select(TaskManager.Instance.ToTaskInfo).ToList();

        return resp;
    }
}

/// <summary>
/// 接取任务 Handler
/// </summary>
public class TaskAcceptHandler : MessageHandler<TaskAcceptReq>
{
    private static readonly ILog Log = LogManager.GetLogger<TaskAcceptHandler>();

    protected override async Task<IMessage?> ExecuteAsync(TcpSession session, TaskAcceptReq req)
    {
        var resp = new TaskAcceptAck();

        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            resp.Code = TaskErrorCode.InvalidParam;
            return resp;
        }

        var taskList = await TaskManager.Instance.LoadTasksAsync(player.RoleId);
        var result = TaskManager.Instance.AcceptTask(taskList, req.TaskId);

        if (result.Success)
        {
            await TaskManager.Instance.SaveTasksAsync(taskList);
            var task = taskList.GetByTaskId(req.TaskId);
            resp.Task = TaskManager.Instance.ToTaskInfo(task!);
            Log.Info($"接取任务 RoleId:{session.RoleId} TaskId:{req.TaskId}");
        }

        resp.Code = result.Success ? TaskErrorCode.Ok : result.ErrorCode;

        return resp;
    }
}

/// <summary>
/// 提交任务（领奖）Handler
/// </summary>
public class TaskSubmitHandler : MessageHandler<TaskSubmitReq>
{
    private static readonly ILog Log = LogManager.GetLogger<TaskSubmitHandler>();

    protected override async Task<IMessage?> ExecuteAsync(TcpSession session, TaskSubmitReq req)
    {
        var resp = new TaskSubmitAck();

        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            resp.Code = TaskErrorCode.InvalidParam;
            return resp;
        }

        var taskList = await TaskManager.Instance.LoadTasksAsync(player.RoleId);
        var result = await TaskManager.Instance.SubmitTaskAsync(taskList, session.RoleId, req.TaskId);

        if (result.Success)
        {
            await TaskManager.Instance.SaveTasksAsync(taskList);
            Log.Info($"提交任务领奖 RoleId:{session.RoleId} TaskId:{req.TaskId}");
        }

        resp.Code = result.Success ? TaskErrorCode.Ok : result.ErrorCode;
        resp.TaskId = req.TaskId;

        return resp;
    }
}

/// <summary>
/// 放弃任务 Handler
/// </summary>
public class TaskGiveUpHandler : MessageHandler<TaskGiveUpReq>
{
    private static readonly ILog Log = LogManager.GetLogger<TaskGiveUpHandler>();

    protected override async Task<IMessage?> ExecuteAsync(TcpSession session, TaskGiveUpReq req)
    {
        var resp = new TaskGiveUpAck();

        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            resp.Code = TaskErrorCode.InvalidParam;
            return resp;
        }

        var taskList = await TaskManager.Instance.LoadTasksAsync(player.RoleId);
        var result = TaskManager.Instance.GiveUpTask(taskList, req.TaskId);

        if (result.Success)
        {
            await TaskManager.Instance.SaveTasksAsync(taskList);
            Log.Info($"放弃任务 RoleId:{session.RoleId} TaskId:{req.TaskId}");
        }

        resp.Code = result.Success ? TaskErrorCode.Ok : result.ErrorCode;
        resp.TaskId = req.TaskId;

        return resp;
    }
}
