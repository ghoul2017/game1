# Misc 模块

目标读者：人类。

## 定位

Misc 模块提供 TCP 连接建立后的握手和心跳 RPC。模块自身不持久化；会话活跃时间保存在共享网络层的 `TcpSession`。

## 核心设计

### 握手是连接确认

TCP 连接建立后，客户端发送 `HandshakeReq` 确认 RPC 链路可用。当前客户端发送空 Token；服务端不做认证，并把 SessionId 和请求 Token 原文写入日志。

### 心跳保持会话

客户端在连接状态下如果 30 秒没有发送其他消息，会发送 `HeartbeatReq`；如果 60 秒没有收到任何消息，会主动断开并进入重连。服务端每 20 秒检查连接，60 秒没有收到消息则断开。

`TcpSession` 收到任意完整消息时都会更新 `LastActiveTime`，不只有心跳会保活；`HeartbeatReqHandler` 还会再次更新一次。

心跳检测定时器通过 `ServerInitEvent` 订阅启动，从 NetworkComponent 构造函数中分离出来，保持网络层的纯粹。

### 保留的文件

| 文件 | 职责 |
|------|------|
| `Handlers/ServerInitHeartbeatEventHandler.cs` | ServerInit → 启动心跳检测定时器 |
| `Handlers/HandshakeReqHandler.cs` | 握手消息处理 |
| `Handlers/HeartbeatReqHandler.cs` | 心跳消息处理 |

## 协议

### 握手

```
客户端                       服务端
  |  HandshakeReq(token)      |
  |-------------------------->|
  |                           | 记录日志，返回成功
  |  HandshakeRes(code,msg)   |
  |<--------------------------|
```

当前仅记录日志，未验证 Token。

### 心跳

```
客户端                       服务端
  |  HeartbeatReq(clientTime) |
  |-------------------------->|
  |                           | 回显 clientTime，写入 Unix 毫秒 serverTime
  |  HeartbeatRes(serverTime, |
  |              clientTime)  |
  |<--------------------------|
```

客户端发送的 `ClientTime` 是 .NET `DateTime.UtcNow.Ticks`，收到响应后用“当前 Ticks - 回显 ClientTime”计算 RTT。`ServerTime` 是 Unix 毫秒，当前客户端不参与 RTT 计算；两个字段的单位不能直接相减。

## 跨模块协作

| 交互方 | 方向 | 内容 |
|--------|------|------|
| Network | 单向 | RpcMessageHandler / TcpSession / DoHeartbeatCheck |
| GameTimer | 单向 | 心跳检测定时器 |

Misc 不被任何业务模块依赖。

## 已知问题与边界

1. **握手未认证**：`HandshakeReqHandler` 不验证 Token，真实身份由后续登录或角色重连处理
2. **Token 原文日志**：Handler 会记录请求 Token；未来若接入真实凭证，必须先调整日志口径
3. **时间单位不同**：ClientTime 使用 .NET Ticks，ServerTime 使用 Unix 毫秒，协议调用方必须按字段语义处理
4. **网络参数未外部配置**：客户端 30 秒心跳和 60 秒接收超时是 `NetClient` 字段默认值，服务端 20/60 秒为代码常量
