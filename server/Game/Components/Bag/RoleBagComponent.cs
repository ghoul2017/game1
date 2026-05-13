using System.Collections.Generic;
using Game.Components.Player;
using Shared.Core.Database;
using MongoDB.Bson.Serialization.Attributes;
using Shared.Core.ECS;

namespace Game.Components.Bag;

/// <summary>
/// 角色背包组件。持久化到 MongoDB（集合名 role_bag）。
/// </summary>
[ComponentOf(typeof(PlayerEntity))]
public class RoleBagComponent : Component, IEntity
{
    [BsonId]
    public long Id { get; set; }

    /// <summary>背包容量（最大格子数）</summary>
    public int Capacity { get; set; } = 100;

    /// <summary>物品列表</summary>
    public List<BagItem> Items { get; set; } = [];

    /// <summary>运行时索引：index → BagItem（不序列化）</summary>
    [BsonIgnore]
    private Dictionary<int, BagItem>? _indexMap;

    internal void AfterDeserialize() { RebuildIndex(); }

    internal void RebuildIndex()
    {
        _indexMap = new Dictionary<int, BagItem>(Items.Count);
        foreach (var item in Items)
        {
            _indexMap[item.Index] = item;
        }
    }

    internal Dictionary<int, BagItem> GetIndexMap()
    {
        _indexMap ??= new Dictionary<int, BagItem>(Items.Count);
        return _indexMap;
    }

    internal BagItem? GetByIndex(int index)
    {
        return GetIndexMap().TryGetValue(index, out var item) ? item : null;
    }

    internal void RemoveFromIndex(BagItem item)
    {
        GetIndexMap().Remove(item.Index);
    }
}
