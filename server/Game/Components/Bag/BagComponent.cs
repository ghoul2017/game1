using Game.Components.Bag;
using Game.Components.Player;
using Shared.Core.ECS;

namespace Game.Components.Bag;

/// <summary>
/// 背包组件。持有 RoleBag 持久化数据。
/// </summary>
[ComponentOf(typeof(PlayerEntity))]
public class BagComponent : Component
{
    public RoleBag? RoleBag { get; set; }

    /// <summary>物品UID生成器</summary>
    internal long _nextItemUid;
}
