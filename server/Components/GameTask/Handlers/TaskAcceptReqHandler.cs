using System;
using System.Linq;
using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Config;
using Shared.Common.Protos;
using Shared.Core.Config;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.GameTask;

public sealed class TaskAcceptReqHandler : PlayerRpcMessageHandler<TaskAcceptReq, TaskAcceptAck>
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(TaskAcceptReqHandler));
    private const int MaxActiveTaskCount = 20;

    protected override async Task ExecuteAsync(PlayerEntity player, TaskAcceptReq req, TaskAcceptAck res)
    {
        var taskComponent = player.GetComponent<RoleTaskComponent>();

        var config = ConfigManager.Get<TaskConfig>(req.TaskId);
        if (config == null)
        {
            res.ErrorMessage = "任务不存在";
            Log.Warn($"任务配置不存在 TaskId:{req.TaskId}");
            return;
        }

        if (taskComponent.GetByTaskId(req.TaskId) != null)
        {
            res.ErrorMessage = "任务已接取";
            return;
        }

        if (taskComponent.Tasks.Count(task => task.IsInProgress) >= MaxActiveTaskCount)
        {
            res.ErrorMessage = "任务列表已满";
            return;
        }

        var task = new RoleTask
        {
            TaskId = req.TaskId,
            State = 0,
            AcceptTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            CompleteTime = 0,
            Goals = RoleTaskHelper.CreateGoals(config),
        };

        taskComponent.Tasks.Add(task);
        taskComponent.MarkDirty();

        res.Task = RoleTaskHelper.ToInfo(task);
        Log.Info($"接取任务 RoleId:{player.Id} TaskId:{req.TaskId} GoalCount:{task.Goals.Count}");

        await Task.CompletedTask;
    }
}
