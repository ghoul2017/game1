---
paths: ["**/*.cs"]
---

# 静态类不能作为泛型类型参数

`static class` 不能用作泛型参数，包括 `LogManager.GetLogger<T>()` 等。

```csharp
// 错误：静态类不能做泛型参数
private static readonly ILog Log = LogManager.GetLogger<PlayerSystem>();

// 正确：用 nameof
private static readonly ILog Log = LogManager.GetLogger(nameof(PlayerSystem));
```

System 是纯静态扩展方法类，日志必须用 `nameof(XxxSystem)` 而非泛型形式。
