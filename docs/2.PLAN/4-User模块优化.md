# User模块优化

## 概述
优化登录流程（合并 LoginReq + RoleListReq）、重命名选角为"进入游戏"、重构重连流程（剥离 player 部分并走进入游戏流程）、Token 体系改为非对称加密签名写入角色数据、补全踢人逻辑。

## 1. 合并 LoginReq 和 RoleListReq

### 分析
当前流程：LoginReq → LoginRes → RoleListReq → RoleListRes，两次请求串行。
LoginRes 已返回 PlayerId 和 Token，RoleListReq 无参数（只需 session.UserId），可合并到 LoginRes 中。

### 修改文件

### shared/Common/Protos/UserProtos.cs
- 变更类型：修改
- LoginRes 增加字段：`[ProtoMember(5)] public List<RoleInfo> Roles { get; set; } = new List<RoleInfo>();`
- 删除 RoleListReq 类
- 删除 RoleListRes 类

### shared/Common/MsgIdConst.cs
- 变更类型：修改
- 删除 `RoleListReq = 10103` 和 `RoleListRes = 10104` 两个常量

### server/Components/User/UserHandler.cs
- 变更类型：修改
- LoginHandler：登录成功后查询用户角色列表，填充 `res.Roles`
- 删除 RoleListHandler 方法

### client/Assets/Scripts/Login/LoginPanel.cs
- 变更类型：修改
- HandleLoginRes：成功后缓存角色列表到 GameData，跳转 RoleSelectPanel

### client/Assets/Scripts/Login/RoleSelectPanel.cs
- 变更类型：修改
- OnShow：不再发送 RoleListReq，改为从 GameData 读取角色列表
- 删除 RequestRoleList 方法和 HandleRoleListRes 处理器
- RegisterHandlers/UnregisterHandlers 中移除 RoleListRes 相关注册

### client/Assets/Scripts/Game/GameData.cs
- 变更类型：修改
- 新增字段：`public static List<RoleData> Roles { get; set; } = new List<RoleData>();`

## 2. SelectRole 改名为"进入游戏"

### 修改文件

### shared/Common/Protos/UserProtos.cs
- 变更类型：修改
- `SelectRoleReq` → `EnterGameReq`
- `SelectRoleRes` → `EnterGameRes`
- 注释从"选角入场"改为"进入游戏"

### shared/Common/MsgIdConst.cs
- 变更类型：修改
- `SelectRoleReq = 10107` → `EnterGameReq = 10107`
- `SelectRoleRes = 10108` → `EnterGameRes = 10108`

### server/Components/User/UserHandler.cs
- 变更类型：修改
- `SelectRoleHandler` → `EnterGameHandler`
- 方法签名：`static async Task<IMessage> EnterGameHandler(TcpSession session, EnterGameReq req, EnterGameRes res)`

### client/Assets/Scripts/Login/RoleSelectPanel.cs
- 变更类型：修改
- `HandleSelectRoleRes` → `HandleEnterGameRes`
- 发送 `SelectRoleReq` → 发送 `EnterGameReq`
- UI 文案"选角"改为"进入游戏"

## 3. 重连流程：剥离 player 部分，重连后走进入游戏流程

### 分析
当前 ReconnectHandler 直接创建 PlayerEntity + 发布 PlayerOnlineEvent，等于是把"进入游戏"逻辑复制了一份。
重构后：重连只恢复 session 绑定（userId），不创建 PlayerEntity。客户端收到 ReconnectRes 成功后，如果之前有角色，再发 EnterGameReq 走正常进入游戏流程。

### 修改文件

### shared/Common/Protos/UserProtos.cs
- 变更类型：修改
- ReconnectRes：删除 `RoleId`、`Name`、`Level` 字段（不再返回角色数据，由 EnterGameReq 流程获取）
- 保留 `Code`、`Message` 字段

### server/Components/User/UserHandler.cs
- 变更类型：修改
- ReconnectHandler：简化为只验证 Token → 绑定 session.UserId → 刷新 Token → 返回成功
- 删除 PlayerEntity 创建、PlayerOnlineEvent 发布、角色加载逻辑
- 如果 Token 中有 roleId，仅将 roleId 写入新 Token 值，不创建 PlayerEntity

### client/Assets/Scripts/Network/NetClient.cs
- 变更类型：修改
- HandleReconnectRes：成功后设为 Connected 状态，触发 OnReconnected 事件
- 删除从 ReconnectRes 更新 GameData.CurrentRole 的逻辑

### client/Assets/Scripts/Login/LoginPanel.cs 或新增重连恢复逻辑
- 变更类型：修改
- 重连成功后，如果 GameData.CurrentRole 不为空，自动发送 EnterGameReq（进入游戏）
- 如果 GameData.CurrentRole 为空（未选角断线），跳到 RoleSelectPanel

## 4. Token 体系重构：非对称加密签名 + 记录到角色上

### 设计
- 使用 HMAC-SHA256 对称签名（非对称在此场景无额外收益，HMAC 已足够安全且更简单）
- 签名内容：`userId:roleId:timestamp`
- 密钥：服务端启动时随机生成，进程内唯一
- Token 格式：`Base64(签名内容):Base64(签名)` → 客户端透明传输
- 优点：无状态，不需要 TokenStore 存储和清理
- 验证：解析 → 验证签名 → 检查 timestamp 是否在有效期（5分钟）→ 检查 userId:roleId 匹配
- 单次有效：在 Role 上记录 `LastToken` 字段，重连成功后更新 Token，旧 Token 即失效

### 新增文件

### server/Core/TokenSigner.cs — Game.Core
- 用途：Token 签名和验证
- 字段：
  - `byte[] _key` — HMAC 密钥（启动时随机生成）
- 方法：
  - `public string Generate(long userId, long roleId)` — 生成签名 Token
  - `public (long userId, long roleId, long timestamp)? Validate(string token, int expireMinutes = 5)` — 验证并解析，过期/无效返回 null

### 修改文件

### server/Components/Player/Role.cs
- 变更类型：修改
- 新增字段：`public string LastToken { get; set; } = "";` — 最近一次重连 Token

### server/Components/User/UserHandler.cs
- 变更类型：修改
- LoginHandler：用 TokenSigner 替换 TokenStore 调用
- EnterGameHandler：用 TokenSigner 生成 Token，写入 Role.LastToken
- ReconnectHandler：用 TokenSigner 验证签名 + 检查 Role.LastToken 匹配 + 更新 LastToken

### server/Core/TokenStore.cs
- 变更类型：删除（不再需要）

### server/Program.cs 或 Context.cs
- 变更类型：修改
- 删除 TokenStore 相关初始化代码（如有）

## 5. 踢人下线

### 分析
当前 LoginHandler 检测到同名用户在线时，只移除内存索引，未发送 KickNtf 和断开旧连接。
NetworkComponent.Kick() 方法已实现（发送 KickNtf + Disconnect），直接调用即可。

### 修改文件

### server/Components/User/UserHandler.cs
- 变更类型：修改
- LoginHandler：同名用户检测到后，调用 `Context.Server.GetComponent<NetworkComponent>().Kick(oldUser.Session, LogoutReason.AnotherSession)` 踢下线
- 同时需要清理旧用户的 PlayerEntity（如果已选角）：从 PlayerComponent 移除、发布 PlayerOfflineEvent

### server/Components/Player/PlayerSystem.cs
- 变更类型：确认
- 确认 PlayerOfflineEvent 处理逻辑存在，能正确清理玩家数据

## 6. KickNtf 接入验证

### 分析
KickNtf 已定义，NetworkComponent.Kick() 已实现发送，客户端 NetClient.HandleKickNtf 已实现接收处理。
唯一缺失的是服务端实际调用 Kick 的地方（第 5 步修复后即完成）。

### 验证
- [ ] 同名登录触发 Kick → 旧客户端收到 KickNtf → 断线 → 显示提示
- [ ] 心跳超时触发 Kick → 已有（NetworkComponent.DoHeartbeatCheck）

## 消息定义

| MsgId | 消息类 | 方向 | 变更 | 关键字段 |
|-------|--------|------|------|----------|
| 10101 | LoginReq | C→S | 不变 | Username, Password |
| 10102 | LoginRes | S→C | 新增 Roles 字段 | Code, Message, PlayerId, Token, Roles |
| 10103 | ~~RoleListReq~~ | — | 删除 | — |
| 10104 | ~~RoleListRes~~ | — | 删除 | — |
| 10107 | EnterGameReq | C→S | 重命名(原SelectRoleReq) | RoleId |
| 10108 | EnterGameRes | S→C | 重命名(原SelectRoleRes) | Code, Message, RoleId, Name, Level, Job |
| 10109 | ReconnectReq | C→S | 不变 | Token, RoleId |
| 10110 | ReconnectRes | S→C | 简化(删字段) | Code, Message |
| 10111 | KickNtf | S→C | 不变 | Reason |

## 数据库变更

- 修改文档：`Role` 增加字段 `LastToken`（string, 默认 ""）
- 无新集合

## 执行顺序

1. 新增 TokenSigner.cs
2. Role.cs 新增 LastToken 字段
3. 修改 UserProtos.cs（合并消息、重命名、简化 ReconnectRes）
4. 修改 MsgIdConst.cs（删除 RoleList、重命名 SelectRole）
5. 修改 UserHandler.cs（LoginHandler 合并角色列表 + 踢人 + TokenSigner；EnterGameHandler 重命名 + TokenSigner；ReconnectHandler 简化；删除 RoleListHandler）
6. 修改客户端 GameData.cs（新增 Roles 缓存）
7. 修改客户端 LoginPanel.cs（LoginRes 处理角色列表）
8. 修改客户端 RoleSelectPanel.cs（从缓存读取角色列表 + 重命名 + 重连后自动进入游戏）
9. 修改客户端 NetClient.cs（HandleReconnectRes 简化）
10. 删除 TokenStore.cs
11. 编译验证

## 验证

- [ ] 服务端编译通过
- [ ] 客户端编译通过
- [ ] 登录后直接返回角色列表，无需二次请求
- [ ] 选角流程 UI 文案改为"进入游戏"
- [ ] 重连后自动走进入游戏流程
- [ ] Token 使用 HMAC 签名，无状态验证
- [ ] 同名登录踢旧连接，旧客户端收到 KickNtf
