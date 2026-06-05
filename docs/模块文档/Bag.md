# Bag 模块

## 定位

Bag 模块管理玩家的物品背包——增删改查、整理交换、使用消耗品。它是物品流通的枢纽：邮件附件、任务奖励、怪物掉落最终都汇入背包。

## 核心设计

### Component 与持久化合一

`RoleBagComponent` 同时是 Component 和 IEntity，直接持久化到 MongoDB 的 `role_bag_component` 集合。上线时通过 `AddComponent(instance)` 挂载 DB 加载实例，无需逐字段拷贝。

### 运行时索引

背包物品按 `Index`（格子编号）存储，直接使用 `Dictionary<int, BagItem>` 作为 MongoDB 持久化结构。MongoDB 原生支持 Dictionary 序列化（key 存储为 BSON document 的 string key），O(1) 格子查找无需重建索引。

### 物品效果反向依赖 Fight

`ItemEffectSystem` 属于 Bag 模块，但物品使用效果（HP 恢复、复活）需要修改 Fight 模块的战斗状态。这是一个 **Bag → Fight 的反向依赖**，通过 `FightSystem.Revive()` / `RoleAttributeComponent` 间接实现，而非直接依赖 FightComponent。

### 保留的文件

| 文件 | 职责 |
|------|------|
| `RoleBagComponent.cs` | 背包 Component（IEntity 持久化，Dictionary 主结构） |
| `RoleBagSystem.cs` | 背包操作扩展方法（增删查改、整理、交换） |
| `Handlers/PlayerOnlineLoadBagEventHandler.cs` | PlayerOnline → 加载背包 |
| `RoleBagHelper.cs` | BagItemInfo 转换 |
| `Handlers/QueryBagReqHandler.cs` | 查询背包消息处理 |
| `Handlers/BagTidyReqHandler.cs` | 整理背包消息处理 |
| `Handlers/BagSwapReqHandler.cs` | 交换背包格子消息处理 |
| `Handlers/BagUseItemReqHandler.cs` | 使用物品消息处理 |
| `BagItem.cs` | 物品数据类 |
| `ItemEffectSystem.cs` | 物品使用效果（HP/MP 恢复、复活） |

## 背包操作

### 添加物品

```
1. 遍历已有同类物品，尝试叠加（不超过 maxStack，默认 999）
2. 剩余数量放到新空格（FindEmptyIndex 找最小空闲索引）
3. 背包满返回 Partial 结果
```

叠加优先于开新格——同类物品不会散布在多个格子，除非单格已满。

### 使用物品

```
1. 按索引获取物品 → 不存在返回错误
2. 检查数量是否足够
3. 检查 ConfigId 范围（2001-2999 为消耗品）
4. 扣除物品（UseItem）
5. 保存背包（SaveBagAsync）
6. 应用物品效果（ItemEffectSystem.ApplyItemEffectAsync）
```

扣物品在效果前，确保效果失败时物品已消耗（当前实现不回滚）。

### 物品效果

| 效果类型 | 常量 | 说明 | 状态 |
|---------|------|------|------|
| EffectHp | 1 | HP 恢复 | 已实现 |
| EffectMp | 2 | MP 恢复 | 已实现 |
| EffectRevive | 5 | 复活 | 已实现 |

## 上线加载

```
PlayerOnlineEvent → PlayerOnlineLoadBagEventHandler
  ├── LoadAsync<RoleBagComponent>(roleId)
  ├── 不存在 → new RoleBagComponent { Capacity = 100 } + InsertAsync
  └── player.AddComponent(bag)   // AddComponent(instance)，_parent 正确
```

## 跨模块协作

```
                ┌─────────────┐
                │   Config    │ ItemConfig / ItemEffectConfig
                └──────┬──────┘
                       │
┌──────────┐    ┌──────▼──────┐    ┌──────────┐
│   Mail   │───►│     Bag     │◄───│ GameTask │
│ 附件领取  │    │  增删查改    │    │ 任务奖励  │
└──────────┘    └──────┬──────┘    └──────────┘
                       │
                ┌──────▼──────┐
                │   Fight     │ HP/MP 恢复、复活
                └─────────────┘
```

| 交互方 | 方向 | 内容 |
|--------|------|------|
| Mail | 单向 | 领取附件 → `AddItem` |
| GameTask | 单向 | 任务奖励 → `AddItem` |
| Monster | 单向 | 掉落物品 → `AddItem` |
| Fight | 单向（反向） | ItemEffectSystem 修改战斗状态（HP 恢复、复活） |
| Config | 单向 | 读取 `ItemConfig`、`ItemEffectConfig` |

## 已知局限

1. **效果回滚暂不实现**：物品扣除后若效果应用失败，物品不会退回
2. **物品过期暂不实现**：BagItem 有 ExpireTime 字段但无定时清理
