# Chat 模块文档

## 概述

Chat 模块实现聊天系统，支持世界频道、私聊和系统广播。世界聊天有冷却时间限制，私聊需指定目标玩家名。Module 级 Component，无数据库持久化。

## 文件清单

| 文件 | 行数 | 职责 |
|------|------|------|
| `server/Game/Components/Chat/ChatComponent.cs` | 15 | Module 级 Component，世界聊天冷却记录 |
| `server/Game/Components/Chat/ChatSystem.cs` | 114 | 聊天逻辑扩展方法 + 错误码 |
| `server/Game/Components/Chat/ChatHandlers.cs` | 51 | 消息处理 + 事件订阅 |
| `client/Assets/Scripts/shared/Common/Protos/ChatProtos.cs` | 64 | 消息定义 + 频道常量 |
| `client/Assets/Scripts/shared/Common/MsgIdConst.cs` | — | MsgId 10501-10600 段 |

## Component

### ChatComponent（Module 级）

```csharp
[ComponentOf(typeof(Module))]
public class ChatComponent : Component
{
    public readonly Dictionary<long, long> LastWorldChatTime = new();  // roleId → 上次世界聊天时间（Unix毫秒）
}
```

- 挂载目标：`Context.Server`
- Context 便利属性：`Context.Chat`
- 仅维护世界聊天冷却，不持久化

## System 详解

### ChatSystem

常量：
- `WorldChatCooldownMs = 3000`（3秒冷却）
- `MaxContentLength = 500`（最大消息长度）

| 方法 | 签名 | 说明 |
|------|------|------|
| HandleChat | `HandleChat(this ChatComponent, long senderRoleId, string senderName, int channel, string content, string targetName) → (int code, ChatNtf? ntf, long targetRoleId)` | 处理聊天请求 |
| SendSystemBroadcast | `SendSystemBroadcast(this ChatComponent, string content)` | 发送系统广播 |
| CleanCooldown | `CleanCooldown(this ChatComponent, long roleId)` | 清除冷却（下线时） |

#### HandleChat 逻辑

```
1. 校验内容非空
2. 截断超长内容
3. 按频道分发：
   - World(1)：检查冷却 → 更新冷却时间 → 构造 ChatNtf
   - Private(3)：校验目标名非空且不是自己 → 查找目标玩家 → 构造 ChatNtf
   - 其他：返回 InvalidChannel
4. 返回 (code, ntf, targetRoleId)
```

### 频道常量

```csharp
public static class ChatChannel
{
    public const int World = 1;    // 世界频道
    public const int System = 2;   // 系统广播
    public const int Private = 3;  // 私聊
}
```

### 错误码

| Code | 常量 | 含义 |
|------|------|------|
| 0 | Ok | 成功 |
| 3001 | EmptyContent | 内容为空 |
| 3002 | Cooldown | 世界聊天冷却中 |
| 3003 | InvalidChannel | 无效频道 |
| 3004 | InvalidTarget | 无效私聊目标 |
| 3005 | TargetNotFound | 目标玩家不在线 |
| 3006 | NotLoggedIn | 目标未登录 |

## Handler 详解

### 消息处理

**ChatHandler**（MsgId 10501→10502）

```
ChatReq(channel, content, targetName)
    ↓
1. 调用 Context.Chat.HandleChat()
2. 设置 res.Code
3. 成功时：
   - 世界聊天 → Context.Players.Broadcast(ChatNtf)
   - 私聊 → Context.Players.SendToPlayer(targetRoleId, ChatNtf)
4. 返回 ChatAck
```

### 事件订阅

| 事件 | 方法 | 说明 |
|------|------|------|
| PlayerOfflineEvent | OnPlayerOffline | 清除世界聊天冷却 |

## 协议汇总

| MsgId | 消息类 | 方向 | 关键字段 |
|-------|--------|------|----------|
| 10501 | ChatReq | C→S | Channel, Content, TargetName |
| 10502 | ChatAck | S→C | Code |
| 10503 | ChatNtf | S→C | Channel, SenderId, SenderName, Content, SendTime |
| 10504 | SystemChatNtf | S→C | Content, SendTime |

## 模块依赖

```
Chat 模块
  ├── 依赖 → Player（Context.Players.Broadcast / SendToPlayer / GetByRoleName）
  ├── 依赖 → EventBus（PlayerOfflineEvent 订阅）
  │
  └── 无被依赖（独立模块，其他模块不调用 Chat）
```
