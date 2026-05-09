---
paths: ["server/**/*.cs"]
---

# Component + IEntity 持久化模式

将 IEntity 数据类（如 RoleBag）合并到 Component 时，Component 实现 IEntity 以保持 MongoEntityRepository 兼容。

## BSON 序列化标记

1. **`[BsonId]`** — 必须在 `Id` 属性上标记，否则 MongoDB 无法将 `Id` 映射为 `_id`
2. **`[BsonIgnore]`** — 运行时索引字段（如 `_indexMap`、`_mailMap`）和运行时状态（如 `_nextItemUid`、`NextMailId`）必须标记
3. **`AfterDeserialize`** — MongoDB 反序列化不会自动调用，通过 MongoEntityRepository 反射在 LoadAsync 后调用
4. **Proto 属性** — 仅通过网络 Protobuf 序列化的类才需要 `[ProtoContract]`/`[ProtoMember]`。纯 MongoDB 持久化的 Component 和嵌套数据类不需要

## 步骤

1. Component 实现 `IEntity`，添加 `[BsonId] public long Id { get; set; }`
2. 把原 IEntity 类的持久化字段搬到 Component 上
3. 运行时索引字段标记 `[BsonIgnore]`
4. `AfterDeserialize` / `RebuildIndex` 保留在 Component 上
5. 删除原 IEntity 类

## LoadAsync 拷贝模式

`MongoEntityRepository.LoadAsync<T>` 返回新实例，但数据需要写到已挂载的 Component（`self`）上：

```csharp
public static async Task<RoleBagComponent> LoadBagAsync(this RoleBagComponent self, long roleId)
{
    var bag = await DatabaseManager.Instance.EntityRepo.LoadAsync<RoleBagComponent>(roleId);
    if (bag == null)
    {
        bag = new RoleBagComponent { Id = roleId, Capacity = 100 };
        await DatabaseManager.Instance.EntityRepo.InsertAsync(bag);
    }
    // 拷贝到 self，保留 self 的 _parent 引用
    self.Id = bag.Id;
    self.Capacity = bag.Capacity;
    self.Items = bag.Items;
    self.RebuildIndex();
    return self;
}
```

**Why:** `LoadAsync` 返回的实例 `_parent` 为 null，不能直接替代已挂载的 Component。拷贝字段 + 手动 RebuildIndex 确保 self 数据正确且保持 Entity 树关系。

## 安全性

- Component 基类的 `_parent` 是私有字段，`Parent` 只有 getter → BSON 不会序列化，MongoDB 持久化安全
- `AfterDeserialize` 通过反射调用，Component 实现 IEntity 后泛型约束 `T : class, IEntity` 仍满足

## Proto 属性移除条件

当数据仅通过 MongoDB（BSON）持久化、不通过网络 Protobuf 序列化时，`[ProtoContract]`/`[ProtoMember]` 可移除。嵌套数据类（BagItem、RoleMail 等）同理。
