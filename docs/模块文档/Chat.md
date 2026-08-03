# Chat 模块

目标读者：人类。

## 定位

Chat 模块提供世界频道、私聊和服务端系统广播。它是 Module 级运行时组件，不做数据库持久化；世界频道冷却只保存在当前服务进程内。

## 核心设计

### Module 级运行时状态

ChatComponent 挂载在 `Context.Server` 上，只维护世界聊天冷却记录（`Dictionary<long, long>`），不需要持久化。玩家下线时清除冷却。

### 两种频道，两种分发

| 频道 | 常量 | 分发方式 | 特殊规则 |
|------|------|---------|---------|
| World | 1 | `Context.Players.Broadcast` | 3 秒冷却 |
| Private | 3 | `Context.Players.SendToPlayer` | 目标必须在线，不能私聊自己，只推送给接收方 |

系统广播（Channel=2）只能由服务端发起，不通过玩家请求。

### 保留的文件

| 文件 | 职责 |
|------|------|
| `ChatComponent.cs` | Module 级 Component，世界聊天冷却记录 |
| `ChatSystem.cs` | 系统广播和冷却清理组件扩展 |
| `Handlers/ChatReqHandler.cs` | ChatReq 校验、频道业务和消息分发 |
| `Handlers/PlayerOfflineCleanChatCooldownEventHandler.cs` | PlayerOffline → 清理聊天冷却 |

## 聊天处理

```
ChatReq(channel, content, targetName)
  │
  ├── 1. 校验内容非空
  ├── 2. 截断超长内容（MaxContentLength=500）
  ├── 3. 按频道分发：
  │     ├── World：检查冷却 → 更新冷却时间 → 构造 ChatNtf → Broadcast
  │     └── Private：校验目标名非空且不是自己 → 查找在线目标 → 构造 ChatNtf → SendToPlayer
  └── 4. 直接填充 ChatAck，成功时完成消息分发
```

服务端不会把私聊 `ChatNtf` 回发给发送者。客户端 `ChatController.SendChat` 当前只设置频道和内容，没有填写 `TargetName`；因此界面虽然有“私聊”按钮，实际请求会被服务端以“无效的私聊目标”拒绝。

## 跨模块协作

| 交互方 | 方向 | 内容 |
|--------|------|------|
| Player | 单向 | Broadcast / SendToPlayer / GetByRoleName 查找私聊目标 |
| EventBus | 单向 | PlayerOfflineEvent → 清除世界聊天冷却 |

`SendSystemBroadcast` 会向全部在线玩家发送 `SystemChatNtf`，但当前服务端没有业务调用方。

## 已知问题与边界

1. **无聊天记录**：消息不持久化，离线玩家收不到历史消息
2. **客户端私聊未闭环**：没有输入或选择目标名的链路，`TargetName` 始终为空；服务端成功私聊时也不回显给发送者
3. **系统广播未接入**：协议、客户端 Handler 和服务端方法存在，但没有服务端生产者
4. **无频道扩展**：缺少队伍频道、公会频道等
5. **无内容过滤**：没有敏感词屏蔽
