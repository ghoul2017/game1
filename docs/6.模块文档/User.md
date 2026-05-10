# User 模块文档

## 概述

User 模块是游戏的入口模块，负责账号认证、角色管理和断线重连。所有其他模块（背包、邮件、任务、战斗等）都依赖 User 模块完成登录和进入游戏后才能初始化。

## 文件清单

| 文件 | 行数 | 职责 |
|------|------|------|
| `server/Game/Components/User/User.cs` | 21 | 账号实体，MongoDB 持久化 |
| `server/Game/Components/User/UserComponent.cs` | 15 | Module 级 Component，全局用户索引 |
| `server/Game/Components/User/UserRole.cs` | 23 | 角色索引实体，MongoDB 持久化 |
| `server/Game/Components/User/UserHandler.cs` | ~300 | 消息处理 + 事件订阅 |
| `server/Game/Core/TokenSigner.cs` | ~80 | HMAC-SHA256 Token 签名器 |
| `client/Assets/Scripts/shared/Common/Protos/UserProtos.cs` | ~90 | 消息定义（双端共享） |
| `client/Assets/Scripts/shared/Common/MsgIdConst.cs` | — | MsgId 10101-10200 段 |

## 数据模型

### User（账号实体）

```csharp
namespace Game.Components.User;

public class User : IEntity
{
    [BsonId]  public long Id { get; set; }           // 雪花算法 ID
    public string Username { get; set; } = "";        // 用户名（登录账号）
    public string Password { get; set; } = "";        // 密码
    public string Nickname { get; set; } = "";        // 昵称
    public int Level { get; set; }                    // 账号等级
    public DateTime CreateTime { get; set; }          // 注册时间

    [BsonIgnore] public TcpSession? Session { get; set; }  // 运行时会话引用
}
```

- MongoDB 集合名：`user`
- `[BsonIgnore]` 标记的 `Session` 不持久化，运行时由 Handler 维护
- 遵循数据所有者模式：User 自己持有 Session 引用，而非由 SessionManager 集中管理

### UserRole（角色索引）

```csharp
namespace Game.Components.User;

public class UserRole : IEntity
{
    [BsonId]  // _id = RoleId
    public long Id { get => RoleId; set => RoleId = value; }
    public long RoleId { get; set; }     // 角色ID（雪花算法）
    public long UserId { get; set; }     // 所属账号ID
    public string Name { get; set; } = "";  // 角色名
    public int Level { get; set; }       // 角色等级
    public int Job { get; set; }         // 职业
}
```

- MongoDB 集合名：`user_role`
- `_id` 与 `RoleId` 相同，保证按角色ID可直接查询
- 是 `Role`（Player 模块）的轻量索引，用于角色列表展示，避免加载完整 Role 数据

## Component

### UserComponent（Module 级）

```csharp
[ComponentOf(typeof(Module))]
public class UserComponent : Component
{
    public const int MaxRolesPerUser = 4;           // 每账号最多4个角色
    public Dictionary<long, User> Users = new();    // userId → User
    public Dictionary<string, User> Usernames = new(); // username → User
}
```

- 挂载目标：`Context.Server`（通过 `ServerInitEvent` 事件订阅自动注册）
- 两套内存索引：按 userId 和按 username 查找
- 无 Context 便利属性，通过 `Context.Server.GetComponent<UserComponent>()` 访问

## Handler 详解

### 事件订阅

| 事件 | 方法 | 说明 |
|------|------|------|
| `ServerInitEvent` | `ServerInitEvent()` | 服务器初始化时自动挂载 UserComponent |

### 登录（Login）

**MsgId**: 10101 → 10102
**消息**: `LoginReq` → `LoginRes`

```
客户端                        服务端
  |  LoginReq(username,password)  |
  |------------------------------>|
  |                               | 1. 校验是否已登录（session.UserId != 0）
  |                               | 2. 查内存索引，同名用户踢下线（KickNtf + 断开旧连接）
  |                               | 3. 清理旧用户的 PlayerEntity（如有）
  |                               | 4. 查 MongoDB，账号不存在则自动注册
  |                               | 5. 验证密码
  |                               | 6. 构建内存 User，加入索引
  |                               | 7. 绑定 session.UserId + user.Session
  |                               | 8. 生成重连 Token（TokenSigner HMAC签名）
  |                               | 9. 查询角色列表，填入响应
  |  LoginRes(playerId,token,roles)|
  |<------------------------------|
```

错误码：

| Code | 含义 |
|------|------|
| 0 | 成功 |
| 1001 | 已登录，不允许重复登录 |
| 1002 | 密码错误 |

特点：
- **自动注册**：账号不存在时自动创建，无需单独注册接口
- **合并角色列表**：LoginRes 直接返回 Roles，不再需要单独请求 RoleListReq
- **Token 签名**：使用 HMAC-SHA256 签名，无状态验证，无需 TokenStore
- **同名踢出**：检测到同名用户时，调用 NetworkComponent.Kick() 踢旧连接 + 清理 PlayerEntity

### 创建角色（CreateRole）

**MsgId**: 10105 → 10106
**消息**: `CreateRoleReq(name,job)` → `CreateRoleRes(role)`

```
客户端                       服务端
  |  CreateRoleReq(name,job)   |
  |---------------------------->|
  |                             | 1. 校验登录状态
  |                             | 2. 名称校验（1-12字符）
  |                             | 3. 角色数量校验（≤4）
  |                             | 4. 名称唯一性校验
  |                             | 5. 雪花算法生成 roleId
  |                             | 6. 创建 Role 实体 → MongoDB role 集合
  |                             | 7. 创建 UserRole 索引 → MongoDB user_role 集合
  |  CreateRoleRes(role)       |
  |<---------------------------|
```

错误码：

| Code | 含义 |
|------|------|
| 0 | 成功 |
| 1001 | 未登录 |
| 1002 | 角色名长度不合法（1-12字符） |
| 1003 | 角色数量已达上限（4个） |
| 1004 | 角色名已存在 |

写入两个集合：
- `role`：完整角色数据（Player 模块维护）
- `user_role`：轻量索引（本模块维护，用于列表查询）

### 进入游戏（EnterGame）

**MsgId**: 10107 → 10108
**消息**: `EnterGameReq(roleId)` → `EnterGameRes(roleId,name,level,job,token)`

```
客户端                         服务端
  |  EnterGameReq(roleId)        |
  |------------------------------>|
  |                               | 1. 校验登录状态
  |                               | 2. 加载 Role 数据
  |                               | 3. 校验角色归属
  |                               | 4. 绑定 session.RoleId
  |                               | 5. 生成 Token（含roleId），写入 Role.LastToken
  |                               | 6. 创建 PlayerEntity
  |                               | 7. 设置 player.Online.Session
  |                               | 8. 加入在线列表
  |                               | 9. 发布 PlayerOnlineEvent
  |  EnterGameRes(roleId,...,token)|
  |<------------------------------|
```

错误码：

| Code | 含义 |
|------|------|
| 0 | 成功 |
| 1001 | 未登录 |
| 1002 | 角色不存在 |
| 1003 | 不是你的角色 |

关键流程：
1. `PlayerEntity.Create(roleId, userId, role)` — 创建玩家实体
2. `player.Online.Session = session` — 绑定会话（数据所有者模式）
3. `Context.Players.AddPlayer(player)` — 加入全局在线列表
4. `await EventBus.Instance.PublishAsync(PlayerOnlineEvent)` — 异步发布上线事件
5. 各模块（Bag/Mail/Task/RoleAttribute）在 PlayerOnlineEvent 订阅中 AddComponent + LoadAsync
6. `res.Token = token` — 返回新 Token，客户端更新缓存

### 断线重连（Reconnect）

**MsgId**: 10109 → 10110
**消息**: `ReconnectReq(token,roleId)` → `ReconnectRes(code,message,token)`

```
客户端                       服务端
  |  ReconnectReq(token,roleId) |
  |----------------------------->|
  |                              | 1. TokenSigner 验证签名 + 检查过期
  |                              | 2. 解析 userId, roleId
  |                              | 3. 若有 roleId，检查 Role.LastToken 匹配（单次有效）
  |                              | 4. 绑定 session.UserId
  |                              | 5. 生成新 Token，更新 Role.LastToken
  |  ReconnectRes(token)        |
  |<-----------------------------|
  |                              |
  |  客户端收到成功后自动发送      |
  |  EnterGameReq(roleId)       |
  |----------------------------->|
```

错误码：

| Code | 含义 |
|------|------|
| 0 | 成功 |
| 1001 | Token无效或已过期 |
| 1002 | Token已失效（LastToken不匹配） |

Token 机制：
- 使用 HMAC-SHA256 签名，无状态验证（不依赖 TokenStore）
- Token 内容：`Base64(userId:roleId:timestamp):Base64(signature)`
- 进入游戏后 Token 写入 `Role.LastToken`，重连验证时比对实现单次有效
- 重连成功后客户端自动走 EnterGameReq 流程创建 PlayerEntity

### 踢人通知（KickNtf）

**MsgId**: 10111
**消息**: `KickNtf(reason)` — 服务端 → 客户端

触发场景：
- 同名用户登录（`LogoutReason.AnotherSession`）
- 心跳超时（`LogoutReason.HeartbeatTimeout`）
- 服务器关闭（`LogoutReason.ServerClose`）

客户端收到 KickNtf 后：清除 Token/CurrentRole/CurrentMap → 断开连接 → 显示提示

## TokenSigner

```csharp
namespace Game.Core;

public class TokenSigner
{
    public static TokenSigner Instance { get; } = new();
    public string Generate(long userId, long roleId = 0);
    public (long userId, long roleId, long timestamp)? Validate(string token, int expireMinutes = 5);
}
```

- 启动时随机生成 32 字节 HMAC 密钥，进程内唯一
- `Generate`：对 `userId:roleId:timestamp` 做 HMAC-SHA256 签名，返回 `Base64(payload):Base64(signature)`
- `Validate`：验证签名 + 检查过期，返回解析结果或 null
- 使用 `CryptographicOperations.FixedTimeEquals` 防时序攻击

## 协议汇总

| MsgId | 消息类 | 方向 | 关键字段 |
|-------|--------|------|----------|
| 10101 | LoginReq | C→S | Username, Password |
| 10102 | LoginRes | S→C | Code, Message, PlayerId, Token, Roles |
| 10105 | CreateRoleReq | C→S | Name, Job |
| 10106 | CreateRoleRes | S→C | Code, Message, Role |
| 10107 | EnterGameReq | C→S | RoleId |
| 10108 | EnterGameRes | S→C | Code, Message, RoleId, Name, Level, Job, Token |
| 10109 | ReconnectReq | C→S | Token, RoleId |
| 10110 | ReconnectRes | S→C | Code, Message, Token |
| 10111 | KickNtf | S→C | Reason |

共享结构 `RoleInfo`：RoleId, Name, Level, Job — 在登录响应、创建角色响应中复用。

## 数据库操作

| 操作 | 集合 | 方法 | 场景 |
|------|------|------|------|
| 按用户名查询账号 | `user` | `QueryFirstOrDefaultAsync<User>(p => p.Username == username)` | 登录 |
| 插入新账号 | `user` | `InsertAsync(account)` | 自动注册 |
| 查询用户角色列表 | `user_role` | `QueryAsync<UserRole>(r => r.UserId == userId)` | 登录（合并角色列表） |
| 校验角色名唯一 | `user_role` | `AnyAsync<UserRole>(r => r.Name == name)` | 创建角色 |
| 插入角色索引 | `user_role` | `InsertAsync(new UserRole {...})` | 创建角色 |
| 插入角色实体 | `role` | `InsertAsync(role)` | 创建角色 |

## 模块依赖

```
User 模块
  ├── 依赖 → MongoDB（User、UserRole 持久化）
  ├── 依赖 → TokenSigner（重连 Token 签名验证）
  ├── 依赖 → IdGenerator（雪花算法 ID）
  ├── 依赖 → EventBus（ServerInitEvent 订阅、PlayerOnlineEvent 发布）
  ├── 依赖 → Player 模块（PlayerEntity.Create、Context.Players）
  ├── 依赖 → NetworkComponent（踢人下线 Kick）
  │
  └── 被依赖 ← 所有 PlayerEntity 级模块
       ├── Bag（PlayerOnlineEvent 订阅 → 挂载 + 加载）
       ├── Mail（PlayerOnlineEvent 订阅 → 挂载 + 加载）
       ├── Task（PlayerOnlineEvent 订阅 → 挂载 + 加载）
       ├── RoleAttribute（PlayerOnlineEvent 订阅 → 挂载 + 加载）
       └── Map（进入游戏后进入地图）
```

核心依赖链：**User 登录（含角色列表）→ 进入游戏 → PlayerOnlineEvent → 各模块挂载 Component + 加载数据**

## 完整流程

```
客户端启动
    │
    ▼
LoadingPanel → LoginPanel
    │
    │  LoginReq(username, password)
    ▼
LoginHandler ──── 自动注册 / 验证密码 / 踢同名 ──── 生成 Token + 查询角色列表
    │
    │  LoginRes(playerId, token, roles)
    ▼
RoleSelectPanel（角色列表从 LoginRes 缓存读取，无需二次请求）
    │
    │ [创建角色] CreateRoleReq(name, job)
    │                       │
    │  CreateRoleRes(role)  │  或选择已有角色
    ▼                       ▼
EnterGameReq(roleId)
    │
    ▼
EnterGameHandler ──── 加载Role → 创建PlayerEntity → 发布PlayerOnlineEvent
    │                                         │
    │                                         ├── BagComponent 挂载 + LoadAsync
    │                                         ├── MailComponent 挂载 + LoadAsync
    │                                         ├── TaskComponent 挂载 + LoadAsync
    │                                         └── RoleAttributeComponent 挂载 + LoadAsync
    │
    │  EnterGameRes(roleId, name, level, job, token)
    ▼
GameScene → MainPanel
```

断线重连流程：
```
客户端断线 → 自动重连 → Handshake → ReconnectReq(token)
    │
    ▼
ReconnectHandler ──── 验证签名 + LastToken匹配 → 恢复session → 发新Token
    │
    │  ReconnectRes(token)
    ▼
客户端自动发送 EnterGameReq → 走正常进入游戏流程
```

## 已知待办

1. **账号等级**：User.Level 字段存在但未使用
2. **密码明文**：Password 直接存储和比对，无加密
