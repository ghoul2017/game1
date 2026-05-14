# Misc 模块

## 定位

Misc 模块提供网络基础通信——握手和心跳。它是连接层之上的第一个协议处理点，确保连接可用、会话活跃。最小独立模块，无状态、无持久化、无业务逻辑。

## 核心设计

### 握手是连接确认

TCP 连接建立后，客户端发送 `HandshakeReq` 确认连接可用。当前实现仅记录日志，未做 Token 验证——这是一个预留的入口，后续可加入认证逻辑。

### 心跳保持会话

客户端定时发送 `HeartbeatReq`，服务端更新 `session` 活跃时间。服务端每 20 秒检查所有连接，超时 60 秒的连接被断开。

心跳检测定时器通过 `ServerInitEvent` 订阅启动，从 NetworkComponent 构造函数中分离出来，保持网络层的纯粹。

### 保留的文件

| 文件 | 职责 |
|------|------|
| `MiscHandlers.cs` | 握手 + 心跳处理 + 心跳检测定时器 |

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
  |                           | 更新 session 活跃时间
  |  HeartbeatRes(serverTime, |
  |              clientTime)  |
  |<--------------------------|
```

客户端可计算 RTT = serverTime - clientTime。

## 跨模块协作

| 交互方 | 方向 | 内容 |
|--------|------|------|
| Network | 单向 | RpcMethodHandler / TcpSession / DoHeartbeatCheck |
| GameTimer | 单向 | 心跳检测定时器 |

Misc 不被任何业务模块依赖。

## 已知局限

1. **握手未验证**：HandshakeHandler 未验证 Token，仅记录日志
2. **心跳频率固定**：客户端心跳间隔硬编码，不可配置
