using ProtoBuf;
using Server.Core.Entity;

namespace Server.Game.Bag;

/// <summary>
/// 角色背包实体。持久化到数据库。
/// 对应数据库表: p_role_bag (id=roleId, data=protobuf)
/// </summary>
[ProtoContract]
public class RoleBag : IEntity
{
    [ProtoMember(1)]
    public long Id { get; set; }

    /// <summary>背包容量（最大格子数）</summary>
    [ProtoMember(2)]
    public int Capacity { get; set; } = 100;

    /// <summary>物品列表</summary>
    [ProtoMember(3)]
    public List<BagItem> Items { get; set; } = [];

    /// <summary>运行时索引：index → BagItem（不序列化）</summary>
    [ProtoIgnore]
    private Dictionary<int, BagItem>? _indexMap;

    [ProtoAfterDeserialization]
    internal void AfterDeserialize()
    {
        RebuildIndex();
    }

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
