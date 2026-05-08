# 数据所有者模式：引用跟着所属对象走

不要在中心化位置维护业务级映射（如 userId/roleId → session）。让对象自己持有引用，基础设施层只管连接/传输层映射。

## 规则

| 层级 | 维护什么 | 不维护什么 |
|------|---------|-----------|
| 基础设施层（NetworkComponent） | sessionId → TcpSession | userId → session, roleId → session |
| 业务对象（User, PlayerOnlineComponent） | 自己的 Session 引用 | — |

## 反模式：两套独立映射

```csharp
// 反模式：SessionManager 维护 userId → session，NetworkComponent 维护 sessionId → session
// 问题：两套映射容易不同步（disconnect 时忘了清 SessionManager → 映射泄漏）
// 本次重构前 SessionManager._userSessionMap 从未被填充，GetByUserId 永远返回 null
```

## 正确模式

```csharp
// User 持有自己的 session 引用
public class User { public TcpSession? Session { get; set; } }

// PlayerOnlineComponent 持有自己的 session 引用
public class PlayerOnlineComponent : Component { public TcpSession? Session { get; set; } }

// NetworkComponent 只管 sessionId → TcpSession（连接生命周期）
// Kick/SendToPlayer 通过 player.Online.Session?.Send() 访问
```

## 通用原则

如果同一份数据需要在两个地方维护索引，说明索引应该跟着数据所有者走，而不是建第二份映射。
