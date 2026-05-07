using Game.Entities;
using Shared.Core.ECS;

namespace Game.Components.Task;

/// <summary>
/// 任务组件。挂载到根实体。
/// </summary>
[ComponentOf(typeof(Module))]
public class TaskComponent : Component
{
}
