using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.GameTask;

public sealed class TaskGiveUpReqHandler : PlayerRpcMessageHandler<TaskGiveUpReq, TaskGiveUpAck>
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(TaskGiveUpReqHandler));

    protected override async Task ExecuteAsync(PlayerEntity player, TaskGiveUpReq req, TaskGiveUpAck res)
    {
        var taskComponent = player.GetComponent<RoleTaskComponent>();
        res.TaskId = req.TaskId;

        var task = taskComponent.GetByTaskId(req.TaskId);
        if (task == null)
        {
            res.ErrorMessage = "任务不存在";
            return;
        }

        if (!task.IsInProgress)
        {
            res.ErrorMessage = "任务已完成";
            return;
        }

        taskComponent.Tasks.Remove(task);
        taskComponent.MarkDirty();

        Log.Info($"放弃任务 RoleId:{player.Id} TaskId:{req.TaskId}");
        await Task.CompletedTask;
    }
}
