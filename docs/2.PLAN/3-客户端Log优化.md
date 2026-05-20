# 客户端 Log 优化

## 概述
让客户端使用共享日志框架（ILog），新增 UnityAppender 同时输出到 Unity Console 和本地文件。现有客户端 Debug.Log 调用暂不修改，后续改到了再迁移。

## 现状分析

- 共享日志框架（`shared/Core/Log/`）已完整：ILog、LogManager、Logger、ConsoleLogAppender、FileLogAppender
- 服务端已正确初始化 LogManager，客户端未初始化
- 客户端大量使用 `Debug.Log/LogWarning/LogError`，改造成本高，暂不动
- 共享模块（TcpSession、MessageRouter 等）使用 ILog，但在客户端走 ConsoleFallbackLogger 输出到 System.Console（Unity 中不可见）

## 目标

1. 客户端启动时调用 `LogManager.Init()`，配置 UnityAppender + FileAppender
2. 新增 UnityAppender：将 ILog 日志转发到 UnityEngine.Debug.Log/LogWarning/LogError
3. 共享模块的 ILog 日志在客户端同时输出到 Unity Console + 本地文件
4. 现有客户端 Debug.Log 调用暂不迁移

## 新增文件

### `Shared.Core.Log.UnityLogAppender` — `client/Assets/Scripts/shared/Core/Log/UnityLogAppender.cs`
- 用途：将 ILog 日志转发到 UnityEngine.Debug
- 基类/接口：实现 `ILogAppender`
- 字段：
  - `ILogFormatter _formatter` — 格式化器
- 方法：
  - `public UnityLogAppender(ILogFormatter formatter, LogLevel level = LogLevel.Debug)` — 构造
  - `public void Append(LogEntry entry)` — 按 Level 转发到 Debug.Log/LogWarning/LogError/LogException
  - `public void Flush()` — 空实现
  - `public void Close()` — 空实现
- 注意：使用 `#if UNITY_ENGINE` 或条件编译，确保非 Unity 环境不编译。但 shared 代码被双端引用，所以用运行时判断：尝试加载 UnityEngine.Debug 类型，存在则调用，不存在则跳过

### `Client.Core.LogClientInitializer` — `client/Assets/Scripts/Core/LogClientInitializer.cs`
- 用途：客户端日志初始化，在 GameEntry 启动时调用
- 方法：
  - `public static void Init()` — 创建 LogOptions，配置 UnityAppender + FileAppender，调用 LogManager.Init()
- 文件路径配置：`Application.persistentDataPath + "/logs/game.log"`

## 修改文件

### `client/Assets/Scripts/Core/GameEntry.cs`: GameEntry
- 变更类型：修改
- 新增调用：在游戏启动早期调用 `LogClientInitializer.Init()`

### `client/Assets/Scripts/shared/Core/Log/LogManager.cs`: LogManager
- 变更类型：修改
- 修改 `Init` 方法：添加对外部 Appender 注册的支持
- 新增方法：`public static void AddAppender(ILogAppender appender)` — 允许在 Init 后追加 Appender
- 修改 `CreateLogger`：已有 logger 需要感知新 appender（重新创建或直接追加到 _appenders 列表）

## 实现细节

### UnityLogAppender 核心逻辑

```csharp
public void Append(LogEntry entry)
{
    if (entry.Level < Level) return;
    var text = _formatter.Format(entry);
    switch (entry.Level)
    {
        case LogLevel.Debug:
        case LogLevel.Info:
            UnityEngine.Debug.Log(text);
            break;
        case LogLevel.Warn:
            UnityEngine.Debug.LogWarning(text);
            break;
        case LogLevel.Error:
        case LogLevel.Fatal:
            if (entry.Exception != null)
                UnityEngine.Debug.LogError(text + "\n" + entry.Exception);
            else
                UnityEngine.Debug.LogError(text);
            break;
    }
}
```

### LogManager.Init 扩展

在 Init 方法中，增加对 `CustomAppenders` 配置的支持，或提供一个 `AddAppender` 方法让客户端在 Init 后追加 UnityAppender。

更简洁的方案：`LogOptions` 新增 `List<ILogAppender>? CustomAppenders` 字段，Init 时一并添加。

### FileLogAppender 路径

客户端使用 `Application.persistentDataPath` 作为日志目录，需要通过 LogOptions 传入。

## 执行顺序

1. 新建 `UnityLogAppender.cs`（shared/Core/Log/）
2. 修改 `LogManager.cs` — Init 支持自定义 Appender
3. 新建 `LogClientInitializer.cs`（client/Core/）
4. 修改 `GameEntry.cs` — 调用初始化
5. 编译验证

## 验证

- [ ] `dotnet build server/Server.csproj` 编译通过（服务端不受影响）
- [ ] 客户端 Unity 编译通过
- [ ] 共享模块（如 TcpSession）的日志在 Unity Console 可见
- [ ] 日志文件写入 `Application.persistentDataPath/logs/`
