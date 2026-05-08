---
always_apply: true
---

# 调试方法论

## 服务端调试

- **日志框架**：`ILog` + `LogManager`，初始化在 `AppStart.RunServerAsync` 中 `LogManager.Init(Context.Config.Log)`
- **日志配置**：`appsettings.json` 的 `Log` 节，支持按 Logger 名称配置级别（前缀匹配）
- **日志输出**：ConsoleAppender（彩色控制台）+ FileAppender（`log/game-YYYYMMDD.log`，错误单独 `log/error-YYYYMMDD.log`，7天滚动清理）
- **日志用法**：`private static readonly ILog Log = LogManager.GetLogger<T>();`（静态类用 `LogManager.GetLogger(nameof(XxxSystem))`）
- **日志级别**：Debug / Info / Warn / Error / Fatal，生产环境建议 Info 以上
- **单线程模型**：所有逻辑在主线程执行，无需担心并发问题，await 自动回主线程
- **网络调试**：通过 `[MessageHandler]` 标记的 Handler 入口追踪请求，`session.UserId` / `session.RoleId` 判断会话状态

## 客户端调试

- **日志框架**：共享 `ILog` + `LogManager`，初始化在 `GameEntry.Start` 中 `LogClientInitializer.Init()`
- **日志输出**：UnityAppender（转发到 UnityEngine.Debug Console）+ FileAppender（`Application.persistentDataPath/logs/`）
- **共享模块日志**：TcpSession、MessageRouter、EventBus 等使用 ILog，初始化后自动输出到 Unity Console + 文件
- **客户端自有日志**：目前仍用 `Debug.Log`，后续改到了再迁移到 ILog
- **日志文件位置**：`Application.persistentDataPath/logs/game-YYYYMMDD.log`
  - Windows: `C:\Users\{user}\AppData\LocalLow\{company}\{product}\logs\`
  - macOS: `~/Library/Logs/{company}/{product}/logs/`
  - Android: `/storage/emulated/0/Android/data/{package}/files/logs/`
  - iOS: `Application/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx/Documents/logs/`

## 数据库调试

- **连接**：MongoDB，配置在 `appsettings.json` 的 `MongoDb` 节
- **统一入口**：`MongoDb.Instance`，所有 CRUD 走这个
- **IEntity 实体**：`LoadAsync<T>(id)` / `InsertAsync(entity)` / `UpdateAsync(entity)` / `DeleteAsync<T>(id)`
- **非 IEntity 类型**：`QueryAsync<T>(filter)` / `QueryFirstOrDefaultAsync<T>(filter)` / `AnyAsync<T>(filter)` / `SaveAsync(document)` / `ReplaceAsync(filter, document)` / `RemoveAsync<T>(filter)`
- **集合名**：自动蛇形转换（`RoleBagComponent` → `role_bag_component`，`UserRole` → `user_role`）
- **反序列化钩子**：`AfterDeserialize()` / `RebuildIndex()` 通过反射自动调用，重建运行时索引

## 常见调试场景

| 场景 | 方法 |
|------|------|
| 请求未到达 Handler | 检查 `MsgIdConst` ID 是否匹配、`[MessageHandler]` 标记是否正确 |
| 数据未持久化 | 检查是否调用了 `UpdateAsync` / `SaveAsync`，检查 `[BsonId]` 标记 |
| Component 字段为 null | 检查 Component 分级（Module vs PlayerEntity）、AddComponent 时机、AfterDeserialize 是否被调用 |
| 事件未触发 | 检查 `EventBus.AutoRegister()` 是否执行、`[EventSubscriber]` 标记、同步 vs 异步发布 |
| 客户端日志看不到 | 确认 `LogClientInitializer.Init()` 已调用，检查 Unity Console 过滤器 |
| 服务端日志级别太高 | 修改 `appsettings.json` 的 `Log.Level` 为 `Debug` |
