# Component 分级决定访问路径

Component 的挂载目标（Module 级 vs PlayerEntity 级）决定了访问方式，搞错会导致运行时空引用。

## 规则

| 级别 | 挂载目标 | 访问方式 | Context 属性 |
|------|---------|---------|-------------|
| Module 级 | `Context.Server` | `Context.Players` | 有 |
| PlayerEntity 级 | `PlayerEntity` | `player.Bag` | 无 |

## 常见错误

Component 从 Module 级改为 PlayerEntity 级后，`Context.Bag` 会返回 null（因为不再挂在 Module 上）。必须：
1. 删除 Context 中的便利属性
2. 所有调用方改为 `player.Bag.xxx`
3. 在 UserHandler.SelectRoleHandler 中 AddComponent + 加载数据

```csharp
// 错误：PlayerEntity 级 Component 从 Context 访问
var bag = Context.Bag;  // null，BagComponent 不在 Module 上

// 正确
var bag = player.Bag;   // 从 PlayerEntity 获取
```

## 注意

- 新建 Component 时先确定级别（全局数据→Module 级，玩家数据→PlayerEntity 级）
- 级别变更后必须同步修改 Context.cs 和所有调用方
