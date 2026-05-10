# Network 优化

## 概述
1. 心跳定时器从 NetworkComponent 移到 Misc 模块（消除 GameTimer 依赖）
2. Kick 简化：网络层只管 session.Disconnect()，KickNtf 发送逻辑移到 User 模块
3. NetworkComponent 添加客户端主动连接能力（ConnectAsync），为客户端公用和分布式架构准备

## 1. 心跳定时器移到 Misc 模块

### 分析
NetworkComponent 构造函数中直接启动 GameTimer，导致 shared 层代码依赖游戏层的 GameTimer。应该由外部（Misc 模块）启动定时器，NetworkComponent 只提供 `DoHeartbeatCheck()` 方法。

### 修改文件

### server/Game/Core/Network/NetworkComponent.cs
- 变更类型：修改
- 删除 `_heartbeatTimer` 字段和构造函数中的 `GameTimer.Start(...)` 调用
- `DoHeartbeatCheck()` 保留为 public 方法，供外部定时调用
- 构造函数变为空（或只初始化 MessageRouter）

### server/Game/Components/Misc/MiscHandlers.cs（或新建 MiscSystem.cs）
- 变更类型：修改/新增
- 添加心跳检测定时器启动逻辑：`GameTimer.Start(CheckIntervalSeconds * 1000, () => Context.Network.DoHeartbeatCheck())`
- 时机：在 ServerInitEvent 中启动

## 2. Kick 简化

### 分析
当前 NetworkComponent.Kick() 做两件事：发 KickNtf + Disconnect。用户希望：
- 删除 `NetworkComponent.Kick()` 方法，调用方直接 `session.Disconnect(reason)`
- `LogoutReason` 从 Disconnect 一路透传到 PlayerOfflineEvent，整条下线链路可感知下线原因
- User 模块新增 `KickUser()`：发 KickNtf + Disconnect（业务级踢人）
- KickNtf 消息保留，但发送权归 User 模块

### LogoutReason 透传链路

```
session.Disconnect(reason)
  → OnDisconnected(session, reason)
    → NetworkComponent.SessionDisconnected(session, reason)
      → AppStart.OnSessionDisconnected(session, reason)
        → PlayerOfflineEvent { RoleId, Reason }
```

### 修改文件

### client/Assets/Scripts/shared/Core/Network/TcpSession.cs
- 变更类型：修改
- `Disconnect()` 添加 `LogoutReason reason = LogoutReason.NetDisconnected` 参数（默认值保持兼容）
- Disconnect 内部打日志含 reason
- 保存 reason 到字段 `DisconnectReason`，供外部读取
- `OnDisconnected` 事件签名改为 `Action<TcpSession, LogoutReason>`
- 所有内部 `Disconnect()` 调用无需改动（使用默认值）

### server/Game/Core/Network/NetworkComponent.cs
- 变更类型：修改
- 删除 `Kick()` 方法
- `KickAll()` 方法：遍历调用 `session.Disconnect(LogoutReason.ServerClose)`，不发 KickNtf
- `SessionDisconnected` 回调签名改为 `Action<TcpSession, LogoutReason>`
- `OnSessionDisconnected` 传递 reason

### server/Game/Components/User/UserHandler.cs
- 变更类型：新增方法 + 修改调用
- 新增 `KickUser(TcpSession session, LogoutReason reason)`：
  - `session.Send(new KickNtf { Reason = (int)reason })`
  - `session.Disconnect(reason)`
- LoginHandler: `Context.Server.GetComponent<NetworkComponent>().Kick(oldSession, LogoutReason.AnotherSession)` → `KickUser(oldSession, LogoutReason.AnotherSession)`

### server/Game/AppStart.cs: OnSessionDisconnected
- 变更类型：修改
- 签名改为 `OnSessionDisconnected(TcpSession session, LogoutReason reason)`
- `PlayerOfflineEvent` 增加 `Reason` 字段，传递 reason

### server/Game/Core/Event/EventType.cs: PlayerOfflineEvent
- 变更类型：修改
- 增加 `public LogoutReason Reason;` 字段

### PlayerOfflineEvent 订阅方（无需修改，新增字段不影响现有订阅）
- FightHandlers.OnPlayerOffline
- ChatHandlers.OnPlayerOffline
- MapHandlers.OnPlayerOffline
- MonsterHandlers.OnPlayerOffline
- UserHandler 中的 `Publish(new PlayerOfflineEvent { RoleId = ..., Reason = ... })`

## 3. NetworkComponent 添加客户端连接能力

### 分析
当前 NetworkComponent 只有服务端 Listen/Accept 能力。添加客户端 ConnectAsync 方法，使 NetworkComponent 可用于：
- 客户端主动连接服务器
- 服务端作为客户端连接其他服务（分布式架构：Gateway → GameServer）

TcpSession 已同时支持服务端和客户端场景（Socket 是通用的），只需 NetworkComponent 提供 ConnectAsync 入口。

### 修改文件

### client/Assets/Scripts/shared/Core/Network/NetworkComponent.cs
- 变更类型：修改
- 新增 `public async Task<TcpSession> ConnectAsync(string host, int port)` 方法：
  - 创建 TcpClient/Socket 连接远端
  - 创建 TcpSession 并启动接收
  - 返回 TcpSession 供调用方使用
- 新增客户端连接管理：`_clientSessions` 列表（或复用 _sessionMap）
- 区分 Listen（服务端）和 Connect（客户端）模式：NetworkComponent 可同时作为服务端和客户端使用

### 具体设计

```csharp
// 客户端连接
public async Task<TcpSession> ConnectAsync(string host, int port)
{
    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    await socket.ConnectAsync(host, port);
    var session = new TcpSession(socket, _router);
    _sessionMap[session.SessionId] = session;
    session.OnDisconnected += OnSessionDisconnected;
    session.StartReceive();
    return session;
}
```

- NetworkComponent 不再是纯服务端组件，`[ComponentOf]` 仍为 `Module`（或考虑改为更通用的挂载方式）
- 客户端使用时：`Context.Server.AddComponent<NetworkComponent>()` → `ConnectAsync(host, port)`
- 服务端使用时保持不变：`Listen(host, port)`

### 客户端 NetClient 改造（后续）
- 当前 NetClient 继续存在，后续可逐步迁移为使用 NetworkComponent + TcpSession
- 本次只添加 ConnectAsync 能力，不强制迁移 NetClient

## 数据库变更

无

## 执行顺序

1. NetworkComponent 删除心跳定时器，DoHeartbeatCheck 保留为 public
2. Misc 模块启动心跳检测定时器
3. TcpSession.Disconnect(LogoutReason) 添加参数+日志，删除 NetworkComponent.Kick()
4. User 模块新增 KickUser()（发 KickNtf + Disconnect）
5. UserHandler.LoginHandler 调用改为 KickUser
6. NetworkComponent 添加 ConnectAsync 方法
7. 编译验证

## 验证

- [ ] 服务端编译通过
- [ ] NetworkComponent 不依赖 GameTimer
- [ ] 心跳检测由 Misc 模块驱动
- [ ] NetworkComponent.Kick() 已删除
- [ ] TcpSession.Disconnect(LogoutReason) 在内部打日志
- [ ] LogoutReason 从 Disconnect 透传到 PlayerOfflineEvent
- [ ] KickUser() 在 User 模块，发 KickNtf + Disconnect
- [ ] ConnectAsync 可以创建客户端连接
