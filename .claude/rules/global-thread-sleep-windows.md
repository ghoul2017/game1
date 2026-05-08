---
paths: ["server/**/*.cs"]
---

# Windows 下 Thread.Sleep(1) 实际休眠 ~15.6ms

Windows 默认时钟分辨率 ~15.6ms，`Thread.Sleep(1)` 远超 1ms。轮询场景用 `AutoResetEvent` 替代。

```csharp
// 错误：实际 15.6ms 延迟
while (running) { if (!queue.TryDequeue(...)) Thread.Sleep(1); }

// 正确：信号唤醒，微秒级响应
while (running) { while (queue.TryDequeue(...)) { ... } wakeSignal.WaitOne(); }
// Post 时：queue.Enqueue(...); wakeSignal.Set();
```
