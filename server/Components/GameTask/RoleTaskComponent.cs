using System.Collections.Generic;
using Game.Components.Player;
using MongoDB.Bson.Serialization.Attributes;
using Shared.Core.Database;
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

    /// <summary>任务字典：taskId → RoleTask</summary>
    public Dictionary<int, RoleTask> Tasks { get; set; } = new();

    internal RoleTask? GetByTaskId(int taskId)
    {
        return Tasks.TryGetValue(taskId, out var task) ? task : null;
    }
}
