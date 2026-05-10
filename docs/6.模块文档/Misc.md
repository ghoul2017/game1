# Misc 模块文档

## 概述

Misc 模块提供网络基础通信功能：握手（Handshake）和心跳（Heartbeat）。握手用于连接建立后的初始验证，心跳用于保持连接活跃和测量网络延迟。同时负责驱动服务端心跳检测定时器（每20秒检查超时连接）。是最小的独立模块，无状态、无持久化。

## 文件清单

| 文件 | 行数 | 职责 |
|------|------|------|
| `server/Game/Components/Misc/MiscHandlers.cs` | ~55 | 握手+心跳处理 + 心跳检测定时器 |
| `client/Assets/Scripts/shared/Common/Protos/MiscProtos.cs` | 39 | 消息定义 |
| `client/Assets/Scripts/shared/Common/MsgIdConst.cs` | — | MsgId 10201-10300 段 |

## Handler 详解

### 事件订阅

| 事件 | 方法 | 说明 |
|------|------|------|
| `ServerInitEvent` | `OnServerInit()` | 服务器初始化时启动心跳检测定时器 |

### 心跳检测定时器

- MiscHandlers.OnServerInit 订阅 ServerInitEvent，启动 GameTimer 每20秒调用 `Context.Network.DoHeartbeatCheck()`
- NetworkComponent.DoHeartbeatCheck() 检查超时60秒的连接并断开
- 心跳定时器原在 NetworkComponent 构造函数中启动，现已分离到 Misc 模块

### HandshakeHandler（MsgId 10201→10202）

```
客户端                       服务端
  |  HandshakeReq(token)      |
  |-------------------------->|
  |                           | 记录日志，返回成功
  |  HandshakeRes(code,msg)   |
  |<--------------------------|
```

- 当前仅记录日志，未做 Token 验证
- 返回固定 Code=0, Message="OK"

### HeartbeatHandler（MsgId 10203→10204）

```
客户端                       服务端
  |  HeartbeatReq(clientTime) |
  |-------------------------->|
  |                           | 更新 session 活跃时间
  |  HeartbeatRes(serverTime, |
  |              clientTime)  |
  |<--------------------------|
```

- `session.UpdateActiveTime()` 刷新会话活跃时间
- 返回服务端时间戳，客户端可计算 RTT 延迟

## 协议汇总

| MsgId | 消息类 | 方向 | 关键字段 |
|-------|--------|------|----------|
| 10201 | HandshakeReq | C→S | Token |
| 10202 | HandshakeRes | S→C | Code, Message |
| 10203 | HeartbeatReq | C→S | ClientTime |
| 10204 | HeartbeatRes | S→C | ServerTime, ClientTime |

## 模块依赖

```
Misc 模块
  ├── 依赖 → Network（RpcMethodHandler、TcpSession、DoHeartbeatCheck）
  ├── 依赖 → GameTimer（心跳检测定时器）
  │
  └── 无被依赖（纯基础设施模块）
```

## 已知待办

1. **握手验证**：HandshakeHandler 未验证 Token，仅记录日志
