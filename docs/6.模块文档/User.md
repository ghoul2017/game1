# User 模块文档

## 概述

User 模块是游戏的入口模块，负责账号认证、角色管理和断线重连。所有其他模块（背包、邮件、任务、战斗等）都依赖 User 模块完成登录和选角后才能初始化。

## 文件清单

| 文件 | 行数 | 职责 |
|------|------|------|
| `server/Game/Components/User/User.cs` | 21 | 账号实体，MongoDB 持久化 |
| `server/Game/Components/User/UserComponent.cs` | 15 | Module 级 Component，全局用户索引 |
| `server/Game/Components/User/UserRole.cs` | 23 | 角色索引实体，MongoDB 持久化 |
| `server/Game/Components/User/UserHandler.cs` | 341 | 消息处理 + 事件订阅 |
| `client/Assets/Scripts/shared/Common/Protos/UserProtos.cs` | 125 | 消息定义（双端共享） |
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
  |                               | 2. 查内存索引，同名用户踢出（todo）
  |                               | 3. 查 MongoDB，账号不存在则自动注册
  |                               | 4. 验证密码
  |                               | 5. 构建内存 User，加入索引
  |                               | 6. 绑定 session.UserId + user.Session
  |                               | 7. 生成重连 Token（5分钟有效）
  |  LoginRes(code,playerId,token)|
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
- **Token 机制**：登录成功后生成 GUID Token，存入 TokenStore（内存），5分钟有效
- **同名踢出**：内存索引中存在同 username 时移除旧记录（实际踢人下线待实现）

### 角色列表（RoleList）

**MsgId**: 10103 → 10104
**消息**: `RoleListReq` → `RoleListRes`

```
客户端                    服务端
  |  RoleListReq            |
  |------------------------>|
  |                         | 1. 校验登录状态
  |                         | 2. 查 MongoDB user_role 集合
  |                         | 3. 转换为 RoleInfo 列表
  |  RoleListRes(roles)     |
  |<------------------------|
```

错误码：

| Code | 含义 |
|------|------|
| 0 | 成功 |
| 1001 | 未登录 |

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
  |<----------------------------|
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

### 选角入场（SelectRole）

**MsgId**: 10107 → 10108
**消息**: `SelectRoleReq(roleId)` → `SelectRoleRes(roleId,name,level,job)`

```
客户端                         服务端
  |  SelectRoleReq(roleId)       |
  |------------------------------>|
  |                               | 1. 校验登录状态
  |                               | 2. 加载 Role 数据
  |                               | 3. 校验角色归属
  |                               | 4. 绑定 session.RoleId
  |                               | 5. 更新 Token（加入 roleId）
  |                               | 6. 创建 PlayerEntity
  |                               | 7. 设置 player.Online.Session
  |                               | 8. 加入在线列表
  |                               | 9. 发布 PlayerOnlineEvent
  |  SelectRoleRes(roleId,...)    |
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

### 断线重连（Reconnect）

**MsgId**: 10109 → 10110
**消息**: `ReconnectReq(token,roleId)` → `ReconnectRes(roleId,name,level)`

```
客户端                       服务端
  |  ReconnectReq(token,roleId) |
  |----------------------------->|
  |                              | 1. 从 TokenStore 验证 Token
  |                              | 2. 解析 token 值：userId 或 userId:roleId
  |                              | 3. 若请求指定 roleId，以请求为准
  |                              | 4. 绑定 session.UserId
  |                              | 5. 若有 roleId：加载角色 → 恢复在线状态
  |                              | 6. 发布 PlayerOnlineEvent
  |                              | 7. 刷新 Token（新Token + 删除旧Token）
  |  ReconnectRes(roleId,...)    |
  |<-----------------------------|
```

错误码：

| Code | 含义 |
|------|------|
| 0 | 成功 |
| 1001 | Token无效或已过期 |
| 1002 | Token数据异常 |

Token 格式演变：
- 登录后：`reconnect:{guid}` → `userId`（5分钟有效）
- 选角后：`reconnect:{guid}` → `userId:roleId`（刷新5分钟）
- 重连后：生成新 GUID Token，删除旧 Token

### 踢人通知（KickNtf）

**MsgId**: 10111
**消息**: `KickNtf(reason)` — 服务端 → 客户端

当前仅定义消息，尚未实现实际踢人逻辑（LoginHandler 中有 todo 注释）。

## 协议汇总

| MsgId | 消息类 | 方向 | 关键字段 |
|-------|--------|------|----------|
| 10101 | LoginReq | C→S | Username, Password |
| 10102 | LoginRes | S→C | Code, Message, PlayerId, Token |
| 10103 | RoleListReq | C→S | （无） |
| 10104 | RoleListRes | S→C | Code, Message, Roles(List\<RoleInfo\>) |
| 10105 | CreateRoleReq | C→S | Name, Job |
| 10106 | CreateRoleRes | S→C | Code, Message, Role(RoleInfo) |
| 10107 | SelectRoleReq | C→S | RoleId |
| 10108 | SelectRoleRes | S→C | Code, Message, RoleId, Name, Level, Job |
| 10109 | ReconnectReq | C→S | Token, RoleId |
| 10110 | ReconnectRes | S→C | Code, Message, RoleId, Name, Level |
| 10111 | KickNtf | S→C | Reason |

共享结构 `RoleInfo`：RoleId, Name, Level, Job — 在角色列表、创建角色、选角响应中复用。

## 数据库操作

| 操作 | 集合 | 方法 | 场景 |
|------|------|------|------|
| 按用户名查询账号 | `user` | `QueryFirstOrDefaultAsync<User>(p => p.Username == username)` | 登录 |
| 插入新账号 | `user` | `InsertAsync(account)` | 自动注册 |
| 查询用户角色列表 | `user_role` | `QueryAsync<UserRole>(r => r.UserId == userId)` | 角色列表、创建角色（数量校验） |
| 校验角色名唯一 | `user_role` | `AnyAsync<UserRole>(r => r.Name == name)` | 创建角色 |
| 插入角色索引 | `user_role` | `InsertAsync(new UserRole {...})` | 创建角色 |
| 插入角色实体 | `role` | `InsertAsync(role)` | 创建角色 |

## 模块依赖

```
User 模块
  ├── 依赖 → MongoDB（User、UserRole 持久化）
  ├── 依赖 → TokenStore（重连 Token）
  ├── 依赖 → IdGenerator（雪花算法 ID）
  ├── 依赖 → EventBus（ServerInitEvent 订阅、PlayerOnlineEvent 发布）
  ├── 依赖 → Player 模块（PlayerEntity.Create、Context.Players）
  │
  └── 被依赖 ← 所有 PlayerEntity 级模块
       ├── Bag（PlayerOnlineEvent 订阅 → 挂载 + 加载）
       ├── Mail（PlayerOnlineEvent 订阅 → 挂载 + 加载）
       ├── Task（PlayerOnlineEvent 订阅 → 挂载 + 加载）
       ├── RoleAttribute（PlayerOnlineEvent 订阅 → 挂载 + 加载）
       └── Map（选角后进入地图）
```

核心依赖链：**User 登录 → User 选角 → PlayerOnlineEvent → 各模块挂载 Component + 加载数据**

## 完整流程

```
客户端启动
    │
    ▼
LoadingPanel → LoginPanel
    │
    │  LoginReq(username, password)
    ▼
LoginHandler ──── 自动注册 / 验证密码 ──── 生成 Token
    │
    │  LoginRes(playerId, token)
    ▼
RoleSelectPanel
    │
    │  RoleListReq
    ▼
RoleListHandler ──── 查询 user_role ──── 返回角色列表
    │
    │  RoleListRes(roles)
    ▼
[创建角色] CreateRoleReq(name, job)
    │                        │
    │  CreateRoleRes(role)   │  或选择已有角色
    ▼                        ▼
SelectRoleReq(roleId)
    │
    ▼
SelectRoleHandler ──── 加载Role → 创建PlayerEntity → 发布PlayerOnlineEvent
    │                                         │
    │                                         ├── BagComponent 挂载 + LoadAsync
    │                                         ├── MailComponent 挂载 + LoadAsync
    │                                         ├── TaskComponent 挂载 + LoadAsync
    │                                         └── RoleAttributeComponent 挂载 + LoadAsync
    │
    │  SelectRoleRes(roleId, name, level, job)
    ▼
GameScene → MainPanel
```

## 已知待办

1. **踢人下线**：LoginHandler 同名用户检测到后仅移除内存索引，未实际发送 KickNtf 和断开旧连接
2. **KickNtf 未接入**：消息已定义，服务端无发送逻辑
3. **账号等级**：User.Level 字段存在但未使用
4. **密码明文**：Password 直接存储和比对，无加密
5. **Token 单点有效**：重连后旧 Token 立即失效，不支持多设备
