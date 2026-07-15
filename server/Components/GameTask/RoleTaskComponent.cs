using System.Collections.Generic;
using System.Linq;
using Game.Components.Player;
using MongoDB.Bson.Serialization.Attributes;
using Shared.Core.Database;
using Shared.Core.ECS;

namespace Game.Components.GameTask;

/// <summary>
/// 角色任务组件。持久化到 MongoDB（集合名 role_task_component）。
/// </summary>
[ComponentOf(typeof(PlayerEntity))]
public class RoleTaskComponent : Component, IEntity
{
    [BsonId]
    public long Id { get; set; }

    /// <summary>任务列表；数量上限较小，TaskId 由任务对象自身承载</summary>
    public List<RoleTask> Tasks { get; set; } = [];

    internal RoleTask? GetByTaskId(int taskId)
    {
        return Tasks.FirstOrDefault(task => task.TaskId == taskId);
    }
}
