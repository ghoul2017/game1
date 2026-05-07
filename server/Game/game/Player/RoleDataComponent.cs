using Shared.Core.ECS;

namespace Game.Game.Player;

/// <summary>
/// 角色持久化数据组件。持有 Role 实体引用。
/// </summary>
[ComponentOf(typeof(PlayerEntity))]
public class RoleDataComponent : Component
{
    public Role? Role { get; set; }
}
