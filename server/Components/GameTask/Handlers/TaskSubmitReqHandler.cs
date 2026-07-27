using System;
using System.Linq;
using System.Threading.Tasks;
using Game.Components.Bag;
using Game.Components.Player;
using Shared.Common.Config;
using Shared.Common.Protos;
using Shared.Core.Config;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.GameTask;

public sealed class TaskSubmitReqHandler : PlayerRpcMessageHandler<TaskSubmitReq, TaskSubmitAck>
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(TaskSubmitReqHandler));

    protected override async Task ExecuteAsync(PlayerEntity player, TaskSubmitReq req, TaskSubmitAck res)
    {
        RoleTaskComponent taskComponent = player.GetComponent<RoleTaskComponent>();
        RoleBagComponent bagComponent = player.GetComponent<RoleBagComponent>();
        res.TaskId = req.TaskId;

        RoleTask? task = taskComponent.GetByTaskId(req.TaskId);
        if (task == null)
        {
            res.ErrorMessage = "任务不存在";
            return;
        }

        if (task.IsClaimed)
        {
            res.ErrorMessage = "任务已提交";
            return;
        }

        if (!task.IsCompleted)
        {
            res.ErrorMessage = "任务未完成";
            return;
        }

        TaskConfig? config = ConfigManager.Get<TaskConfig>(req.TaskId);
        if (config == null)
        {
            res.ErrorMessage = "任务不存在";
            Log.Warn($"任务配置不存在 TaskId:{req.TaskId}");
            return;
        }

        if (!BagItemAddDataGenerator.TryGenerate(config.RewardConfigs, config.RewardCounts, out List<BagItemAddData> normalizedItems, out string errorMessage))
        {
            res.ErrorMessage = errorMessage;
            return;
        }

        if (bagComponent.IsFull())
        {
            res.ErrorMessage = "背包已满";
            return;
        }

        task.State = 2;
        taskComponent.MarkDirty();

        List<BagItemAddData> gainedItems = bagComponent.AddItems(normalizedItems);
        res.Rewards = gainedItems.Select(item => new ItemTipsData
        {
            ConfigId = item.ConfigId,
            Count = item.Count,
        }).ToList();
        Log.Info($"[任务] 提交任务领奖 RoleId:{player.Id} TaskId:{req.TaskId} RewardCount:{normalizedItems.Count}");

        await Task.CompletedTask;
    }
}
