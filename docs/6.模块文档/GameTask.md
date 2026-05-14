# GameTask 模块

## 定位

GameTask 模块管理玩家任务——接取、进度追踪、提交领奖、放弃。多种目标类型（击杀怪物、收集物品、升级等）通过事件驱动自动更新进度，任务完成时推送通知，提交时发放奖励。

## 核心设计

### 事件驱动进度更新

任务进度不靠玩家手动上报，而是由其他模块发布事件自动驱动。GameTask 订阅多种事件，调用 `UpdateProgress(goalType, goalParam, count)` 匹配目标并累加进度：

| 事件 | 目标类型 | 说明 |
|------|---------|------|
| MonsterKilledEvent | KillMonster (1) | 怪物被击杀时检查 CfgId 匹配 |
| PlayerItemGainedEvent | CollectItem (2) | 物品获得时检查 ConfigId 匹配 |
| PlayerLevelUpEvent | ReachLevel (4) | 升级时检查等级匹配 |

### 任务状态机

```
[未接取] → AcceptTask → 进行中 (State=0)
                              │
                    UpdateProgress（所有 Goal 完成）
                              │
                              ▼
                     已完成 (State=1, 待领奖)
                              │
                    SubmitTaskAsync（发放奖励）
                              │
                              ▼
                     已领奖 (State=2)

进行中 → GiveUpTask → 移除任务
已完成 → GiveUpTask → 拒绝（已完成的任务不能放弃）
```

### Component 与持久化合一

`RoleTaskComponent` 同时是 Component 和 IEntity，持久化到 `role_task_list` 集合。直接使用 `Dictionary<int, RoleTask>` 作为 MongoDB 持久化结构（key=taskId），提供 O(1) 任务查找。

### 保留的文件

| 文件 | 职责 |
|------|------|
| `RoleTaskComponent.cs` | 任务 Component（IEntity 持久化，Dictionary 主结构） |
| `RoleTaskSystem.cs` | 任务操作扩展方法 |
| `RoleTaskHandlers.cs` | 消息处理 + 多事件订阅 |
| `RoleTask.cs` | 任务/目标数据类 + 目标类型常量 |

## 任务操作

### 接取任务

```
1. 校验任务配置存在
2. 校验未重复接取
3. 校验进行中任务数 ≤ MaxActiveTaskCount (20)
4. 从 TaskConfig 生成 Goal 列表
5. 添加到 Tasks + 更新索引
6. 保存到 MongoDB
```

### 提交领奖

```
1. 校验任务存在且已完成 (State=1)
2. 解析 TaskConfig.RewardItems（"itemId:count" 格式）
3. 调用 player.RoleBag.AddItem() 添加物品到背包
4. 发布 PlayerItemGainedEvent
5. 更新 State = 2
6. 保存到 MongoDB
```

### 进度更新

```
事件触发 → UpdateProgress(goalType, goalParam, count)
  │
  ├── 1. 遍历所有进行中任务 (State=0) 的目标
  ├── 2. 匹配 GoalType + GoalParam → 累加 CurrentCount
  ├── 3. 所有目标完成 → State = 1
  └── 4. 推送 TaskChangedNtf 给客户端
```

## 上线加载

```
PlayerOnlineEvent → RoleTaskHandlers.OnPlayerOnline
  ├── LoadAsync<RoleTaskComponent>(roleId)
  ├── 不存在 → 创建默认空任务列表 + InsertAsync
  └── player.AddComponent(taskComp)
```

## 跨模块协作

```
                ┌─────────────┐
                │   Config    │ TaskConfig 配置表
                └──────┬──────┘
                       │
┌──────────┐    ┌──────▼──────┐    ┌──────────┐
│  Fight   │───►│  GameTask   │───►│   Bag    │
│ MonsterKilled│  进度追踪     │    │ 奖励发放  │
└──────────┘    └─────────────┘    └──────────┘
                       ▲
                ┌──────┴──────┐
                │   Player    │ LevelUpEvent / ItemGainedEvent
                └─────────────┘
```

| 交互方 | 方向 | 内容 |
|--------|------|------|
| Fight | 单向 | `MonsterKilledEvent` → 更新 KillMonster 目标 |
| Player | 单向 | `PlayerLevelUpEvent` → 更新 ReachLevel 目标；`PlayerItemGainedEvent` → 更新 CollectItem 目标 |
| Bag | 单向 | `SubmitTaskAsync` → `AddItem` 发放奖励 |
| Config | 单向 | 读取 `TaskConfig` 驱动目标生成和奖励配置 |

## 已知局限

1. **任务前置条件**：无前置任务校验，任何任务可直接接取
2. **日常/周常**：只有永久任务，无重置机制
3. **目标类型不全**：TalkNpc(3)、EnterScene(5)、UseItem(6)、KillPlayer(7)、CompleteDungeon(8) 定义了但无事件驱动
4. **奖励单一**：只支持物品奖励，无经验/金币奖励
