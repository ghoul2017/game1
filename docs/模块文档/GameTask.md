# GameTask 模块

目标读者：人类。

## 定位

GameTask 管理任务接取、事件驱动进度、提交领奖和放弃。`RoleTaskComponent` 使用 `List<RoleTask>` 保存任务。

## 同步业务与事件

任务业务方法 `AcceptTask`、`UpdateProgress`、`SubmitTask`、`GiveUpTask` 都同步修改任务状态。当前已接入的同步事件为：

| 事件 | 目标类型 | 匹配字段 |
|------|----------|----------|
| MonsterKilledEvent | KillMonster (1) | Monster CfgId |
| PlayerItemGainedEvent | CollectItem (2) | Item ConfigId；当前只由 `PlayerSystem.GrantItem` 生产 |
| PlayerLevelUpEvent | ReachLevel (4) | Level |

任务发生变化后立即向客户端推送 `TaskChangedNtf`。

## 状态机

```text
[未接取] → AcceptTask → 进行中 State=0
                         │
                         └─ 所有目标完成 → State=1（待领奖）
                                                │
                                                └─ SubmitTask → State=2（已领奖）

进行中 → GiveUpTask → 从列表移除
已完成或已领奖 → GiveUpTask → 拒绝
```

## 任务操作

### 接取

1. 校验任务配置存在、没有重复接取。
2. 校验进行中任务少于 20。
3. 从 `TaskConfig` 生成目标列表。
4. 加入 `Tasks`。

### 进度

`UpdateProgress` 只匹配进行中的任务，按 `GoalType + GoalParam` 增加数量并限制到目标值。物品获得事件数量为 long，系统先用 long 计算目标剩余量和本次增量，确认增量处于 int 范围后再写入仍为 int 的 CurrentCount。全部目标完成时设置 `State=1` 和 `CompleteTime`。

### 提交领奖

1. 校验任务处于 `State=1`。
2. 将配置表的 `int[] RewardConfigs` 和 `int[] RewardCounts` 交给 `BagItemAddDataGenerator`；生成器验证数组等长、正数量、物品配置和表格 MaxStack，并返回 normalized `List<BagItemAddData>`。
3. 只有当前背包 `Items.Count > Capacity` 时返回“背包已满”；100/100 仍可提交完整奖励批次。
4. 通过校验后先设置任务 `State=2`，最后调用背包 AddItems 完整发奖。
5. `TaskSubmitAck.Rewards` 使用 AddItems 返回的本次获得展示量；背包变化通知只由 AddItems 发送。

## 数据结构

`Tasks` 使用 `List<RoleTask>`，任务对象自身承载 `TaskId`。

## 已知局限

1. `PreTaskId` 和 `MinLevel` 尚未接入接取校验。
2. `AcceptNpcId` 和 `SubmitNpcId` 尚未参与接取或提交校验，任务可以直接通过 ID 操作。
3. TalkNpc、EnterScene、UseItem、KillPlayer、CompleteDungeon 等目标尚无事件生产者。
4. `RewardExp` 和 `RewardGold` 当前未发放；提交只发放 `RewardConfigs/RewardCounts`。
5. CollectItem 进度只接收 `PlayerSystem.GrantItem` 产生的事件；邮件附件和任务奖励直接调用 `AddItems`，不会推进收集物品目标。
