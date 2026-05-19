---
paths: ["**/*.cs"]
---

# 代码规范约定

## C# 格式

- **大括号**：Allman 风格，左大括号另起一行，包括空体
- **new 表达式**：优先目标类型 new（`new()`），编译器能推断时省略类型名
- **控制流**：`lock`、`catch`、`if`、`for` 等关键字后加空格再加括号

```csharp
// 正确
public interface IEvent
{
}

public static EventBus Instance { get; } = new();

lock (list) { ... }

// 错误
public void Clear() { _handlers.Clear(); }
```

## 接口默认方法 → 扩展方法

接口默认实现（`default interface method`）在类实例上直接调用报 CS1061，且 Unity Mono 运行时不支持。用扩展方法替代。

```csharp
// 错误：默认接口实现
public interface IEntity
{
    long Id { get; set; }
    public async Task SaveAsync() { await MongoDb.Instance.UpdateAsync(this); }
}

// 正确：扩展方法
public static class IEntityExtensions
{
    public static async Task SaveAsync(this IEntity entity)
    {
        await MongoDb.Instance.UpdateAsync(entity);
    }
}
```

## Handler 约定

Handler 使用 `[MessageHandler]` 标记的静态方法，通过 MessageRouter 自动扫描注册。

- RPC 风格（有响应）：`static async Task<IMessage> XxxHandler(TcpSession session, TReq req, TResp res)`
- 通知风格（无响应）：`static async Task XxxHandler(TcpSession session, TReq req)`
- 客户端 Handler：static 类 + `[MessageHandler(typeof(NtfMethodHandler))]`，通过 `UIManager.Instance?.GetPanel<T>()` 获取面板

## Component 分级

| 级别 | 挂载目标 | 访问方式 |
|------|---------|---------|
| Module 级 | `Context.Server` | `Context.Players` 等 |
| PlayerEntity 级 | `PlayerEntity` | `player.Bag` 等 |

新建 Component 先确定级别。级别变更后必须同步修改 Context.cs 和所有调用方。PlayerEntity 级的 Component 从 Context 访问返回 null。

## 数据所有者模式

不要在中心化位置维护业务级映射（如 userId → session）。让对象自己持有引用，基础设施层只管连接层映射。

```csharp
// 正确：User/PlayerOnlineComponent 持有自己的 Session 引用
// NetworkComponent 只管 sessionId → TcpSession
```

## 事件系统

- **底层模块用回调，不用 EventBus** — NetworkComponent 等基础设施用 `Action<T>` 回调
- **事件是通知，不是执行** — 模块内明确知道在干什么的不用事件，跨模块副作用才用
- **EventSubscriber 放 Handlers 文件**，不放 System 文件
- **同步 vs 异步**：纯通知用 `Publish`，需要等数据加载用 `await PublishAsync`
- **绝对不要用 `async void`** — `Publish` 不会等 `async void` 的 `await` 完成

## 命名空间冲突

`Game.Components.Task` 与 `System.Threading.Tasks.Task` 冲突。任务相关组件用 `Game.Components.GameTask` 命名空间。新建命名空间前检查是否与 `System.*` 常用类型同名。

## 删除类后清理 using

如果被删除的类是其命名空间下唯一的类，所有 `using ThatNamespace;` 会编译报错。删除文件前搜索该命名空间的引用数量，准备批量清理。

## 静态类不能做泛型参数

`static class` 不能用作泛型参数。System 日志用 `LogManager.GetLogger(nameof(XxxSystem))` 而非 `GetLogger<XxxSystem>()`。

## 反射相关

- 接口定义的属性是实例属性，不能 `PropertyInfo.GetValue(null)` 静态获取，需先创建实例
- 反射调用 `Entity.GetComponent<T>()` 需提供非泛型重载 `GetComponent(Type type)`

## 兼容性

- **ConcurrentDictionary**：用 `TryRemove` 而非 `Remove`（.NET 5+ 才有 Remove 重载）
- **SynchronizationContext**：安装后 await 自动回主线程。Timer 回调、外部线程事件需手动 Post
- **Thread.Sleep(1)**：Windows 下实际 ~15.6ms，轮询场景用 `AutoResetEvent` 替代
- **Unity csproj**：`Assembly-CSharp.csproj` 由 Unity Editor 自动生成，新增 shared 文件后需 Unity 刷新重新生成
- **客户端 .NET 4.7.1**：需要显式 `using System.Linq;`，不能用 C# 10+ 语法和 .NET 5+ API

## ID 生成

所有实体/对象 ID 统一使用 `IdGenerator.Instance.NextId()` 雪花算法，全项目无例外。
