using Game.Entities;
using Shared.Core.ECS;

namespace Game.Components.Fight;

/// <summary>
/// 战斗组件。挂载到根实体。
/// </summary>
[ComponentOf(typeof(Module))]
public class FightComponent : Component
{
}
