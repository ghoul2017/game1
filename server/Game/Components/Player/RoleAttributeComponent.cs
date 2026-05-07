using Game.Components.Player;
using Shared.Core.ECS;

namespace Game.Components.Player;

/// <summary>
/// 角色属性组件。持有 RoleAttribute 持久化数据。
/// </summary>
[ComponentOf(typeof(PlayerEntity))]
public class RoleAttributeComponent : Component
{
    public RoleAttribute? RoleAttribute { get; set; }
}
