using System.Collections.Generic;
using Game.Components.Player;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using Shared.Core.ECS;

namespace Game.Components.Bag;

/// <summary>
/// 角色背包组件。作为 PlayerEntity 完整文档的嵌入数据保存。
/// </summary>
[ComponentOf(typeof(PlayerEntity))]
public class RoleBagComponent : Component
{
    /// <summary>业务满包判断阈值；物品栈数量允许超过该值。</summary>
    public int Capacity { get; set; } = 100;

    /// <summary>物品字典：Uid → BagItem。</summary>
    [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
    public Dictionary<long, BagItem> Items { get; set; } = new();
}
