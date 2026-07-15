# Bag 模块

目标读者：人类。

## 定位

Bag 管理玩家物品的增删、整理、交换和使用，也是邮件附件、任务奖励与怪物掉落的最终入口。`RoleBagComponent` 持久化到 `role_bag_component`。

## 数据结构与持久化

运行时以 `Dictionary<int, BagItem>` 保存“格子索引 → 物品”。MongoDB 使用 `ArrayOfDocuments` 表示整数键，避免 Document 字典要求字符串键导致序列化失败。

`AddItem`、`RemoveItem`、`RemoveByIndex`、`Tidy`、`Swap` 和 `UseItem` 都同步修改内存并标脏。业务方法不保存数据库；Player 批量持久化系统在定时、断线或停服批次中写入该集合。

## 主要操作

### 添加物品

1. 优先叠加到同配置且未满的格子。
2. 剩余数量依次放入最小空闲格子。
3. 背包不足时返回 Partial；已经发生的部分添加仍会标脏。

任务提交会在调用 `AddItem` 前保留背包内存快照。如果多项奖励中途失败，由任务业务恢复整份快照。

### 使用物品

1. 校验格子、数量和消耗品 ConfigId 范围。
2. `UseItem` 同步扣除物品并标脏。
3. `ItemEffectSystem.ApplyItemEffect` 同步应用 HP、MP 或复活效果；HP/MP 属性变化同时标脏 `RoleAttributeComponent`。

当前语义是先扣物品再应用效果，效果失败不回滚消耗。

## 跨模块关系

| 来源/目标 | 行为 |
|-----------|------|
| Mail | 领取附件后调用 `AddItem` |
| GameTask | 提交任务后调用 `AddItem` |
| Monster | 掉落后调用 `GrantItem` → `AddItem` |
| Fight / Player | 物品效果修改战斗或角色属性 |

## 已知局限

1. 物品过期尚未清理。
2. 物品效果失败不回滚消耗。
3. 部分添加的通知语义仍需在内容阶段统一。
4. 与任务、邮件等其他集合之间不提供原子提交。
