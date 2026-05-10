# Player 模块文档

## 概述

Player 模块管理在线玩家的核心实体 PlayerEntity 及其生命周期。提供玩家集合管理、会话绑定、数据持久化、广播消息等基础能力，是所有 PlayerEntity 级模块（Bag/Mail/Task/RoleAttribute）的宿主。

## 文件清单

| 文件 | 行数 | 职责 |
|------|------|------|
| `server/Game/Components/Player/PlayerComponent.cs` | 17 | Module 级 Component，全局在线玩家集合 |
| `server/Game/Components/Player/PlayerEntity.cs` | 40 | 玩家实体，Id=RoleId，挂载所有 PlayerEntity 级 Component |
| `server/Game/Components/Player/PlayerSystem.cs` | ~200 | 玩家管理扩展方法 + EnterGame 逻辑 |
| `server/Game/Components/Player/PlayerHandlers.cs` | ~30 | 事件订阅（PlayerOnline → 加载属性） |
| `server/Game/Components/Player/PlayerOnlineComponent.cs` | 18 | PlayerEntity 级，在线状态和会话 |
| `server/Game/Components/Player/RoleDataComponent.cs` | ~45 | PlayerEntity 级，角色持久化数据（IEntity） |
| `server/Game/Components/Player/RoleAttributeComponent.cs` | 88 | PlayerEntity 级，战斗属性（IEntity 持久化） |
| `server/Game/Components/Player/RoleAttributeSystem.cs` | 20 | 属性保存扩展方法 |

## 数据模型

### RoleDataComponent（角色持久化组件）

```csharp
namespace Game.Components.Player;

[ComponentOf(typeof(PlayerEntity))]
public class RoleDataComponent : Component, IEntity
{
    [BsonId]
    public long Id { get; set; }              // 角色ID（雪花算法）
    public long UserId { get; set; }           // 所属用户ID
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public int Job { get; set; }
    public long Exp { get; set; }
    public int VipLevel { get; set; }
    public long LastLogoutTime { get; set; }
    public long CreateTime { get; set; }

    [BsonIgnore]
    public string LastToken { get; set; } = "";  // 重连Token（运行时）
}
```

- MongoDB 集合名：`role`（与旧 Role 实体 BSON 兼容）
- 实现 IEntity，直接持久化，不再包装 Role 实体
- RoleDataComponent 在 EnterGame 时用 `AddComponent(instance)` 直接挂载 DB 加载实例

## Component

### PlayerComponent（Module 级）

```csharp
[ComponentOf(typeof(Module))]
public class PlayerComponent : Component
{
    public readonly Dictionary<long, PlayerEntity> Players;          // roleId → PlayerEntity
    public readonly Dictionary<long, PlayerEntity> PlayersByUserId;  // userId → PlayerEntity
    public readonly Dictionary<string, PlayerEntity> PlayersByName;  // roleName → PlayerEntity
}
```

- 挂载目标：`Context.Server`
- Context 便利属性：`Context.Players`
- 三套内存索引：按 roleId、userId、roleName 查找

### PlayerOnlineComponent（PlayerEntity 级）

```csharp
[ComponentOf(typeof(PlayerEntity))]
public class PlayerOnlineComponent : Component
{
    public long UserId { get; set; }
    public TcpSession? Session { get; set; }     // 数据所有者模式
    public DateTime LoginTime { get; set; }
    public DateTime LastActiveTime { get; set; }
    public string LoginIp { get; set; } = "";
}
```

- 不持久化，运行时维护会话引用
- 遵循数据所有者模式：PlayerOnlineComponent 自己持有 Session

### RoleAttributeComponent（PlayerEntity 级）

```csharp
[ComponentOf(typeof(PlayerEntity))]
public class RoleAttributeComponent : Component, IEntity
{
    [BsonId] public long Id { get; set; }
    public int Hp { get; set; } = 100;
    public int MaxHp { get; set; } = 100;
    public int Mp { get; set; } = 50;
    public int MaxMp { get; set; } = 50;
    public int PhyAttMin { get; set; } = 10;
    public int PhyAttMax { get; set; } = 12;
    public int MagicAttMin { get; set; } = 5;
    public int MagicAttMax { get; set; } = 7;
    public int PhyDef { get; set; } = 5;
    public int MagicDef { get; set; } = 3;
    public int MoveSpeed { get; set; } = 5;
    public int AttackSpeed { get; set; } = 100;
    public int CriticalRate { get; set; } = 100;    // 万分比（1%）
    public int CriticalDamage { get; set; } = 150;   // 万分比（150%）
    public int Exp { get; set; }
    public int Level { get; set; } = 1;
    public int Gold { get; set; }

    public FightAttribute ToFightAttribute();  // 转换为战斗属性
}
```

- MongoDB 集合名：`role_attribute`
- 实现 IEntity，支持直接持久化

## PlayerEntity

```csharp
public class PlayerEntity : Entity
{
    // 便利属性（访问 Component）
    public PlayerOnlineComponent Online { get; }
    public RoleDataComponent RoleData { get; }
    public RoleAttributeComponent RoleAttribute { get; }
    public RoleBagComponent RoleBag { get; }
    public RoleMailComponent RoleMail { get; }
    public RoleTaskComponent RoleTask { get; }

    // 工厂方法
    public static PlayerEntity Create(long roleId, long userId);
}
```

- Id = RoleId
- 所有 PlayerEntity 级 Component 通过便利属性访问

## System 详解

### PlayerSystem

| 方法 | 签名 | 说明 |
|------|------|------|
| AddPlayer | `AddPlayer(this PlayerComponent, PlayerEntity)` | 添加在线玩家，处理重复登录 |
| RemovePlayer | `RemovePlayer(this PlayerComponent, long roleId)` | 移除在线玩家 |
| GetByRoleId | `GetByRoleId(this PlayerComponent, long roleId) → PlayerEntity?` | 按角色ID查找 |
| GetByUserId | `GetByUserId(this PlayerComponent, long userId) → PlayerEntity?` | 按用户ID查找 |
| GetByRoleName | `GetByRoleName(this PlayerComponent, string name) → PlayerEntity?` | 按角色名查找 |
| IsOnline | `IsOnline(this PlayerComponent, long roleId) → bool` | 是否在线 |
| OnlineCount | `OnlineCount(this PlayerComponent) → int` | 在线人数 |
| GetOnlineRoleIds | `GetOnlineRoleIds(this PlayerComponent) → List<long>` | 所有在线角色ID |
| ForEachPlayer | `ForEachPlayer(this PlayerComponent, Action<PlayerEntity>)` | 遍历在线玩家 |
| Broadcast | `Broadcast(this PlayerComponent, IMessage)` | 全服广播 |
| SendToPlayer | `SendToPlayer(this PlayerComponent, long roleId, IMessage)` | 发送给指定玩家 |
| EnterGame | `EnterGame(this PlayerComponent, roleId, userId, session, name, level, job) → Task<(player, token)?>` | 进入游戏全流程 |
| LoadRoleDataAsync | `LoadRoleDataAsync(this PlayerComponent, long roleId) → Task<RoleDataComponent?>` | 从 MongoDB 加载角色数据 |
| SaveRoleAsync | `SaveRoleAsync(this PlayerComponent, PlayerEntity)` | 保存角色数据 |
| SaveAllRolesAsync | `SaveAllRolesAsync(this PlayerComponent)` | 批量保存所有在线角色 |
| RemoveAll | `RemoveAll(this PlayerComponent)` | 清除所有在线玩家 |

### EnterGame 流程

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
        ├── PlayerHandlers → RoleAttributeComponent
        ├── RoleBagHandlers → RoleBagComponent
        ├── RoleMailHandlers → RoleMailComponent
        └── RoleTaskHandlers → RoleTaskComponent
```

### RoleAttributeSystem

| 方法 | 签名 | 说明 |
|------|------|------|
| SaveAttributeAsync | `SaveAttributeAsync(this RoleAttributeComponent) → Task` | 保存属性到 MongoDB |

## Handler 详解

### 事件订阅

| 事件 | 方法 | 说明 |
|------|------|------|
| PlayerOnlineEvent | `OnPlayerOnline` | 加载/创建 RoleAttributeComponent |

PlayerOnlineEvent 处理流程：
1. 从 `e.Player` 直接获取 PlayerEntity（事件携带，不再查 Context）
2. LoadAsync<RoleAttributeComponent>(player.Id) 从 MongoDB 加载
3. 不存在则创建默认值并 InsertAsync
4. `player.AddComponent(attr)` 直接挂载 DB 实例

## AddComponent(instance) 加载模式

所有 PlayerEntity 级 Component 的上线加载统一使用 `AddComponent<T>(T component)` 挂载 DB 加载实例，不再 `AddComponent<T>()` + 逐字段拷贝：

```csharp
// 统一模式
var bag = await MongoDb.Instance.LoadAsync<RoleBagComponent>(player.Id);
if (bag == null)
{
    bag = new RoleBagComponent { Id = player.Id, Capacity = 100 };
    await MongoDb.Instance.InsertAsync(bag);
}
bag.RebuildIndex();
player.AddComponent(bag);
```

适用于：RoleDataComponent、RoleAttributeComponent、RoleBagComponent、RoleMailComponent、RoleTaskComponent

## 模块依赖

```
Player 模块
  ├── 依赖 → MongoDB（RoleDataComponent、RoleAttributeComponent 持久化）
  ├── 依赖 → EventBus（PlayerOnlineEvent 订阅/发布）
  ├── 依赖 → TokenSigner（EnterGame 生成重连Token）
  ├── 依赖 → Network（PlayerOnlineComponent.Session）
  │
  └── 被依赖 ← 所有其他模块
       ├── User（EnterGameHandler 调用 PlayerSystem.EnterGame）
       ├── Bag（player.RoleBag 访问背包）
       ├── Mail（player.RoleMail 访问邮件）
       ├── Task（player.RoleTask 访问任务）
       ├── Fight（player.RoleAttribute 转换战斗属性）
       ├── Map（Context.Players.SendToPlayer 发消息）
       ├── Chat（Context.Players.Broadcast 广播）
       └── Monster（Context.Players.SendToPlayer 掉落通知）
```

## 数据库操作

| 操作 | 集合 | 方法 | 场景 |
|------|------|------|------|
| 加载角色数据 | `role` | `LoadAsync<RoleDataComponent>(roleId)` | 进入游戏 |
| 创建角色数据 | `role` | `InsertAsync(component)` | 首次进入游戏 |
| 保存角色数据 | `role` | `UpdateAsync(component)` | 下线/定时保存 |
| 加载属性 | `role_attribute` | `LoadAsync<RoleAttributeComponent>(roleId)` | 上线加载 |
| 创建属性 | `role_attribute` | `InsertAsync(component)` | 首次上线 |
| 保存属性 | `role_attribute` | `UpdateAsync(component)` | 属性变更 |

## PlayerRpcMethodHandler

Player 模块还提供了一个重要的网络基础设施——`PlayerRpcMethodHandler`（位于 `server/Game/Core/Network/`），用于适配以 PlayerEntity 为首参的 Handler：

```csharp
// 普通 Handler：首参为 TcpSession
[MessageHandler(typeof(RpcMethodHandler))]
static Task<IMessage> Handler(TcpSession session, Req req, Res res)

// Player Handler：首参为 PlayerEntity，自动从 session.RoleId 查找
[MessageHandler(typeof(PlayerRpcMethodHandler))]
static Task<IMessage> Handler(PlayerEntity player, Req req, Res res)
```

Bag/Mail/Task/Chat/Fight/Map 模块的 Handler 均使用 PlayerRpcMethodHandler。
