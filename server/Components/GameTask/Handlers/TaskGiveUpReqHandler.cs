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
        var result = player.RoleTask.GiveUpTask(req.TaskId);

        if (result.Success)
        {
            await player.RoleTask.SaveTasksAsync();
            Log.Info($"放弃任务 RoleId:{player.Id} TaskId:{req.TaskId}");
        }
        else
        {
            res.ErrorMessage = result.ErrorMessage;
        }

        res.TaskId = req.TaskId;
    }
}
