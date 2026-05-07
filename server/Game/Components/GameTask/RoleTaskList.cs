using System.Collections.Generic;
using Game.Core.Entity;
using MongoDB.Bson.Serialization.Attributes;
using ProtoBuf;

namespace Game.Components.GameTask;

/// <summary>
/// 角色任务列表实体。每个角色一条记录，包含所有进行中/已完成任务。
/// 对应数据库表: p_role_task_list (id=roleId, data=protobuf)
/// </summary>
[ProtoContract]
public class RoleTaskList : IEntity
{
    [ProtoMember(1)]
    [BsonId]
    public long Id { get; set; }

    /// <summary>当前任务列表</summary>
    [ProtoMember(2)]
    public List<RoleTask> Tasks { get; set; } = [];

    /// <summary>运行时索引：taskId → RoleTask（不序列化）</summary>
    [ProtoIgnore]
    [BsonIgnore]
    private Dictionary<int, RoleTask>? _taskMap;

    [ProtoAfterDeserialization]
    internal void AfterDeserialize()
    {
        RebuildIndex();
    }

    internal void RebuildIndex()
    {
        _taskMap = new Dictionary<int, RoleTask>(Tasks.Count);
        foreach (var task in Tasks)
        {
            _taskMap[task.TaskId] = task;
        }
    }

    internal Dictionary<int, RoleTask> GetTaskMap()
    {
        _taskMap ??= new Dictionary<int, RoleTask>(Tasks.Count);
        return _taskMap;
    }

    internal RoleTask? GetByTaskId(int taskId)
    {
        return GetTaskMap().TryGetValue(taskId, out var task) ? task : null;
    }
}

/// <summary>角色任务数据</summary>
[ProtoContract]
public class RoleTask
{
    /// <summary>任务配置ID</summary>
    [ProtoMember(1)]
    public int TaskId { get; set; }

    /// <summary>任务状态：0=进行中, 1=已完成(待领奖), 2=已领奖</summary>
    [ProtoMember(2)]
    public int State { get; set; }

    /// <summary>接取时间（Unix毫秒）</summary>
    [ProtoMember(3)]
    public long AcceptTime { get; set; }

    /// <summary>完成时间（Unix毫秒）</summary>
    [ProtoMember(4)]
    public long CompleteTime { get; set; }

    /// <summary>目标进度列表</summary>
    [ProtoMember(5)]
    public List<TaskGoal> Goals { get; set; } = [];

    /// <summary>是否已完成（待领奖）</summary>
    public bool IsCompleted => State == 1;

    /// <summary>是否已领奖</summary>
    public bool IsClaimed => State == 2;

    /// <summary>是否进行中</summary>
    public bool IsInProgress => State == 0;
}

/// <summary>任务目标进度</summary>
[ProtoContract]
public class TaskGoal
{
    /// <summary>目标类型（对应配置表中的目标类型）</summary>
    [ProtoMember(1)]
    public int GoalType { get; set; }

    /// <summary>目标参数（如怪物ID、物品ID等）</summary>
    [ProtoMember(2)]
    public int GoalParam { get; set; }

    /// <summary>当前进度</summary>
    [ProtoMember(3)]
    public int CurrentCount { get; set; }

    /// <summary>目标数量</summary>
    [ProtoMember(4)]
    public int TargetCount { get; set; }

    /// <summary>该目标是否已完成</summary>
    public bool IsFinished => CurrentCount >= TargetCount;
}

/// <summary>任务目标类型常量</summary>
public static class TaskGoalType
{
    public const int KillMonster = 1;   // 击杀怪物
    public const int CollectItem = 2;   // 收集物品
    public const int TalkNpc = 3;       // 与NPC对话
    public const int ReachLevel = 4;    // 达到等级
    public const int EnterScene = 5;    // 进入场景
    public const int UseItem = 6;       // 使用物品
    public const int KillPlayer = 7;    // 击杀玩家
    public const int CompleteDungeon = 8; // 完成副本
}
