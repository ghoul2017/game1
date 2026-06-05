# Chat 模块

## 定位

Chat 模块提供玩家间文字通信——世界频道、私聊、系统广播。它是 Module 级组件，无数据库持久化，纯内存操作。作为最简单的业务模块，也是唯一不被其他模块依赖的独立模块。

## 核心设计

### Module 级、无状态

ChatComponent 挂载在 `Context.Server` 上，只维护世界聊天冷却记录（`Dictionary<long, long>`），不需要持久化。玩家下线时清除冷却。

### 两种频道，两种分发

| 频道 | 常量 | 分发方式 | 特殊规则 |
|------|------|---------|---------|
| World | 1 | `Context.Players.Broadcast` | 3 秒冷却 |
| Private | 3 | `Context.Players.SendToPlayer` | 不能私聊自己 |

系统广播（Channel=2）只能由服务端发起，不通过玩家请求。

### 保留的文件

| 文件 | 职责 |
|------|------|
| `ChatComponent.cs` | Module 级 Component，世界聊天冷却记录 |
| `ChatSystem.cs` | 聊天逻辑扩展方法 |
| `Handlers/ChatReqHandler.cs` | ChatReq 消息处理 |
| `Handlers/PlayerOfflineCleanChatCooldownEventHandler.cs` | PlayerOffline → 清理聊天冷却 |

## 聊天处理

```
ChatReq(channel, content, targetName)
  │
  ├── 1. 校验内容非空
  ├── 2. 截断超长内容（MaxContentLength=500）
  ├── 3. 按频道分发：
  │     ├── World：检查冷却 → 更新冷却时间 → 构造 ChatNtf → Broadcast
  │     └── Private：校验目标名非空且不是自己 → 查找目标 → 构造 ChatNtf → SendToPlayer
  └── 4. 返回 (errorMessage, ntf, targetRoleId)
```

## 跨模块协作

| 交互方 | 方向 | 内容 |
|--------|------|------|
| Player | 单向 | Broadcast / SendToPlayer / GetByRoleName 查找私聊目标 |
| EventBus | 单向 | PlayerOfflineEvent → 清除世界聊天冷却 |

Chat 不被任何其他业务模块依赖——它是终端模块。

## 已知局限

1. **无聊天记录**：消息不持久化，离线玩家收不到历史消息
2. **无频道扩展**：缺少队伍频道、公会频道等
3. **无内容过滤**：没有敏感词屏蔽
