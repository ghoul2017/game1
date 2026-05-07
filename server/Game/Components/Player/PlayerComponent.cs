using Game.Entities;
using Shared.Core.ECS;

namespace Game.Components.Player;

/// <summary>
/// 玩家组件。挂载到根实体，管理玩家相关逻辑。
/// </summary>
[ComponentOf(typeof(Module))]
public class PlayerComponent : Component
{
}
