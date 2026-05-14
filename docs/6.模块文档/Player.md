# Player 模块

## 定位

Player 模块管理在线玩家的核心实体 `PlayerEntity` 及其生命周期。提供玩家集合管理、会话绑定、数据持久化、广播消息等基础能力，是所有 PlayerEntity 级模块（Bag/Mail/Task/RoleAttribute）的宿主。

## 核心设计

### PlayerEntity 是运行时容器

`PlayerEntity` 在选角入场时创建，下线时销毁。它不持久化自身，而是挂载持久化 Component（RoleDataComponent、RoleAttributeComponent 等），由各 Component 各自持久化到 MongoDB。

### 三套内存索引

`PlayerComponent` 维护 `Players`（roleId）、`PlayersByUserId`（userId）、`PlayersByName`（roleName）三套字典，覆盖所有查找场景。

### PlayerOnlineEvent 驱动级联加载

玩家进入游戏后发布 `PlayerOnlineEvent`，各模块订阅此事件挂载自己的 Component 并加载数据：

```
PlayerOnlineEvent
  ├── PlayerHandlers → RoleAttributeComponent
  ├── RoleBagHandlers → RoleBagComponent
  ├── RoleMailHandlers → RoleMailComponent
  └── RoleTaskHandlers → RoleTaskComponent
```

这实现了 Player 模块不需要知道有哪些子模块——子模块自己订阅、自己加载。

### AddComponent(instance) 统一加载模式

所有 PlayerEntity 级 Component 的上线加载统一使用 `AddComponent<T>(T component)` 挂载 DB 加载实例：

```
var bag = await MongoDb.Instance.LoadAsync<RoleBagComponent>(player.Id);
if (bag == null)
{
    bag = new RoleBagComponent { Id = player.Id, Capacity = 100 };
    await MongoDb.Instance.InsertAsync(bag);
}
bag.RebuildIndex();
player.AddComponent(bag);
```

`AddComponent(instance)` 内部调用 `component.SetParent(this)`，确保 DB 加载实例的 `_parent` 引用正确。`RebuildIndex()` 必须在 `AddComponent` 之前调用。

### 保留的文件

| 文件 | 职责 |
|------|------|
| `PlayerEntity.cs` | 玩家实体（Create 中挂载 PlayerOnlineComponent） |
| `PlayerComponent.cs` | Module 级 Component，全局在线玩家集合 |
| `PlayerSystem.cs` | 玩家管理扩展方法 + EnterGame 逻辑 |
| `PlayerHandlers.cs` | 事件订阅（PlayerOnline → 加载属性） |
| `PlayerOnlineComponent.cs` | PlayerEntity 级，在线状态和会话 |
| `RoleDataComponent.cs` | PlayerEntity 级，角色持久化数据（IEntity） |
| `RoleAttributeComponent.cs` | PlayerEntity 级，战斗属性（IEntity 持久化） |
| `RoleAttributeSystem.cs` | 属性保存扩展方法 |

## EnterGame 流程

```
PlayerSystem.EnterGame(roleId, userId, session, name, level, job)
  │
  ├── 1. PlayerEntity.Create(roleId, userId)  — 创建空 PlayerEntity
  ├── 2. LoadRoleDataAsync(roleId)            — 从 MongoDB 加载
  │     └── 不存在 → 用 UserRole 数据创建并 InsertAsync（首次进入游戏）
  ├── 3. player.AddComponent(roleData)        — 直接挂载 DB 实例
  ├── 4. session.RoleId = roleId              — 绑定会话
  ├── 5. player.Online.Session = session
  ├── 6. TokenSigner.Generate → roleData.LastToken
  ├── 7. AddPlayer(player)                    — 加入在线列表
  ├── 8. session.Send(EnterGameNtf)           — 通知客户端准备就绪
  └── 9. PublishAsync(PlayerOnlineEvent)      — 各模块挂载 Component + 加载数据
```

## Component 分级

| Component | 级别 | 持久化 | MongoDB 集合 |
|-----------|------|--------|-------------|
| PlayerComponent | Module 级 | 否 | — |
| PlayerOnlineComponent | PlayerEntity 级 | 否 | — |
| RoleDataComponent | PlayerEntity 级 | 是 | `role` |
| RoleAttributeComponent | PlayerEntity 级 | 是 | `role_attribute` |

RoleAttributeComponent 的 `ToFightAttribute()` 方法是 Player → Fight 的数据桥梁，将持久化属性转换为战斗属性。

## PlayerRpcMethodHandler

Player 模块提供 `PlayerRpcMethodHandler`，让 Handler 首参直接用 `PlayerEntity` 替代 `TcpSession`：

```csharp
// 普通 Handler
[MessageHandler(typeof(RpcMethodHandler))]
static Task<IMessage> Handler(TcpSession session, Req req, Res res)

// Player Handler — 自动从 session.RoleId 查找 PlayerEntity
[MessageHandler(typeof(PlayerRpcMethodHandler))]
static Task<IMessage> Handler(PlayerEntity player, Req req, Res res)
```

Bag/Mail/Task/Chat/Fight/Map 模块的 Handler 均使用 PlayerRpcMethodHandler。

## 跨模块协作

```
                ┌─────────────┐
                │  MongoDB    │ RoleDataComponent / RoleAttributeComponent 持久化
                └──────┬──────┘
                       │
┌──────────┐    ┌──────▼──────┐
│   User   │───►│   Player    │
│ EnterGame │    │ 集合/会话/广播│
└──────────┘    └──────┬──────┘
                       │ 被所有 PlayerEntity 级模块依赖
          ┌────────────┼────────────┐
          │            │            │
     ┌────▼───┐  ┌────▼───┐  ┌────▼───┐
     │  Bag   │  │  Mail  │  │  Task  │
     └────────┘  └────────┘  └────────┘
```

| 交互方 | 方向 | 内容 |
|--------|------|------|
| User | 单向 | `EnterGameHandler` → `PlayerSystem.EnterGame` |
| Bag/Mail/Task | 双向 | PlayerOnlineEvent 订阅加载；`player.RoleXxx` 访问 |
| Fight | 单向 | `RoleAttributeComponent.ToFightAttribute()` 属性转换 |
| Map | 单向 | `Context.Players.SendToPlayer` 推送通知 |
| Chat | 单向 | `Context.Players.Broadcast` 广播 |

## 已知局限

1. **无下线保存定时器**：仅下线时保存，崩溃可能丢失数据
2. **无玩家数量上限**：无最大在线人数限制
