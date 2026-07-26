using System.Collections.Generic;
using System.Linq;
using Shared.Common.Config;
using Shared.Common.Protos;

namespace Game.Components.GameTask;

public static class RoleTaskHelper
{
    public static List<TaskGoal> CreateGoals(TaskConfig config)
    {
        var goals = new List<TaskGoal>();
        if (config.GoalCount >= 1)
            AddGoal(goals, config.GoalType1, config.GoalParam1, config.GoalTarget1);

        if (config.GoalCount >= 2)
            AddGoal(goals, config.GoalType2, config.GoalParam2, config.GoalTarget2);

        if (config.GoalCount >= 3)
            AddGoal(goals, config.GoalType3, config.GoalParam3, config.GoalTarget3);

        return goals;
    }

    public static bool TryParseRewards(TaskConfig config, out List<TaskRewardInfo> rewards)
    {
        rewards = [];
        if (string.IsNullOrEmpty(config.RewardItems))
            return true;

        foreach (var part in config.RewardItems.Split(','))
        {
            var pair = part.Trim().Split(':');
            if (pair.Length != 2 ||
                !int.TryParse(pair[0], out var configId) ||
                !long.TryParse(pair[1], out var count) ||
                configId <= 0 ||
                count <= 0)
            {
                rewards = [];
                return false;
            }

            rewards.Add(new TaskRewardInfo
            {
                ConfigId = configId,
                Count = count,
            });
        }

        return true;
    }

    public static TaskInfo ToInfo(RoleTask task)
    {
        return new TaskInfo
        {
            TaskId = task.TaskId,
            State = task.State,
            AcceptTime = task.AcceptTime,
            CompleteTime = task.CompleteTime,
            Goals = task.Goals.Select(goal => new TaskGoalInfo
            {
                GoalType = goal.GoalType,
                GoalParam = goal.GoalParam,
                CurrentCount = goal.CurrentCount,
                TargetCount = goal.TargetCount,
            }).ToList(),
        };
    }

    private static void AddGoal(List<TaskGoal> goals, int goalType, int goalParam, int targetCount)
    {
        if (goalType <= 0)
            return;

        goals.Add(new TaskGoal
        {
            GoalType = goalType,
            GoalParam = goalParam,
            TargetCount = targetCount,
        });
    }
}
