using System.Collections.Generic;
using Game.Entities;
using Shared.Core.ECS;

namespace Game.Components.Player;

/// <summary>
/// 玩家组件。挂载到根实体，管理在线玩家集合。
/// </summary>
[ComponentOf(typeof(Module))]
public class PlayerComponent : Component
{
    public readonly Dictionary<long, PlayerEntity> Players = new();           // roleId → PlayerEntity
    public readonly Dictionary<long, PlayerEntity> PlayersByUserId = new();   // userId → PlayerEntity
    public readonly Dictionary<string, PlayerEntity> PlayersByName = new();   // roleName → PlayerEntity
}
