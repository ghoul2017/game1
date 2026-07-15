using System.Collections.Generic;
using Game.Components.Player;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using Shared.Core.Database;
using Shared.Core.ECS;

namespace Game.Components.Bag;

/// <summary>
/// 角色背包组件。持久化到 MongoDB（集合名 role_bag_component）。
/// </summary>
[ComponentOf(typeof(PlayerEntity))]
public class RoleBagComponent : Component, IEntity
{
    [BsonId]
    public long Id { get; set; }

    /// <summary>背包容量（最大格子数）</summary>
    public int Capacity { get; set; } = 100;

    /// <summary>物品字典：格子索引 → BagItem</summary>
    [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
    public Dictionary<int, BagItem> Items { get; set; } = new();

    internal BagItem? GetByIndex(int index)
    {
        return Items.TryGetValue(index, out var item) ? item : null;
    }
}
