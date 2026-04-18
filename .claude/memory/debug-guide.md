---
name: Debug Troubleshooting Guide
description: 问题排查指南、调试技巧、日志位置
type: reference
---

## 网络问题

| 症状 | 可能原因 | 排查步骤 |
|------|----------|----------|
| 连接失败 | 服务端未启动/端口错误 | 检查 `appsettings.json` 端口配置 |
| 断线重连 | 网络波动/服务端重启 | 查看日志 `HandleDisconnect` |
| 消息无响应 | MsgId 不匹配/Handler 未注册 | 检查 MsgId 和 Handler 注册 |
| 粘包/拆包 | 编解码实现错误 | 检查 `PacketCodec.TryDecode` |

## 数据问题

| 症状 | 可能原因 | 排查步骤 |
|------|----------|----------|
| 数据丢失 | Write-Behind 未及时持久化 | 停服前调用 `FlushAllAsync` |
| 加载失败 | 数据库连接异常 | 检查 MySQL 配置和连接 |
| 缓存不一致 | 多实例缓存不同步 | 检查 EntityFactory 缓存逻辑 |

## 性能问题

| 症状 | 可能原因 | 排查步骤 |
|------|----------|----------|
| 高延迟 | 数据库查询慢/GC压力 | 查看日志 Perf 相关 |
| 内存泄漏 | 缓存未清理/事件未取消订阅 | 检查 Dispose/Unsubscribe |
| CPU 高 | 死循环/频繁 GC | 性能分析工具检查 |

## 调试技巧

```csharp
// 服务端：查看消息收发
Log.Debug($"收到消息 MsgId:{msgId} Length:{payload.Length}");
// 客户端：查看网络状态
Debug.Log($"[NetClient] State:{_state} Ping:{PingMs}ms");
// 断点：消息路由 MessageRouter.RouteAsync()
```

## 日志文件位置

| 文件 | 位置 | 说明 |
|------|------|------|
| 服务端日志 | `server/log/game.log` | 主日志 |
| 错误日志 | `server/log/error.log` | 错误单独记录 |
| 客户端日志 | Unity Console | 通过 Debug.Log |

## 关键文件快速定位

| 问题类型 | 检查文件 |
|----------|----------|
| 登录失败 | `server/game/user/UserHandlers.cs`, `server/game/player/PlayerManager.cs` |
| 背包异常 | `server/game/bag/BagManager.cs` |
| 地图加载 | `server/game/map/MapManager.cs`, `server/game/map/MapHandlers.cs` |
| 战斗计算 | `server/game/fight/FightManager.cs`, `server/game/fight/FightCore.cs` |
| 断线重连 | `client/Assets/Scripts/Network/NetClient.cs` |
| 配置表错误 | `server/server/Config/ConfigManager.cs`, `shared/Config/*.cs` |
