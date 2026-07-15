using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.GameTask;

public sealed class TaskAcceptReqHandler : PlayerRpcMessageHandler<TaskAcceptReq, TaskAcceptAck>
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(TaskAcceptReqHandler));

    protected override async Task ExecuteAsync(PlayerEntity player, TaskAcceptReq req, TaskAcceptAck res)
    {
        var result = player.RoleTask.AcceptTask(req.TaskId);

        if (result.Success)
        {
            var task = player.RoleTask.GetByTaskId(req.TaskId);
            res.Task = player.RoleTask.ToTaskInfo(task!);
            Log.Info($"接取任务 RoleId:{player.Id} TaskId:{req.TaskId}");
        }
        else
        {
            res.ErrorMessage = result.ErrorMessage;
        }

        await Task.CompletedTask;
    }
}
