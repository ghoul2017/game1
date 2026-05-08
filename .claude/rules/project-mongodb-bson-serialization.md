---
paths: ["server/**/*.cs"]
---

# MongoDB BSON 序列化与 Component 持久化

Component 实现 `IEntity` 后通过 MongoEntityRepository 持久化：

1. **`[BsonId]`** — 必须在 `Id` 属性上标记，否则 MongoDB 无法将 `Id` 映射为 `_id`
2. **`[BsonIgnore]`** — 运行时索引字段（如 `_indexMap`、`_mailMap`、`_taskMap`）和运行时状态（如 `_nextItemUid`、`NextMailId`）必须标记，否则 MongoDB 序列化时包含无意义字段
3. **`AfterDeserialize`** — MongoDB 反序列化不会自动调用，MongoEntityRepository.InvokeAfterDeserialize 通过反射在 LoadAsync 后调用
4. **Proto 属性** — 仅通过网络 Protobuf 序列化的类才需要 `[ProtoContract]`/`[ProtoMember]`。纯 MongoDB 持久化的 Component 和嵌套数据类不需要

```csharp
// 示例：Component 实现 IEntity 持久化
[ComponentOf(typeof(PlayerEntity))]
public class RoleBagComponent : Component, IEntity
{
    [BsonId]
    public long Id { get; set; }

    public int Capacity { get; set; } = 100;
    public List<BagItem> Items { get; set; } = [];

    [BsonIgnore]
    internal long _nextItemUid;

    [BsonIgnore]
    private Dictionary<int, BagItem>? _indexMap;

    internal void AfterDeserialize() { RebuildIndex(); }
    internal void RebuildIndex() { ... }
}
```
