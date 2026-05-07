using Game.Entities;
using Shared.Core.ECS;

namespace Game.Components.Map;

/// <summary>
/// 地图组件。挂载到根实体。
/// </summary>
[ComponentOf(typeof(Module))]
public class MapComponent : Component
{
}
