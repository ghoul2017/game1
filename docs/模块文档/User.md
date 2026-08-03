# User 模块

目标读者：人类。

## 定位

User 模块是游戏的入口——账号认证、角色管理、断线重连。所有其他模块都依赖 User 完成登录后才能工作。它管理从"谁连上来了"到"用哪个角色进入游戏"的完整链路。

## 核心设计

### 角色索引

`UserRole` 保存 UserId、Name、Level 和 Job，用于角色列表展示和归属验证。

### 自动注册

账号不存在时自动创建，无需单独注册接口。降低客户端复杂度，适合内网测试环境。

### 登录 Token 与角色重连令牌

`LoginRes.Token` 仍由 `TokenSigner` 使用进程级 HMAC-SHA256 密钥签发，标识已登录用户，不承担角色重连的单次
失效语义。选角成功后的 `EnterGameRes.Token` 由 `ReconnectTokenRegistry` 随机签发：仅当前服务进程有效，
每个 userId/roleId 只有一个 owner token。在线 token 不计时且不可消费；只有权威 User/Player 会话断线后才进入
5 分钟 Recovery 状态。两类 Token 不持久化，服务重启后均失效。

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
  |                               | 1. 校验当前 session 尚未登录
  |                               | 2. 查 MongoDB；账号不存在则自动注册
  |                               | 3. 验证密码并查询角色列表
  |                               | 4. 凭据成功后才使旧角色重连令牌失效
  |                               | 5. 踢下线并移除旧会话/旧在线玩家（如有）
  |                               | 6. 构建内存 User，绑定新 session
  |                               | 7. 生成用户登录签名 Token
  |                               | 8. 填入角色列表并响应
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
  ├── 3. 名称全局唯一性校验
  ├── 4. 雪花算法生成 roleId
  └── 5. 创建 UserRole → MongoDB user_role 集合
```

当前服务端不校验 `Job` 的合法范围。

## 进入游戏

```
EnterGameReq(roleId)
  │
  ├── 1. 校验登录状态、当前会话和重复入场语义
  ├── 2. 用 UserRole 验证角色归属
  ├── 3. 加载 Player 后再次核验 session 仍是该用户当前会话
  ├── 4. 调用 PlayerSystem.EnterGame
  │     ├── 原子绑定会话 + 签发/复用角色重连令牌
  │     ├── 注册在线玩家
  │     └── 发送 EnterGameNtf（角色数据）
  ├── 5. EnterGameRes(errorMessage, token)
  └── 6. EnterGameNtf(roleId, name, level, job)
```

EnterGameRes 精简到只含 ErrorMessage + Token。

## 断线重连

```
ReconnectReq(token, roleId)
  │
  ├── 1. 要求当前 session 未选角；已认证 pending session 只允许同角色幂等重试
  ├── 2. 校验 token 已由权威断线开启 Recovery 状态且仍在 5 分钟窗口
  ├── 3. 首次消费原子轮换；原窗口内旧 token 重放只返回同一新 token
  ├── 4. 核对当前进程仍有对应 User；竞争重放不得覆盖当前权威会话
  └── 5. 绑定 session.UserId 和 pending role；客户端随后以同一角色执行 EnterGame
```

重连成功后客户端再发送 `EnterGameReq`，走同一加载/绑定流程。Reconnect 响应丢失时，客户端可在原窗口内重放
旧 token 并取得同一个轮换 token；EnterGame 成功后提交该 token，清除旧 token 重放映射和 pending role。
EnterGame 失败保留同角色约束，可重试或再次断线恢复，不能借此切换角色。服务端重启后不能使用该令牌恢复；
活动远征也只承诺同进程恢复。

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
2. **服务端输入校验不足**：登录不校验账号、密码为空或长度，创建角色不校验 Job 范围
3. **角色等级投影未同步**：UserRole.Level 不随 RoleAttribute 等级更新，登录角色列表可能长期显示初始等级
4. **账号运行时副本不完整**：登录重建内存 User 时把 CreateTime 设为当前时间，也没有复制持久化 Level；这些字段当前没有业务消费者
5. **账号等级无用**：User.Level 字段存在但未使用
6. **握手无验证**：HandshakeReq 的 Token 未校验
7. **同进程边界**：角色重连令牌和活动远征都不跨服务进程恢复
