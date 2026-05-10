# GameTask 模块文档

## 概述

GameTask 模块实现任务系统，支持任务接取、进度追踪、提交领奖、放弃。多种目标类型（击杀怪物、收集物品、升级等）通过事件驱动更新进度。任务数据持久化到 MongoDB。

## 文件清单

| 文件 | 行数 | 职责 |
|------|------|------|
| `server/Game/Components/GameTask/RoleTask.cs` | 71 | 任务数据类 + 目标类 + 目标类型常量 |
| `server/Game/Components/GameTask/RoleTaskComponent.cs` | 47 | PlayerEntity 级 Component（IEntity 持久化） |
| `server/Game/Components/GameTask/RoleTaskSystem.cs` | 296 | 任务操作扩展方法 + 错误码 |
| `server/Game/Components/GameTask/RoleTaskHandlers.cs` | 160 | 消息处理 + 事件订阅 |
| `client/Assets/Scripts/shared/Common/Protos/TaskProtos.cs` | 124 | 消息定义 |
| `client/Assets/Scripts/shared/Common/MsgIdConst.cs` | — | MsgId 10601-10700 段 |

## 数据模型

### RoleTask（任务）

```csharp
namespace Game.Components.GameTask;

public class RoleTask
{
    public int TaskId;                          // 配置ID
    public int State;                           // 0=进行中, 1=已完成(待领奖), 2=已领奖
    public long AcceptTime;                     // 接取时间（Unix毫秒）
    public long CompleteTime;                   // 完成时间
    public List<TaskGoal> Goals = [];           // 目标列表

    public bool IsCompleted => State >= 1;
    public bool IsClaimed => State >= 2;
    public bool IsInProgress => State == 0;
}
```

### TaskGoal（任务目标）

```csharp
public class TaskGoal
{
    public int GoalType;       // 目标类型
    public int GoalParam;      // 目标参数
    public int CurrentCount;   // 当前进度
    public int TargetCount;    // 目标数量
    public bool IsFinished => CurrentCount >= TargetCount;
}
```

### TaskGoalType（目标类型常量）

| 常量 | 值 | 说明 |
|------|---|------|
| KillMonster | 1 | 击杀怪物 |
| CollectItem | 2 | 收集物品 |
| TalkNpc | 3 | 与NPC对话 |
| ReachLevel | 4 | 达到等级 |
| EnterScene | 5 | 进入场景 |
| UseItem | 6 | 使用物品 |
| KillPlayer | 7 | 击杀玩家 |
| CompleteDungeon | 8 | 完成副本 |

## Component

### RoleTaskComponent（PlayerEntity 级）

```csharp
[ComponentOf(typeof(PlayerEntity))]
public class RoleTaskComponent : Component, IEntity
{
    [BsonId] public long Id { get; set; }                         // = RoleId
    public List<RoleTask> Tasks = [];                             // 任务列表

    [BsonIgnore] private Dictionary<int, RoleTask>? _taskMap;     // 运行时索引 taskId → RoleTask

    internal void AfterDeserialize();
    internal void RebuildIndex();
    internal Dictionary<int, RoleTask> GetTaskMap();
    internal RoleTask? GetByTaskId(int taskId);
}
```

- MongoDB 集合名：`role_task_list`

## System 详解

### RoleTaskSystem

常量：`MaxActiveTaskCount = 20`（最大同时进行任务数）

| 方法 | 签名 | 说明 |
|------|------|------|
| LoadTasksAsync | `LoadTasksAsync(this RoleTaskComponent, long roleId) → Task<RoleTaskComponent>` | 从 MongoDB 加载 |
| SaveTasksAsync | `SaveTasksAsync(this RoleTaskComponent) → Task` | 保存到 MongoDB |
| AcceptTask | `AcceptTask(this RoleTaskComponent, int taskId) → TaskAcceptResult` | 接取任务（从配置表生成目标） |
| SubmitTaskAsync | `SubmitTaskAsync(this RoleTaskComponent, long roleId, int taskId) → Task<TaskSubmitResult>` | 提交任务+发放奖励 |
| GiveUpTask | `GiveUpTask(this RoleTaskComponent, int taskId) → TaskGiveUpResult` | 放弃任务 |
| UpdateProgress | `UpdateProgress(this RoleTaskComponent, int goalType, int goalParam, int count) → List<int>` | 更新进度（返回变更的 taskId 列表） |
| GetTaskList | `GetTaskList(this RoleTaskComponent) → List<RoleTask>` | 获取任务列表 |
| ToTaskInfo | `ToTaskInfo(this RoleTaskComponent, RoleTask) → TaskInfo` | 转换为网络格式 |
| NotifyTaskChanged | `NotifyTaskChanged(this RoleTaskComponent, long roleId, List<int> changedTaskIds)` | 推送任务变更通知 |

#### 任务状态机

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

#### SubmitTaskAsync 奖励发放

```
1. 解析 TaskConfig.RewardItems（"itemId:count" 格式）
2. 调用 player.RoleBag.AddItem() 添加物品
3. 发布 PlayerItemGainedEvent
4. 更新 State = 2
5. 保存到 MongoDB
```

### 错误码

| Code | 常量 | 含义 |
|------|------|------|
| 0 | Ok | 成功 |
| 3001 | InvalidParam | 参数无效 |
| 3002 | TaskNotFound | 任务不存在 |
| 3003 | TaskAlreadyAccepted | 任务已接取 |
| 3004 | TaskFull | 任务栏已满 |
| 3005 | TaskNotCompleted | 任务未完成 |
| 3006 | TaskAlreadyClaimed | 任务已领奖 |
| 3007 | TaskCompleted | 任务已完成（不能放弃） |

## Handler 详解

### 消息处理

| Handler | MsgId | 请求 | 响应 | 说明 |
|---------|-------|------|------|------|
| QueryTaskListHandler | 10601→10602 | QueryTaskListReq | QueryTaskListAck | 查询任务列表 |
| TaskAcceptHandler | 10603→10604 | TaskAcceptReq(taskId) | TaskAcceptAck(task) | 接取任务 |
| TaskSubmitHandler | 10605→10606 | TaskSubmitReq(taskId) | TaskSubmitAck(taskId, rewards) | 提交领奖 |
| TaskGiveUpHandler | 10607→10608 | TaskGiveUpReq(taskId) | TaskGiveUpAck(taskId) | 放弃任务 |

### 事件订阅

| 事件 | 方法 | 说明 |
|------|------|------|
| PlayerOnlineEvent | OnPlayerOnline | 挂载 RoleTaskComponent + LoadTasksAsync |
| PlayerLevelUpEvent | OnPlayerLevelUp | 更新 ReachLevel 类型任务进度 |
| PlayerExpChangedEvent | OnPlayerExpChanged | 预留（未实现逻辑） |
| PlayerGoldChangedEvent | OnPlayerGoldChanged | 预留（未实现逻辑） |
| PlayerItemGainedEvent | OnPlayerItemGained | 更新 CollectItem 类型任务进度 |
| MonsterKilledEvent | OnMonsterKilled | 更新 KillMonster 类型任务进度 |

事件驱动更新流程：
1. 事件触发 → `RoleTaskSystem.UpdateProgress(goalType, goalParam, count)`
2. 遍历所有进行中任务的目标
3. 匹配 GoalType + GoalParam → 累加 CurrentCount
4. 所有目标完成 → State = 1
5. 推送 TaskChangedNtf 给客户端

## 协议汇总

| MsgId | 消息类 | 方向 | 关键字段 |
|-------|--------|------|----------|
| 10601 | QueryTaskListReq | C→S | （无） |
| 10602 | QueryTaskListAck | S→C | Code, Tasks(List\<TaskInfo\>) |
| 10603 | TaskAcceptReq | C→S | TaskId |
| 10604 | TaskAcceptAck | S→C | Code, Task(TaskInfo) |
| 10605 | TaskSubmitReq | C→S | TaskId |
| 10606 | TaskSubmitAck | S→C | Code, TaskId, Rewards(List\<TaskRewardInfo\>) |
| 10607 | TaskGiveUpReq | C→S | TaskId |
| 10608 | TaskGiveUpAck | S→C | Code, TaskId |
| 10609 | TaskChangedNtf | S→C | Tasks(List\<TaskInfo\>) |

共享结构：
- `TaskInfo`：TaskId, State, AcceptTime, CompleteTime, Goals(List\<TaskGoalInfo\>)
- `TaskGoalInfo`：GoalType, GoalParam, CurrentCount, TargetCount
- `TaskRewardInfo`：ConfigId, Count

## 模块依赖

```
GameTask 模块
  ├── 依赖 → MongoDB（RoleTaskComponent 持久化）
  ├── 依赖 → Player（player.RoleTask 访问）
  ├── 依赖 → Bag（SubmitTaskAsync → AddItem 发放奖励）
  ├── 依赖 → Config（TaskConfig 配置表）
  ├── 依赖 → EventBus（订阅多种事件驱动进度更新）
  │
  └── 被依赖 ←
       └── Monster（MonsterKilledEvent 驱动 KillMonster 任务进度）
```
