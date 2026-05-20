using System.Collections.Generic;
using System.Linq;

namespace Game.Components.GameTask;

/// <summary>
/// 角色任务数据
/// </summary>
public class RoleTask
{
    /// <summary>任务配置ID</summary>
    public int TaskId { get; set; }

    /// <summary>任务状态：0=进行中, 1=已完成(待领奖), 2=已领奖</summary>
    public int State { get; set; }

    /// <summary>接取时间（Unix毫秒）</summary>
    public long AcceptTime { get; set; }

    /// <summary>完成时间（Unix毫秒）</summary>
    public long CompleteTime { get; set; }

    /// <summary>目标进度列表</summary>
    public List<TaskGoal> Goals { get; set; } = [];

    /// <summary>是否已完成（待领奖）</summary>
    public bool IsCompleted => State == 1;

    /// <summary>是否已领奖</summary>
    public bool IsClaimed => State == 2;

    /// <summary>是否进行中</summary>
    public bool IsInProgress => State == 0;
}

/// <summary>
/// 任务目标进度
/// </summary>
public class TaskGoal
{
    /// <summary>目标类型</summary>
    public int GoalType { get; set; }

    /// <summary>目标参数</summary>
    public int GoalParam { get; set; }

    /// <summary>当前进度</summary>
    public int CurrentCount { get; set; }

    /// <summary>目标数量</summary>
    public int TargetCount { get; set; }

    /// <summary>该目标是否已完成</summary>
    public bool IsFinished => CurrentCount >= TargetCount;
}

/// <summary>
/// 任务目标类型常量
/// </summary>
public static class TaskGoalType
{
    public const int KillMonster = 1;
    public const int CollectItem = 2;
    public const int TalkNpc = 3;
    public const int ReachLevel = 4;
    public const int EnterScene = 5;
    public const int UseItem = 6;
    public const int KillPlayer = 7;
    public const int CompleteDungeon = 8;
}
