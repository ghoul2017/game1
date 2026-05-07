# MongoDB BSON 序列化与 Protobuf 属性共存

实体类同时标记 `[ProtoContract]`/`[ProtoMember]`（网络序列化）和 MongoDB BSON 文档存储时：

1. **`[BsonId]`** — 必须在 `Id` 属性上标记，否则 MongoDB 无法将 `Id` 映射为 `_id`
2. **`[BsonIgnore]`** — 运行时索引字段（如 `_indexMap`、`_mailMap`、`_taskMap`）必须标记，否则 MongoDB 序列化时包含无意义字段
3. **`[ProtoAfterDeserialization]` 不生效** — MongoDB 反序列化不会调用此回调，需要在 Repository 的 LoadAsync 中手动调用 `AfterDeserialize`/`RebuildIndex` 重建运行时索引

```csharp
// 示例
[ProtoContract]
public class RoleBag : IEntity
{
    [ProtoMember(1)]
    [BsonId]
    public long Id { get; set; }

    [ProtoIgnore]
    [BsonIgnore]
    private Dictionary<int, BagItem>? _indexMap;
}
```
