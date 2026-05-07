using Game.Entities;
using Shared.Core.ECS;

namespace Game.Components.Bag;

/// <summary>
/// 背包组件。挂载到根实体。
/// </summary>
[ComponentOf(typeof(Module))]
public class BagComponent : Component
{
}
