using Game.Components.Bag;
using Game.Components.Player;
using Shared.Core.Config;
using Shared.Common.Config;
using Shared.Common.Protos;
using Shared.Core.Log;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Components.GameTask;

/// <summary>
/// 任务系统。提供任务接取、进度更新、完成判定、领奖等功能。
/// </summary>
public static class RoleTaskSystem
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(RoleTaskSystem));

    private const int MaxActiveTaskCount = 20;

    /// <summary>接取任务</summary>
    public static TaskAcceptResult AcceptTask(this RoleTaskComponent self, int taskId)
    {
        var taskCfg = ConfigManager.Get<TaskConfig>(taskId);
        if (taskCfg == null)
        {
            Log.Warn($"任务配置不存在 TaskId:{taskId}");
            return TaskAcceptResult.Fail("任务不存在");
        }

        if (self.GetByTaskId(taskId) != null)
            return TaskAcceptResult.Fail("任务已接取");

        var activeCount = self.Tasks.Count(t => t.IsInProgress);
        if (activeCount >= MaxActiveTaskCount)
            return TaskAcceptResult.Fail("任务列表已满");

        var goals = new List<TaskGoal>();
        if (taskCfg.GoalCount >= 1 && taskCfg.GoalType1 > 0)
        {
            goals.Add(new TaskGoal
            {
                GoalType = taskCfg.GoalType1,
                GoalParam = taskCfg.GoalParam1,
                TargetCount = taskCfg.GoalTarget1,
            });
        }
        if (taskCfg.GoalCount >= 2 && taskCfg.GoalType2 > 0)
        {
            goals.Add(new TaskGoal
            {
                GoalType = taskCfg.GoalType2,
                GoalParam = taskCfg.GoalParam2,
                TargetCount = taskCfg.GoalTarget2,
            });
        }
        if (taskCfg.GoalCount >= 3 && taskCfg.GoalType3 > 0)
        {
            goals.Add(new TaskGoal
            {
                GoalType = taskCfg.GoalType3,
                GoalParam = taskCfg.GoalParam3,
                TargetCount = taskCfg.GoalTarget3,
            });
        }

        var task = new RoleTask
        {
            TaskId = taskId,
            State = 0,
            AcceptTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            CompleteTime = 0,
            Goals = goals,
        };

        self.Tasks.Add(task);
        self.MarkDirty();

        Log.Info($"接取任务 RoleId:{self.Id} TaskId:{taskId} GoalCount:{goals.Count}");
        return TaskAcceptResult.Ok();
    }

    /// <summary>提交已完成的任务，领取奖励</summary>
    public static TaskSubmitResult SubmitTask(this RoleTaskComponent self, long roleId, int taskId)
    {
        var task = self.GetByTaskId(taskId);
        if (task == null)
            return TaskSubmitResult.Fail("任务不存在");

        if (task.IsClaimed)
            return TaskSubmitResult.Fail("任务已提交");

        if (!task.IsCompleted)
            return TaskSubmitResult.Fail("任务未完成");

        var taskCfg = ConfigManager.Get<TaskConfig>(taskId);
        if (taskCfg == null)
        {
            Log.Warn($"任务配置不存在 TaskId:{taskId}");
            return TaskSubmitResult.Fail("任务不存在");
        }

        var player = Context.Players.GetByRoleId(roleId);
        if (player == null)
            return TaskSubmitResult.Fail("任务不存在");

        var rewards = new List<TaskRewardInfo>();
        if (!string.IsNullOrEmpty(taskCfg.RewardItems))
        {
            var itemParts = taskCfg.RewardItems.Split(',');
            foreach (var part in itemParts)
            {
                var kv = part.Trim().Split(':');
                if (kv.Length != 2 ||
                    !int.TryParse(kv[0], out int itemId) ||
                    !int.TryParse(kv[1], out int itemCount) ||
                    itemId <= 0 || itemCount <= 0)
                    return TaskSubmitResult.Fail("任务奖励配置错误");

                rewards.Add(new TaskRewardInfo { ConfigId = itemId, Count = itemCount });
            }
        }

        var itemSnapshot = CloneBagItems(player.RoleBag.Items);
        foreach (var reward in rewards)
        {
            var addResult = player.RoleBag.AddItem(reward.ConfigId, reward.Count);
            if (!addResult.Success)
            {
                player.RoleBag.Items = itemSnapshot;
                Log.Warn($"任务奖励发放失败 RoleId:{roleId} ItemId:{reward.ConfigId} {addResult.ErrorMessage}");
                return TaskSubmitResult.Fail("奖励发放失败");
            }
        }

        task.State = 2;
        self.MarkDirty();
        if (rewards.Count > 0)
            player.RoleBag.MarkDirty();

        Log.Info($"任务提交完成 RoleId:{roleId} TaskId:{taskId} RewardExp:{taskCfg.RewardExp} RewardGold:{taskCfg.RewardGold}");

        return TaskSubmitResult.Ok(rewards);
    }

    /// <summary>放弃任务</summary>
    public static TaskGiveUpResult GiveUpTask(this RoleTaskComponent self, int taskId)
    {
        var task = self.GetByTaskId(taskId);
        if (task == null)
            return TaskGiveUpResult.Fail("任务不存在");

        if (!task.IsInProgress)
            return TaskGiveUpResult.Fail("任务已完成");

        self.Tasks.Remove(task);
        self.MarkDirty();

        return TaskGiveUpResult.Ok();
    }

    /// <summary>
    /// 更新任务进度（事件驱动）。
    /// </summary>
    public static List<int> UpdateProgress(
        this RoleTaskComponent self, int goalType, int goalParam, int count)
    {
        if (count <= 0)
            return [];

        var changedTaskIds = new List<int>();

        foreach (var task in self.Tasks)
        {
            if (!task.IsInProgress)
                continue;

            bool changed = false;
            foreach (var goal in task.Goals)
            {
                if (goal.GoalType != goalType)
                    continue;
                if (goalParam != 0 && goal.GoalParam != goalParam)
                    continue;
                if (goal.IsFinished)
                    continue;

                goal.CurrentCount = Math.Min(goal.CurrentCount + count, goal.TargetCount);
                changed = true;
            }

            if (changed)
            {
                if (task.Goals.All(g => g.IsFinished))
                {
                    task.State = 1;
                    task.CompleteTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    Log.Info($"任务完成 RoleId:{self.Id} TaskId:{task.TaskId}");
                }

                changedTaskIds.Add(task.TaskId);
            }
        }

        if (changedTaskIds.Count > 0)
            self.MarkDirty();

        return changedTaskIds;
    }

    /// <summary>获取任务列表</summary>
    public static List<RoleTask> GetTaskList(this RoleTaskComponent self)
    {
        return self.Tasks.ToList();
    }

    /// <summary>转换为网络传输用的 TaskInfo</summary>
    public static TaskInfo ToTaskInfo(this RoleTaskComponent self, RoleTask task)
    {
        return new TaskInfo
        {
            TaskId = task.TaskId,
            State = task.State,
            AcceptTime = task.AcceptTime,
            CompleteTime = task.CompleteTime,
            Goals = task.Goals.Select(g => new TaskGoalInfo
            {
                GoalType = g.GoalType,
                GoalParam = g.GoalParam,
                CurrentCount = g.CurrentCount,
                TargetCount = g.TargetCount,
            }).ToList(),
        };
    }

    /// <summary>推送任务变更通知给在线玩家</summary>
    public static void NotifyTaskChanged(this RoleTaskComponent self, long roleId, List<int> changedTaskIds)
    {
        var tasks = changedTaskIds
            .Select(self.GetByTaskId)
            .Where(t => t != null)
            .Select(t => self.ToTaskInfo(t!))
            .ToList();

        if (tasks.Count == 0)
            return;

        var ntf = new TaskChangedNtf { Tasks = tasks };
        Context.Players.SendToPlayer(roleId, ntf);
    }

    private static Dictionary<int, BagItem> CloneBagItems(Dictionary<int, BagItem> items)
    {
        return items.ToDictionary(pair => pair.Key, pair => new BagItem
        {
            Uid = pair.Value.Uid,
            ConfigId = pair.Value.ConfigId,
            Count = pair.Value.Count,
            Index = pair.Value.Index,
            Bind = pair.Value.Bind,
            ExpireTime = pair.Value.ExpireTime,
        });
    }
}


public record TaskAcceptResult(bool Success, string ErrorMessage = "")
{
    public static TaskAcceptResult Ok() => new(true);
    public static TaskAcceptResult Fail(string message) => new(false, message);
}

public record TaskSubmitResult(bool Success, string ErrorMessage = "", List<TaskRewardInfo>? Rewards = null)
{
    public static TaskSubmitResult Ok(List<TaskRewardInfo> rewards) => new(true, Rewards: rewards);
    public static TaskSubmitResult Fail(string message) => new(false, message);
}

public record TaskGiveUpResult(bool Success, string ErrorMessage = "")
{
    public static TaskGiveUpResult Ok() => new(true);
    public static TaskGiveUpResult Fail(string message) => new(false, message);
}
