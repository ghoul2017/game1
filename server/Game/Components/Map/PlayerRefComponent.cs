using Game.Components.Player;
using Game.Entities;
using Shared.Core.ECS;

namespace Game.Components.Map;

/// <summary>
/// 玩家引用组件。桥接 MapPlayerEntity 到 PlayerEntity。
/// </summary>
[ComponentOf(typeof(MapPlayerEntity))]
public class PlayerRefComponent : Component
{
    public PlayerEntity? PlayerEntity;
}
