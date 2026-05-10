# Bag 模块文档

## 概述

Bag 模块实现玩家背包系统，支持物品的增删改查、整理、交换、使用及物品效果应用。背包数据持久化到 MongoDB，运行时通过索引提供 O(1) 格子访问。

## 文件清单

| 文件 | 行数 | 职责 |
|------|------|------|
| `server/Game/Components/Bag/BagItem.cs` | 26 | 物品数据类 |
| `server/Game/Components/Bag/RoleBagComponent.cs` | 59 | PlayerEntity 级 Component（IEntity 持久化） |
| `server/Game/Components/Bag/RoleBagSystem.cs` | 264 | 背包操作扩展方法 + 错误码 |
| `server/Game/Components/Bag/RoleBagHandlers.cs` | 128 | 消息处理 + 事件订阅 |
| `server/Game/Components/Bag/ItemEffectSystem.cs` | 151 | 物品使用效果系统 |
| `client/Assets/Scripts/shared/Common/Protos/BagProtos.cs` | 110 | 消息定义 |
| `client/Assets/Scripts/shared/Common/MsgIdConst.cs` | — | MsgId 10301-10400 段 |

## 数据模型

### BagItem（物品）

```csharp
namespace Game.Components.Bag;

public class BagItem
{
    public long Uid { get; set; }          // 运行时唯一ID
    public int ConfigId { get; set; }       // 配置表ID
    public int Count { get; set; }          // 数量
    public int Index { get; set; }          // 格子索引
    public bool Bind { get; set; }          // 是否绑定
    public long ExpireTime { get; set; }    // 过期时间（0=永不过期）
}
```

- 嵌套在 RoleBagComponent.Items 中，不独立持久化

## Component

### RoleBagComponent（PlayerEntity 级）

```csharp
[ComponentOf(typeof(PlayerEntity))]
public class RoleBagComponent : Component, IEntity
{
    [BsonId] public long Id { get; set; }                    // = RoleId
    public int Capacity { get; set; } = 100;                 // 背包容量
    public List<BagItem> Items { get; set; } = [];           // 物品列表

    [BsonIgnore] internal long _nextItemUid;                 // UID 生成器
    [BsonIgnore] private Dictionary<int, BagItem>? _indexMap; // 运行时索引

    internal void AfterDeserialize();
    internal void RebuildIndex();
    internal Dictionary<int, BagItem> GetIndexMap();
    internal BagItem? GetByIndex(int index);
    internal void RemoveFromIndex(BagItem item);
}
```

- MongoDB 集合名：`role_bag`
- 实现 IEntity，Component 与持久化实体合一
- `_indexMap` 按 Index 快速查找格子物品

## System 详解

### RoleBagSystem

| 方法 | 签名 | 说明 |
|------|------|------|
| SaveBagAsync | `SaveBagAsync(this RoleBagComponent) → Task` | 保存到 MongoDB |
| AddItem | `AddItem(this RoleBagComponent, int configId, int count, int maxStack=999) → AddItemResult` | 添加物品（先叠加再找空格） |
| RemoveItem | `RemoveItem(this RoleBagComponent, int configId, int count) → bool` | 按配置ID移除 |
| RemoveByIndex | `RemoveByIndex(this RoleBagComponent, int index, int count) → bool` | 按格子索引移除 |
| HasEnough | `HasEnough(this RoleBagComponent, int configId, int count) → bool` | 检查物品数量 |
| GetItemCount | `GetItemCount(this RoleBagComponent, int configId) → int` | 获取物品总数 |
| HasSpace | `HasSpace(this RoleBagComponent, int need=1) → bool` | 是否有空格 |
| Tidy | `Tidy(this RoleBagComponent)` | 整理背包（按ConfigId排序，紧凑排列） |
| Swap | `Swap(this RoleBagComponent, int index1, int index2) → bool` | 交换两个格子 |
| UseItem | `UseItem(this RoleBagComponent, int index, int count) → UseItemResult` | 使用物品（扣除数量） |
| GetItems | `GetItems(this RoleBagComponent) → List<BagItem>` | 获取物品快照 |

#### AddItem 流程

```
1. 遍历已有同类物品，尝试叠加（不超过 maxStack）
2. 剩余数量放到新空格（FindEmptyIndex 找最小空闲索引）
3. 背包满返回 Partial 结果
```

### ItemEffectSystem

| 效果类型 | 常量 | 说明 | 状态 |
|---------|------|------|------|
| EffectHp | 1 | HP 恢复 | 已实现 |
| EffectMp | 2 | MP 恢复 | 已实现 |
| EffectBuff | 3 | BUFF 效果 | 未实现 |
| EffectTeleport | 4 | 传送 | 未实现 |
| EffectRevive | 5 | 复活 | 已实现 |

核心方法：`ApplyItemEffectAsync(long roleId, int configId) → Task<ItemUseResult>`

- 从 ConfigManager 获取 ItemEffectConfig
- 根据 EffectType 分发到对应处理方法
- HP/MP 恢复直接修改 RoleAttributeComponent
- 复活效果通过 FightComponent.Revive 实现

## Handler 详解

### 消息处理

| Handler | MsgId | 请求 | 响应 | 说明 |
|---------|-------|------|------|------|
| QueryBagHandler | 10301→10302 | QueryBagReq | QueryBagAck | 查询背包 |
| BagTidyHandler | 10303→10304 | BagTidyReq | BagTidyAck | 整理背包 |
| BagSwapHandler | 10305→10306 | BagSwapReq(index1,index2) | BagSwapAck | 交换格子 |
| BagUseItemHandler | 10307→10308 | BagUseItemReq(index,count) | BagUseItemAck(configId) | 使用物品 |

#### BagUseItemHandler 流程

```
1. 按索引获取物品 → 不存在返回 ItemNotFound
2. 检查数量是否足够
3. 检查 ConfigId 范围（2001-2999 为消耗品）
4. 扣除物品（UseItem）
5. 保存背包（SaveBagAsync）
6. 应用物品效果（ItemEffectSystem.ApplyItemEffectAsync）
7. 返回结果
```

### 事件订阅

| 事件 | 方法 | 说明 |
|------|------|------|
| PlayerOnlineEvent | OnPlayerOnline | 挂载 RoleBagComponent + LoadAsync |

上线加载流程：
1. `player.AddComponent<RoleBagComponent>()`
2. `MongoDb.Instance.LoadAsync<RoleBagComponent>(roleId)`
3. 不存在则创建默认背包（Capacity=100）并 InsertAsync
4. 拷贝字段到已挂载 Component，调用 RebuildIndex

### 错误码

| Code | 常量 | 含义 |
|------|------|------|
| 0 | Ok | 成功 |
| 1001 | InvalidParam | 参数无效 |
| 1002 | BagFull | 背包已满 |
| 1003 | ItemNotFound | 物品不存在 |
| 1004 | ItemNotEnough | 物品数量不足 |
| 1005 | ItemUseFail | 物品使用失败 |

## 协议汇总

| MsgId | 消息类 | 方向 | 关键字段 |
|-------|--------|------|----------|
| 10301 | QueryBagReq | C→S | （无） |
| 10302 | QueryBagAck | S→C | Code, Items(List\<BagItemInfo\>), Capacity |
| 10303 | BagTidyReq | C→S | （无） |
| 10304 | BagTidyAck | S→C | Code, Items(List\<BagItemInfo\>) |
| 10305 | BagSwapReq | C→S | Index1, Index2 |
| 10306 | BagSwapAck | S→C | Code |
| 10307 | BagUseItemReq | C→S | Index, Count |
| 10308 | BagUseItemAck | S→C | Code, ConfigId |
| 10309 | BagItemChangedNtf | S→C | ChangedItems, RemovedIndices |

共享结构 `BagItemInfo`：Uid, ConfigId, Count, Index, Bind

## 模块依赖

```
Bag 模块
  ├── 依赖 → MongoDB（RoleBagComponent 持久化）
  ├── 依赖 → Player（player.RoleBag 访问）
  ├── 依赖 → Fight（ItemEffectSystem.ApplyReviveEffect → Context.Fight）
  ├── 依赖 → Config（ItemEffectConfig 配置）
  │
  └── 被依赖 ←
       ├── Mail（ClaimAttachments → AddItem）
       ├── GameTask（SubmitTask → AddItem）
       └── Monster（掉落物品 → AddItem）
```
