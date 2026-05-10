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

## 接口默认方法 → 扩展方法

接口的默认实现（`default interface method`）在类实例上直接调用会编译报 CS1061。必须通过接口类型变量调用，且 Unity Mono 运行时不支持。需要共享逻辑时用扩展方法替代。

```csharp
// 错误：默认接口实现
public interface IEntity
{
    long Id { get; set; }
    public async Task SaveAsync() { await MongoDb.Instance.UpdateAsync(this); }
}
// roleAttr.SaveAsync() → CS1061 编译错误

// 正确：扩展方法
public static class IEntityExtensions
{
    public static async Task SaveAsync(this IEntity entity)
    {
        await MongoDb.Instance.UpdateAsync(entity);
    }
}
// roleAttr.SaveAsync() → OK
```
