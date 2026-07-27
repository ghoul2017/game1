using System.Collections.Generic;
using System.Linq;
using Shared.Common.Config;
using Shared.Common.Protos;

namespace Game.Components.GameTask;

public static class RoleTaskHelper
{
    public static List<TaskGoal> CreateGoals(TaskConfig config)
    {
        List<TaskGoal> goals = [];
        if (config.GoalCount >= 1)
            AddGoal(goals, config.GoalType1, config.GoalParam1, config.GoalTarget1);

        if (config.GoalCount >= 2)
            AddGoal(goals, config.GoalType2, config.GoalParam2, config.GoalTarget2);

        if (config.GoalCount >= 3)
            AddGoal(goals, config.GoalType3, config.GoalParam3, config.GoalTarget3);

        return goals;
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
