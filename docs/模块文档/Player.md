# Player 模块

目标读者：人类。

## 定位

Player 模块管理在线玩家的运行时聚合 `PlayerEntity`、会话绑定、内存索引和批量持久化。Bag、Mail、GameTask、RoleAttribute 等玩家组件挂在同一个 `PlayerEntity` 下，但仍分别写入各自的 MongoDB 集合。

## 运行时模型

`PlayerEntity` 在选角入场时创建、下线后从在线索引移除。在线期间以内存对象为业务权威：任务、战斗奖励、背包和邮件操作同步修改内存并调用 `MarkDirty()`，业务方法不等待数据库。

业务实体不提供 `RoleData`、`RoleAttribute`、`RoleBag`、`RoleMail`、`RoleTask` 或 `Online` 快捷属性；调用方通过必需语义的 `GetComponent<T>()` 获取组件。`Context.Players` 等 Module 级全局入口保留。

服务端从游戏主线程启动网络监听和会话接收，异步网络链路通过 `SynchronizationContext` 自动恢复到 `GameSynchronizationContext`；`GameTimer` 的线程池回调则显式投递回该上下文。因此玩家业务、断线清理、脏版本更新和定时器业务回调在游戏主线程串行执行，无需额外的网络二次调度。

`PlayerComponent` 维护三套在线索引：

- `Players`：roleId → PlayerEntity
- `PlayersByUserId`：userId → PlayerEntity
- `PlayersByName`：roleName → PlayerEntity

## 上线加载

`PlayerSystem.EnterGame` 先加载或创建 `RoleDataComponent`，再发布 `PlayerOnlineEvent`。各模块通过异步生命周期处理器加载自己的集合并挂载组件：

```text
PlayerOnlineEvent
  ├── PlayerOnlineLoadAttributeEventHandler → RoleAttributeComponent
  ├── PlayerOnlineLoadBagEventHandler       → RoleBagComponent
  ├── PlayerOnlineLoadMailEventHandler      → RoleMailComponent
  └── PlayerOnlineLoadTaskEventHandler      → RoleTaskComponent
```

加载和首次建档属于生命周期数据库操作，可以等待 MongoDB；组件挂载完成后才发送 `EnterGameNtf`，避免客户端进入半初始化状态。

## 批量持久化

`PlayerPersistenceSystem` 负责在线玩家数据落库：

1. 业务修改组件后只标记该组件的脏版本。
2. 每 60 秒、玩家断线和服务端停服时，将待保存组件序列化为 BSON 快照。
3. 快照按组件类型分组，每个 MongoDB 集合使用一次无序 `BulkWrite` 批量替换。
4. 只有保存完成时版本仍与快照一致，才清除该组件的脏标记；保存过程中再次发生的修改会保留到下一批。
5. 集合写入或快照生成失败时保留脏标记。已下线玩家放在 `PendingSavePlayers` 中，供后续定时批次或停服批次重试。

所有批次通过 `SaveQueue` 串行，避免较旧快照晚于较新快照覆盖数据库。不同组件集合分别保存，不提供跨集合事务或原子提交；这是当前 Demo/框架阶段确认接受的语义。

## Component 与集合

| Component | 是否持久化 | MongoDB 集合 |
|-----------|------------|--------------|
| PlayerComponent | 否 | — |
| PlayerOnlineComponent | 否 | — |
| RoleDataComponent | 是 | `role_data_component` |
| RoleAttributeComponent | 是 | `role_attribute_component` |
| RoleBagComponent | 是 | `role_bag_component` |
| RoleMailComponent | 是 | `role_mail_component` |
| RoleTaskComponent | 是 | `role_task_component` |

`RoleAttributeComponent.ToFightAttribute()` 是 Player 到 Fight 的数据桥梁。新角色的 `Hp/MaxHp` 暂时为 10 亿，这是 Demo 测试语义，正式数值阶段前必须复核。

## 关键文件

| 文件 | 职责 |
|------|------|
| `PlayerEntity.cs` | 玩家聚合及组件脏版本 |
| `PlayerComponent.cs` | 在线索引、待保存玩家和保存队列 |
| `PlayerSystem.cs` | 入场、上下线、广播和同步奖励业务 |
| `PlayerPersistenceSystem.cs` | 定时、断线、停服批量保存 |
| `MongoDb.cs` | 分集合 `BulkWrite` 实现 |
| `Handlers/PlayerOnlineLoadAttributeEventHandler.cs` | 上线加载角色属性 |

## 已知边界

1. 进程崩溃最多会丢失上一次成功批次之后的内存变更；正常断线和正常停服会主动排队保存。
2. MongoDB 在最终停服批次持续不可用时，进程退出后仍可能丢失未落库数据。
3. 不同组件集合不是原子提交，跨任务、背包、属性的业务可能在异常退出时出现部分落库。
