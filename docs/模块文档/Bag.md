# Bag 模块

目标读者：人类。

## 定位

Bag 管理玩家物品的增删、整理和交换，保留尚未完成的道具使用入口，也是邮件附件、任务奖励与怪物掉落的最终入口。`RoleBagComponent` 持久化到 `role_bag_component`。

## 数据结构与持久化

运行时以 `Dictionary<int, BagItem>` 保存“格子索引 → 物品”。MongoDB 使用 `ArrayOfDocuments` 表示整数键，避免 Document 字典要求字符串键导致序列化失败。

`AddItem`、`RemoveItem`、`RemoveByIndex`、`Tidy` 和 `Swap` 都同步修改内存并标脏。业务方法不保存数据库；Player 批量持久化系统在定时、断线或停服批次中写入该集合。

## 主要操作

### 添加物品

1. 优先叠加到同配置且未满的格子。
2. 剩余数量依次放入最小空闲格子。
3. 返回实际添加数量；空间不足时已经发生的部分添加仍会标脏。

任务提交先通过 `CanAddItems` 整批预检奖励，再写已领取状态并调用 `AddItems` 发放；预检后的意外失败直接暴露，不恢复背包快照。

### 使用物品（功能未完成）

当前代码只有功能骨架：

1. Handler 校验格子、数量和消耗品 ConfigId 范围。
2. Handler 调用 `RemoveByIndex` 同步扣除物品并标脏。
3. `ItemEffectHelper.ApplyItemEffect` 尝试应用 HP、MP 或复活效果。

该链路尚未按 `ItemId` 建立效果配置关联，请求 `Count` 与效果次数的语义也未确定，不能视为已交付功能。实现任务还需确认复活卷轴配置，并保证无有效效果时不只扣除物品后返回成功。

## 跨模块关系

| 来源/目标 | 行为 |
|-----------|------|
| Mail | 领取附件后调用 `AddItem` |
| GameTask | 提交任务后调用 `AddItem` |
| Monster | 掉落后调用 `GrantItem` → `AddItem` |
| Fight / Player | 未完成的道具效果入口计划修改战斗或角色属性 |

## 已知局限

1. 物品过期尚未清理。
2. 部分添加的通知语义仍需在内容阶段统一。
3. 与任务、邮件等其他集合之间不提供原子提交。
