# Player 模块

目标读者：人类。

## 定位

Player 模块管理在线玩家聚合 `PlayerEntity`、会话绑定和内存索引。

## 运行时模型

`PlayerEntity` 在选角入场后加入在线索引，下线后从在线索引移除。在线期间，任务、战斗奖励、背包、邮件、
在线元数据和 `PlayerExpeditionComponent` 都属于同一个玩家聚合。远征组件保存可带回资源、设施等级、
最近启动/结算标识和原始结算回执字段；活动局 Tick 状态仍只在 `ExpeditionComponent` 内存中。

业务实体不提供 `RoleData`、`RoleAttribute`、`RoleBag`、`RoleMail`、`RoleTask` 或 `Online` 快捷属性；调用方通过必需语义的 `GetComponent<T>()` 获取组件。`Context.Players` 等 Module 级全局入口保留。

服务端从游戏主线程启动网络监听和会话接收，异步网络链路通过 `SynchronizationContext` 自动恢复到 `GameSynchronizationContext`；`GameTimer` 的线程池回调则显式投递回该上下文。因此玩家业务、断线清理和定时器业务回调在游戏主线程串行执行，无需额外的网络二次调度。

`PlayerComponent` 维护三套在线索引：

- `Players`：roleId → PlayerEntity
- `PlayersByUserId`：userId → PlayerEntity
- `PlayersByName`：roleName → PlayerEntity

玩家消息以 `session.RoleId` 和 `PlayerOnlineComponent.Session` 的双向引用共同授权。旧连接即使保留相同 RoleId，
也不能继续执行 Player RPC/Ntf；异步加载完成后还会复核 session 仍连接且仍是该用户当前会话，再进行无 await 的绑定提交。

`RoleAttributeComponent.ToFightAttribute()` 是 Player 到 Fight 的数据桥梁。新角色的 `Hp/MaxHp` 暂时为 10 亿，这是 Demo 测试语义，正式数值阶段前必须复核。

## 关键文件

| 文件 | 职责 |
|------|------|
| `PlayerEntity.cs` | 玩家聚合根 |
| `PlayerComponent.cs` | 在线索引 |
| `PlayerSystem.cs` | 入场、上下线、广播和同步奖励业务 |
| `PlayerExpeditionComponent.cs` | 远征永久进度、最近不可变结算回执和保存字段 |
