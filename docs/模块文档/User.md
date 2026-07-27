# User 模块

目标读者：人类。

## 定位

User 模块是游戏的入口——账号认证、角色管理、断线重连。所有其他模块都依赖 User 完成登录后才能工作。它管理从"谁连上来了"到"用哪个角色进入游戏"的完整链路。

## 核心设计

### 角色索引

`UserRole` 保存 UserId、Name、Level 和 Job，用于角色列表展示和归属验证。

### 自动注册

账号不存在时自动创建，无需单独注册接口。降低客户端复杂度，适合内网测试环境。

### Token 无状态验证

`TokenSigner` 使用 HMAC-SHA256 签名，启动时随机生成密钥。Token 包含 userId:roleId:timestamp，验证时检查签名和过期时间。角色 Token 还计划通过 `RoleDataComponent.LastToken` 做单次有效校验，但该字段当前标记为 `BsonIgnore`，因此不能把这条重连链路视为已完成能力。

### 保留的文件

| 文件 | 职责 |
|------|------|
| `UserComponent.cs` | Module 级 Component，全局用户索引 |
| `Handlers/ServerInitUserComponentEventHandler.cs` | ServerInit → 挂载 UserComponent |
| `UserHelper.cs` | 踢人下线工具方法 |
| `Handlers/LoginReqHandler.cs` | 登录消息处理 |
| `Handlers/CreateRoleReqHandler.cs` | 创建角色消息处理 |
| `Handlers/EnterGameReqHandler.cs` | 进入游戏消息处理 |
| `Handlers/ReconnectReqHandler.cs` | 断线重连消息处理 |
| `User.cs` | 账号实体 |
| `UserRole.cs` | 角色索引实体 |

## 登录流程

### 客户端主菜单 / 登录页

客户端入口面板是 `client/Assets/Scripts/View/Panels/LoginPanel.cs`。当前主菜单与登录合并为同一界面：左侧展示游戏标题、服务器氛围和构建信息，右侧是“远征者通行证”式登录凭证，保留账号、密码、记住密码、错误提示和进入游戏按钮。

背景图资源位于 `client/Assets/Resources/UI/LoginMenuBackground.png`，由 `LoginPanel` 通过 `Resources.Load<Texture2D>("UI/LoginMenuBackground")` 加载，并使用 `AspectRatioFitter.EnvelopeParent` 按比例覆盖全屏。背景右侧留暗区给登录卡片，底部留给版本和版权文本。

登录控件皮肤也使用图片资源而不是线框拼接：`LoginCardFrame.png`、`LoginInputFrame.png`、`LoginButtonFrame.png`、`LoginServerPanel.png`、`LoginCrest.png` 位于 `client/Assets/Resources/UI/`。`LoginPanel` 运行时加载这些 Texture2D 并通过 `Sprite.Create` 设置到 UGUI `Image`，保留输入框和按钮原有交互逻辑。

登录业务流程没有放进表现层：`LoginPanel` 只负责输入校验触发、连接事件桥接和 UI 状态切换，实际登录状态仍由 `LoginController` 管理。登录成功后继续进入 `RoleSelectPanel`。

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

LoginRes 直接返回 Roles，不需要单独的 RoleList 请求。合并减少了通信次数。

## 创建角色

```
CreateRoleReq(name, job)
  │
  ├── 1. 名称校验（1-12字符）
  ├── 2. 角色数量校验（≤4/用户）
  ├── 3. 名称唯一性校验
  ├── 4. 雪花算法生成 roleId
  └── 5. 创建 UserRole → MongoDB user_role 集合
```

## 进入游戏

```
EnterGameReq(roleId)
  │
  ├── 1. 校验登录状态
  ├── 2. 用 UserRole 验证角色归属
  ├── 3. 调用 PlayerSystem.EnterGame
  │     ├── 绑定会话 + 生成 Token
  │     ├── 注册在线玩家
  │     └── 发送 EnterGameNtf（角色数据）
  ├── 4. EnterGameRes(errorMessage, token)
  └── 5. EnterGameNtf(roleId, name, level, job)
```

EnterGameRes 精简到只含 ErrorMessage + Token。

## 断线重连（当前未闭环）

```
ReconnectReq(token, roleId)
  │
  ├── 1. TokenSigner 验证签名 + 检查过期
  ├── 2. 解析 userId, roleId
  ├── 3. 检查 RoleDataComponent.LastToken 匹配（单次有效）
  ├── 4. 绑定 session.UserId
  └── 5. 生成新 Token，更新 LastToken
```

设计上重连成功后客户端再发送 `EnterGameReq`，走正常进入游戏流程。当前 `LastToken` 不持久化，带 roleId 的 Token 因而无法通过 LastToken 匹配。

## 踢人

同名用户登录时，旧用户被踢下线：发送 `KickNtf(reason)` + 断开连接。KickNtf 发送权归 User 模块。

## TokenSigner

- 启动时随机生成 32 字节 HMAC 密钥，进程内唯一
- `Generate`：对 `userId:roleId:timestamp` 做 HMAC-SHA256 签名
- `Validate`：验证签名 + 检查过期，返回解析结果或 null
- 使用 `CryptographicOperations.FixedTimeEquals` 防时序攻击

## 跨模块协作

| 交互方 | 方向 | 内容 |
|--------|------|------|
| Player | 单向 | `Handlers/EnterGameReqHandler` → `PlayerSystem.EnterGame` |
| MongoDB | 单向 | User / UserRole 持久化 |
| IdGenerator | 单向 | 雪花算法生成 roleId |
| EventBus | 单向 | ServerInitUserComponentEventHandler 订阅 ServerInitEvent |
| TokenSigner | 单向 | Token 签名验证 |

核心依赖链：**User 登录 → UserRole 验证 → PlayerSystem.EnterGame → PlayerOnlineEvent → EnterGameNtf**

## 已知局限

1. **密码明文**：Password 直接存储和比对，无加密
2. **账号等级无用**：User.Level 字段存在但未使用
3. **握手无验证**：HandshakeReq 的 Token 未校验
4. **角色重连未闭环**：LastToken 不持久化，ReconnectReq 的角色 Token 匹配无法跨断线恢复
