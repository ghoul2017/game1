# ConcurrentDictionary 兼容性：用 TryRemove 而非 Remove

`ConcurrentDictionary.Remove(key, out value)` 是 .NET 5+ 才有的重载。旧版 BCL（如 Unity 的 .NET Framework 4.7.1）没有此方法，Rider 会误报错。

始终使用 `TryRemove(key, out value)`，所有版本都支持，语义也更清晰。

## 示例
```csharp
// 避免旧版 BCL 解析报错
_timers.TryRemove(handle.Id, out var entry);
```
