using System;
using System.Collections.Generic;
using System.Linq;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Log;

namespace Game.Components.GameTask;

/// <summary>
/// 任务组件系统。提供跨业务入口复用的任务进度与通知能力。
/// </summary>
public static class RoleTaskSystem
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(RoleTaskSystem));

    /// <summary>
    /// 更新任务进度（事件驱动）。
    /// </summary>
    public static List<int> UpdateProgress(
        this RoleTaskComponent self, int goalType, int goalParam, long count)
    {
        if (count <= 0)
            return [];

        var changedTaskIds = new List<int>();

        foreach (var task in self.Tasks)
        {
            if (!task.IsInProgress)
                continue;

            var changed = false;
            foreach (var goal in task.Goals)
            {
                if (goal.GoalType != goalType)
                    continue;
                if (goalParam != 0 && goal.GoalParam != goalParam)
                    continue;
                if (goal.IsFinished)
                    continue;

                var remaining = (long)goal.TargetCount - goal.CurrentCount;
                var increment = Math.Min(count, remaining);
                goal.CurrentCount += checked((int)increment);
                changed = true;
            }

            if (!changed)
                continue;

            if (task.Goals.All(goal => goal.IsFinished))
            {
                task.State = 1;
                task.CompleteTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Log.Info($"任务完成 RoleId:{self.Parent!.Id} TaskId:{task.TaskId}");
            }

            changedTaskIds.Add(task.TaskId);
        }

        if (changedTaskIds.Count > 0)
            self.MarkDirty();

        return changedTaskIds;
    }

    /// <summary>推送任务变更通知给在线玩家</summary>
    public static void NotifyTaskChanged(this RoleTaskComponent self, long roleId, List<int> changedTaskIds)
    {
        var tasks = changedTaskIds
            .Select(self.GetByTaskId)
            .Where(task => task != null)
            .Select(task => RoleTaskHelper.ToInfo(task!))
            .ToList();

        if (tasks.Count == 0)
            return;

        Context.Players.SendToPlayer(roleId, new TaskChangedNtf { Tasks = tasks });
    }
}
