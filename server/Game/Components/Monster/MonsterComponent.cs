using Game.Entities;
using Shared.Core.ECS;

namespace Game.Components.Monster;

/// <summary>
/// 怪物组件。挂载到根实体。
/// </summary>
[ComponentOf(typeof(Module))]
public class MonsterComponent : Component
{
}
