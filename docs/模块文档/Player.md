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

## 角色数据权威边界

角色当前存在三处等级字段：`UserRole.Level`、`RoleDataComponent.Level` 和 `RoleAttributeComponent.Level`。实际经验发放与升级只修改 `RoleAttributeComponent.Exp/Level`；前两处只在创建时初始化，之后没有同步。因此：

- 登录角色列表读取 `UserRole.Level`，可能一直显示 1 级。
- `EnterGameNtf` 读取 `RoleDataComponent.Level`，也可能是旧值。
- 在线战斗奖励、升级通知和持久化成长的当前权威是 `RoleAttributeComponent`。

`RoleDataComponent.Exp` 当前没有生产者，`LastLogoutTime` 也没有在断线链路写入。

客户端入场协议只包含角色 ID、名称、RoleData 等级和职业，没有发送 RoleAttribute 快照。`GameData.CurrentHp/MaxHp` 因而以 0/100 的本地默认值开始；MainPanel 的经验条、金币和钻石也是固定展示文本。服务端会发送 `ExpChangedNtf`、`GoldChangedNtf`、`LevelUpNtf`、`ItemGainNtf` 和 `MpChangedNtf`，但当前客户端没有对应 Handler。

`RoleAttributeComponent.ToFightAttribute()` 在创建 `MapPlayerEntity` 时复制一份战斗属性。之后战斗伤害和 `FightHelper.Revive` 修改地图实体的 `FightAttribute`，经验升级及 HP/MP 药品修改持久化 `RoleAttributeComponent`，两者没有双向同步。离图后重建投影会再次从 RoleAttributeComponent 复制，可能覆盖之前的战斗 HP 状态。新角色的 `Hp/MaxHp` 暂时为 10 亿，这是 Demo 测试语义，正式数值阶段前必须复核。

## 关键文件

| 文件 | 职责 |
|------|------|
| `PlayerEntity.cs` | 玩家聚合根 |
| `PlayerComponent.cs` | 在线索引 |
| `PlayerSystem.cs` | 入场、上下线、广播和同步奖励业务 |
| `PlayerExpeditionComponent.cs` | 远征永久进度、最近不可变结算回执和保存字段 |

## 已知问题与边界

1. `UserRole`、`RoleDataComponent` 和 `RoleAttributeComponent` 的等级/经验字段没有统一同步，角色列表与入场通知可能展示旧等级。
2. 地图战斗属性是 RoleAttribute 的一次性快照；战斗 HP、药品 HP、升级属性和持久化属性可能互相覆盖或表现不一致。
3. `RoleDataComponent.LastLogoutTime` 当前从未写入，不能作为真实下线时间使用。
4. 入场缺少当前属性快照，经验、金币、升级、物品和 MP 推送也缺客户端消费者；旧主界面属性与服务端成长状态没有闭环。
