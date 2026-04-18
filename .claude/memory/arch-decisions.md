---
name: Architecture Decisions
description: 架构决策记录（ADR），记录关键设计选择的原因和后果
type: project
---

## ADR-001: 使用 TCP 而非 UDP

**决定**：选择 TCP 作为通信协议
**Why**: 游戏逻辑需要可靠传输，简化开发
**How to apply**: 不需要额外处理丢包重传，但实时性略差

## ADR-002: 服务端权威

**决定**：所有游戏逻辑在服务端计算，客户端仅负责 UI 展示
**Why**: 防止作弊，保证公平性
**How to apply**: 所有数值计算、状态变更必须在服务端完成，客户端只发请求、收结果

## ADR-003: 共享消息定义

**决定**：消息定义放在 shared/ 目录，双端共用一份代码
**Why**: 避免双端消息定义不同步
**How to apply**: shared/ 代码受限于 Unity .NET 版本，必须遵守兼容性约束（见 CLAUDE.md）

## ADR-004: Write-Behind 缓存

**决定**：数据先写内存，定时批量持久化（60s）
**Why**: 减少数据库压力
**How to apply**: 服务器崩溃可能丢失少量数据；停服前必须调用 `FlushAllAsync`；变更合并规则：Insert+Update=Insert, Insert+Delete=忽略, Update+Delete=Delete

## 架构分层

```
Game Layer    — Managers(单例) + Handlers(消息) + Models(数据)
Core Layer    — EventBus, EntityFactory, LogManager, MessageRouter, ConfigManager
Network Layer — TcpServer, TcpSession, PacketCodec, NetClient
Engine Layer  — Unity Physics/Rendering | .NET Networking
```

修改定位：新消息→shared/Protos/ + Handler | UI→client/Game/ | 业务→server/game/<module>/ | 网络→shared/Network/ | 框架→server/Core/
