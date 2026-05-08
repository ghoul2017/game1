# Event 重构记录

## 解耦成果

| 解耦关系 | 事件 | 说明 |
|---------|------|------|
| Fight → Monster | `MonsterKilledEvent` | Fight 不再 `using Game.Components.Monster` |
| Map → Monster | `PlayerEnterMapEvent` | Map 不再 `using Game.Components.Monster` |
| UserHandler → Bag/Mail/Task/Attribute | `PlayerOnlineEvent` | UserHandler 不再引用四个模块 |
| NetworkComponent → 业务模块 | 回调替代事件 | NetworkComponent 不再直接发布 PlayerOfflineEvent，改用 Action 回调 |

## 事件清单（EventType.cs）

| 事件 | 发布方 | 订阅方（Handlers 文件） |
|------|--------|----------------------|
| ServerInitEvent | AppStart | UserHandler |
| PlayerOnlineEvent (async) | UserHandler | RoleBagHandlers, RoleMailHandlers, RoleTaskHandlers, PlayerHandlers |
| PlayerOfflineEvent | AppStart 回调中 | MapHandlers, FightHandlers, MonsterHandlers, ChatHandlers |
| PlayerEnterMapEvent | MapHandlers | MonsterHandlers |
| PlayerLeaveMapEvent | (预留) | FightHandlers |
| MonsterKilledEvent | FightSystem | MonsterHandlers, RoleTaskHandlers |
| PlayerLevelUpEvent | MonsterSystem | RoleTaskHandlers |
| PlayerExpChangedEvent | MonsterSystem | (预留) |
| PlayerGoldChangedEvent | MonsterSystem | (预留) |
| PlayerItemGainedEvent | MonsterSystem | RoleTaskHandlers |
| PlayerDeathEvent | FightSystem | (预留) |
| MonsterSpawnedEvent | MonsterSystem | (预留) |

## 踩坑

1. `async void` + 同步 `Publish` = 数据没加载完就返回 → 改为 `async Task` + `PublishAsync`
2. ReconnectHandler 中残留旧 AddComponent 代码 → 需同步清理两处（SelectRole + Reconnect）
3. NetworkComponent 不应直接依赖 EventBus 发布事件 → 改用 Action 回调，上层（AppStart）注册回调后发布事件
