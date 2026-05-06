---
name: synchronizationcontext-await
description: SynchronizationContext 安装后 await 自动回主线程，无需手动 Post
type: reference
---

# SynchronizationContext 安装后 await 自动回主线程

安装自定义 `SynchronizationContext` 后，所有 `await` 恢复时 .NET 自动调用 `Post()` 回到该线程。已在主线程调用链上的回调不需要手动 Post。

需要手动 Post 的：Timer 回调、外部线程事件等不在主线程调用链上的代码。

**判断方法：** 追踪调用链。从主线程的 async 方法开始，中间全是 await，恢复后自然在主线程。Timer/外部线程触发的回调则不在。

**常见场景：**
- `RouteAsync` → 在主线程调用链上，不需要 Post
- `OnSessionDisconnected` → 由主线程触发，不需要 Post
- `System.Timers.Timer.Elapsed` → Timer 线程池，需要 Post
- `MonsterManager.Start` → 主线程调用，await Task.Delay 恢复后回主线程，不需要 Post
