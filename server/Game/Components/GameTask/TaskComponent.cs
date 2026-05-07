using Game.Components.GameTask;
using Game.Components.Player;
using Shared.Core.ECS;

namespace Game.Components.GameTask;

/// <summary>
/// 任务组件。持有 RoleTaskList 持久化数据。
/// </summary>
[ComponentOf(typeof(PlayerEntity))]
public class TaskComponent : Component
{
    public RoleTaskList? RoleTaskList { get; set; }
}
