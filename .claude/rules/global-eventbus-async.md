# EventBus 异步陷阱：async void handler 不会等待完成

`EventBus.Publish` 是同步方法。如果 handler 是 `async void`，执行到第一个 `await` 就返回调用者，后续异步操作变成 fire-and-forget。

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

## 规则

- 需要 `await` 的 handler 返回 `Task`，配合 `PublishAsync` 使用
- 纯同步 handler 返回 `void`，配合 `Publish` 使用
- 永远不要在 `[EventSubscriber]` 中用 `async void`
