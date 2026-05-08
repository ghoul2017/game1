---
paths: ["**/*.cs"]
---

# C# 代码格式

## 大括号

Allman 风格：左大括号始终另起一行，包括空体。

```csharp
// 正确
public interface IEvent
{
}

public void Clear()
{
    _handlers.Clear();
}

private EventBus()
{
}

// 错误：单行体
public void Clear() { _handlers.Clear(); }
public interface IEvent { }
```

## new 表达式

优先使用目标类型 new（`new()`），编译器能推断时省略类型名。

```csharp
public static EventBus Instance { get; } = new();
private readonly Dictionary<int, string> _map = new();
```

构造函数带参数时仍可省略：

```csharp
var snapshot = new List<Action<IEvent>>(list);
```

## 控制流

`lock`、`catch`、`if`、`for`、`foreach` 等关键字后加空格再加括号。

```csharp
lock (list) { ... }
catch (Exception ex) { ... }
```
