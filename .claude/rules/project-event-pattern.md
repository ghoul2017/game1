---
paths: ["server/**/*.cs"]
---

# 事件系统使用规则

## 底层模块用回调，不用 EventBus

NetworkComponent、DatabaseManager 等底层基础设施不应依赖 EventBus（高层抽象）。用 `Action<T>` 回调，上层在启动时（AppStart）注册。

```csharp
// 底层：只暴露回调
public class NetworkComponent : Component
{
    public Action<TcpSession, LogoutReason>? SessionDisconnected;
}

// 上层：注册回调
network.SessionDisconnected = (session, reason) =>
{
    // 业务逻辑：保存数据、发布事件、清理状态
};
```

**分层原则**：依赖方向永远是上层 → 下层。EventBus 属于 Core 层，但它是"通知"抽象，底层模块（网络、数据库）用更简单的回调机制即可。

## 事件的定位：通知，不是执行

- 事件是"通知"——"某件事发生了，不知道谁关心"
- 模块内明确知道在干什么的不用事件（如 `Context.Players.GetByRoleId` 查询、`SendToPlayer` 消息发送）
- 跨模块的副作用才用事件（如玩家升级 → 任务进度、怪物击杀 → 掉落、玩家下线 → 各模块清理）

## EventSubscriber 放置位置

事件订阅方法放在各模块的 **Handlers** 文件中，不放在 System 文件中。

```
Components/Xxx/XxxHandlers.cs   ← [EventSubscriber] 放这里
Components/Xxx/XxxSystem.cs     ← 纯静态扩展方法，不放订阅
```

## 同步 vs 异步发布

| 场景 | 方法 | 说明 |
|------|------|------|
| 纯通知，不需要等完成 | `Publish` | MonsterKilledEvent、PlayerOfflineEvent 等 |
| 需要等数据加载完成 | `await PublishAsync` | PlayerOnlineEvent（各模块挂载 Component + LoadAsync） |

Handler 签名：
- 同步：`public static void OnXxx(XxxEvent e)`
- 异步：`public static async Task OnXxx(XxxEvent e)`

**绝对不要用 `async void`**——`Publish` 不会等 `async void` 的 `await` 完成。

```csharp
// 错误：Publish 返回时数据根本没加载完
EventBus.Instance.Publish(new PlayerOnlineEvent { RoleId = roleId });
// handler 内部：
//   async void OnPlayerOnline(...) {
//       var bag = player.AddComponent<RoleBagComponent>();
//       await bag.LoadBagAsync(roleId);  // ← 到这里就返回 Publish 了
//   }

// 正确：用 PublishAsync + Task 返回类型
await EventBus.Instance.PublishAsync(new PlayerOnlineEvent { RoleId = roleId });
// handler 内部：
//   async Task OnPlayerOnline(...) {
//       await bag.LoadBagAsync(roleId);  // ← PublishAsync 会等这个完成
//   }
```
