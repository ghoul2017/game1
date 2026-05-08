using System.Collections.Generic;
using Game.Components.Player;
using Game.Core.Database;
using MongoDB.Bson.Serialization.Attributes;
using Shared.Core.ECS;

namespace Game.Components.GameTask;

/// <summary>
/// 角色任务组件。持久化到 MongoDB（集合名 role_task_list）。
/// </summary>
[ComponentOf(typeof(PlayerEntity))]
public class RoleTaskComponent : Component, IEntity
{
    [BsonId]
    public long Id { get; set; }

    /// <summary>当前任务列表</summary>
    public List<RoleTask> Tasks { get; set; } = [];

    /// <summary>运行时索引：taskId → RoleTask（不序列化）</summary>
    [BsonIgnore]
    private Dictionary<int, RoleTask>? _taskMap;

    internal void AfterDeserialize() { RebuildIndex(); }

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
