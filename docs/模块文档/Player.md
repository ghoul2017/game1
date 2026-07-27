# Player 模块

目标读者：人类。

## 定位

Player 模块管理在线玩家聚合 `PlayerEntity`、会话绑定和内存索引。

## 运行时模型

`PlayerEntity` 在选角入场后加入在线索引，下线后从在线索引移除。在线期间，任务、战斗奖励、背包、邮件和在线元数据都属于同一个玩家聚合。

业务实体不提供 `RoleData`、`RoleAttribute`、`RoleBag`、`RoleMail`、`RoleTask` 或 `Online` 快捷属性；调用方通过必需语义的 `GetComponent<T>()` 获取组件。`Context.Players` 等 Module 级全局入口保留。

服务端从游戏主线程启动网络监听和会话接收，异步网络链路通过 `SynchronizationContext` 自动恢复到 `GameSynchronizationContext`；`GameTimer` 的线程池回调则显式投递回该上下文。因此玩家业务、断线清理和定时器业务回调在游戏主线程串行执行，无需额外的网络二次调度。

`PlayerComponent` 维护三套在线索引：

- `Players`：roleId → PlayerEntity
- `PlayersByUserId`：userId → PlayerEntity
- `PlayersByName`：roleName → PlayerEntity

`RoleAttributeComponent.ToFightAttribute()` 是 Player 到 Fight 的数据桥梁。新角色的 `Hp/MaxHp` 暂时为 10 亿，这是 Demo 测试语义，正式数值阶段前必须复核。

## 关键文件

| 文件 | 职责 |
|------|------|
| `PlayerEntity.cs` | 玩家聚合根 |
| `PlayerComponent.cs` | 在线索引 |
| `PlayerSystem.cs` | 入场、上下线、广播和同步奖励业务 |
