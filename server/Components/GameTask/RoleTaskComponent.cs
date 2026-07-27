using System.Collections.Generic;
using System.Linq;
using Game.Components.Player;
using Shared.Core.ECS;

namespace Game.Components.GameTask;

/// <summary>
/// 角色任务组件。作为 PlayerEntity 完整文档的嵌入数据保存。
/// </summary>
[ComponentOf(typeof(PlayerEntity))]
public class RoleTaskComponent : Component
{
    /// <summary>任务列表；数量上限较小，TaskId 由任务对象自身承载</summary>
    public List<RoleTask> Tasks { get; set; } = [];

    internal RoleTask? GetByTaskId(int taskId)
    {
        return Tasks.FirstOrDefault(task => task.TaskId == taskId);
    }
}
