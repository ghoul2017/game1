# GameTask 模块

目标读者：人类。

## 定位

GameTask 管理任务接取、事件驱动进度、提交领奖和放弃。`RoleTaskComponent` 是玩家内存聚合的一部分，使用 `List<RoleTask>` 保存任务，并持久化到 `role_task_component`。

## 同步业务与事件

任务业务方法 `AcceptTask`、`UpdateProgress`、`SubmitTask`、`GiveUpTask` 都同步修改内存并标脏，不等待 MongoDB。当前已接入的同步事件为：

| 事件 | 目标类型 | 匹配字段 |
|------|----------|----------|
| MonsterKilledEvent | KillMonster (1) | Monster CfgId |
| PlayerItemGainedEvent | CollectItem (2) | Item ConfigId |
| PlayerLevelUpEvent | ReachLevel (4) | Level |

任务发生变化后立即向客户端推送 `TaskChangedNtf`；持久化由 Player 的定时、断线或停服批次完成。

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
4. 加入 `Tasks` 并标记任务组件为脏。

### 进度

`UpdateProgress` 只匹配进行中的任务，按 `GoalType + GoalParam` 增加数量并限制到目标值。物品获得事件数量为 long，系统先用 long 计算目标剩余量和本次增量，确认增量处于 int 范围后再写入仍为 int 的 CurrentCount。全部目标完成时设置 `State=1` 和 `CompleteTime`，最后统一标脏。

### 提交领奖

1. 校验任务处于 `State=1`。
2. 将配置表的 `int[] RewardConfigs` 和 `int[] RewardCounts` 交给 `BagItemAddDataGenerator`；生成器验证数组等长、正数量、物品配置和表格 MaxStack，并返回 normalized `List<BagItemAddData>`。
3. 只有当前背包 `Items.Count > Capacity` 时返回“背包已满”；100/100 仍可提交完整奖励批次。
4. 通过校验后先设置任务 `State=2` 并标脏，最后调用背包 AddItems 完整发奖。
5. `TaskSubmitAck.Rewards` 使用 AddItems 返回的本次获得展示量；背包变化通知只由 AddItems 发送。

完整预检后的意外添加失败直接暴露并保留已写任务状态，不创建快照或回滚。任务与背包保存在不同集合，当前不使用奖励回执或跨集合事务。进程在两个集合的写入之间异常退出时，可能出现部分落库；这是当前 Demo/框架阶段确认接受的取舍，不应在业务方法中改回即时 `await` 保存。

## 上线加载与结构

```text
PlayerOnlineEvent → PlayerOnlineLoadTaskEventHandler
  ├── LoadAsync<RoleTaskComponent>(roleId)
  ├── 不存在 → 创建空 List + InsertAsync
  └── player.AddComponent(taskComp)
```

`Tasks` 必须保持为 `List<RoleTask>`。旧的 `Dictionary<int, RoleTask>` 会触发 MongoDB Document 字典键必须为字符串的序列化异常。

## 已知局限

1. 前置任务和最低等级配置尚未接入接取校验。
2. TalkNpc、EnterScene、UseItem、KillPlayer、CompleteDungeon 等目标尚无事件生产者。
3. `RewardExp` 和 `RewardGold` 当前未发放；提交只发放 `RewardConfigs/RewardCounts`。
4. 不同玩家组件分集合批量保存，不保证任务状态与奖励跨集合原子一致。
