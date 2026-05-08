# Event 系统重构记录

## 时间
2025-05-08

## 变更
从 `server/Game/Core/Event/`（手动 Subscribe）迁移到 `shared/Core/Event/`（Attribute 自动扫描）。

## 设计决策
- **无适配器层**：事件是内存对象，不需要 MessageRouter 的反序列化适配器，直接 `Delegate.CreateDelegate` 构建委托
- **Publish 同步**：用户明确要求 `Publish` 不用 async，事件分发在主线程同步完成
- **仅 void 处理器**：`[EventSubscriber]` 只支持 `static void Handle(TEvent e)` 返回类型
- **IEvent 空接口**：去掉旧版 `DateTime Timestamp`，按需添加

## 文件位置
- `client/Assets/Scripts/shared/Core/Event/IEvent.cs`
- `client/Assets/Scripts/shared/Core/Event/EventSubscriberAttribute.cs`
- `client/Assets/Scripts/shared/Core/Event/EventBus.cs`

## 踩坑
- 新建 shared 文件后客户端 csproj 不会自动更新，需 Unity 刷新
- 当前 shell 环境只有 .NET 6 SDK，无法编译服务端（需要 .NET 8）
