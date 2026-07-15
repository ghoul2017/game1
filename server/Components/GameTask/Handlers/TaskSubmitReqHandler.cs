using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.GameTask;

public sealed class TaskSubmitReqHandler : PlayerRpcMessageHandler<TaskSubmitReq, TaskSubmitAck>
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(TaskSubmitReqHandler));

    protected override async Task ExecuteAsync(PlayerEntity player, TaskSubmitReq req, TaskSubmitAck res)
    {
        var result = player.RoleTask.SubmitTask(player.Id, req.TaskId);

        if (result.Success)
        {
            res.Rewards = result.Rewards!;
            Log.Info($"提交任务领奖 RoleId:{player.Id} TaskId:{req.TaskId}");
        }
        else
        {
            res.ErrorMessage = result.ErrorMessage;
        }

        res.TaskId = req.TaskId;
        await Task.CompletedTask;
    }
}
