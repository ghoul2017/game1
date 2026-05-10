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

## AddComponent(instance) 加载模式（推荐）

`Entity.AddComponent<T>(T component)` 可直接挂载外部创建的实例，自动设置 `_parent`。优先使用此模式替代先 `AddComponent()` 再逐字段拷贝：

```csharp
[EventSubscriber]
public static async Task OnPlayerOnline(EventType.PlayerOnlineEvent e)
{
    var player = e.Player;

    var bag = await MongoDb.Instance.LoadAsync<RoleBagComponent>(player.Id);
    if (bag == null)
    {
        bag = new RoleBagComponent { Id = player.Id, Capacity = 100 };
        await MongoDb.Instance.InsertAsync(bag);
    }
    bag.RebuildIndex();
    player.AddComponent(bag);
}
```

**Why:** `AddComponent(instance)` 内部调用 `component.SetParent(this)`，DB 加载实例的 `_parent` 引用正确，不需要先创建空 Component 再逐字段拷贝。代码更简洁，不易遗漏字段。

**注意**：`RebuildIndex()` 等运行时初始化必须在 `AddComponent` 之前调用，因为 `AddComponent` 不会自动触发 `AfterDeserialize`。

## 旧模式：LoadAsync 拷贝（已淘汰）

```csharp
// 已淘汰：先 AddComponent() 再逐字段拷贝
var bagComp = player.AddComponent<RoleBagComponent>();
var bag = await MongoDb.Instance.LoadAsync<RoleBagComponent>(roleId);
bagComp.Id = bag.Id;
bagComp.Capacity = bag.Capacity;
bagComp.Items = bag.Items;
bagComp.RebuildIndex();
```

新代码不要再使用此模式。

## 安全性

- Component 基类的 `_parent` 是私有字段，`Parent` 只有 getter → BSON 不会序列化，MongoDB 持久化安全
- `AfterDeserialize` 通过反射调用，Component 实现 IEntity 后泛型约束 `T : class, IEntity` 仍满足

## Proto 属性移除条件

当数据仅通过 MongoDB（BSON）持久化、不通过网络 Protobuf 序列化时，`[ProtoContract]`/`[ProtoMember]` 可移除。嵌套数据类（BagItem、RoleMail 等）同理。
