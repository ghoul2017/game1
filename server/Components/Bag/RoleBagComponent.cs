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

    /// <summary>业务满包判断阈值；物品栈数量允许超过该值。</summary>
    public int Capacity { get; set; } = 100;

    /// <summary>物品字典：Uid → BagItem。</summary>
    [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
    public Dictionary<long, BagItem> Items { get; set; } = new();
}
