---
name: redis-bypass
description: RedisManager 已支持本地内存降级模式，开发阶段无需启动 Redis
type: project
---

RedisManager 在 Redis 连接失败时自动降级为内存存储模式（ConcurrentDictionary）。

**Why:** 开发阶段不需要 Redis，但 UserHandlers 中有 6 处 Redis 调用（ID 生成 + 重连 Token）。改 RedisManager 降级比改业务代码侵入更小。

**How to apply:**
- 内存模式下 Token 存储正常工作（带过期时间）
- 内存模式下角色 ID 用 Interlocked 自增，重启后从 0 开始，开发阶段可接受
- Hash/SortedSet 在内存模式下抛 NotImplementedException，当前未使用
- 日志会打印 "使用本地内存模式" 提示
- 未来需要 Redis 时只需启动 Redis 服务，无需改代码
