你是一个代码生成器。根据参数创建一对 Protobuf 消息类 + Handler + 注册 MsgId。

## 用法

`/newmsg <消息名>`，例如 `/newmsg BagUseItemReq`、`/newmsg LoginReq`

## 参数

消息名通过 $ARGUMENTS 传入。要求包含后缀（Req/Ack/Ntf），PascalCase。

## 工作流程

### 1. 确认参数
- 如果 $ARGUMENTS 为空，提示用法并停止
- 从消息名推断所属系统：例如 `BagUseItemReq` → `Bag` 系统
- 从后缀推断方向：`Req` = 请求（客户端→服务器），`Ack` = 响应（服务器→客户端），`Ntf` = 通知（服务器→客户端）

### 2. 分配 MsgId
- 读取 `server/Network/Messages/MsgId.cs`
- 找到对应系统的 ID 分段（如 Bag = 103xx）
- 在该分段内分配下一个可用的 ID

### 3. 确定目标系统
- 检查 `server/System/{SystemName}/` 是否存在
- 如果存在，在对应的 `Messages/` 和 `Handlers/` 下创建文件
- 如果不存在，在 `server/Network/Messages/` 和 `server/Network/Handlers/` 下创建（待后续迁移）

### 4. 生成消息类

在 `Messages/` 目录创建：

```csharp
using ProtoBuf;

namespace GameServer.Network.Messages;

[ProtoContract]
public class {MessageName} : IMessage  // 或使用项目的消息基类
{
    // TODO: 定义消息字段，用 [ProtoMember(N)] 标注
}
```

### 5. 注册 MsgId

在 `MsgId.cs` 中添加常量：
```csharp
public const ushort {MessageName} = {分配的ID};
```

在消息路由映射中注册（如果项目有集中的路由映射文件）。

### 6. 生成 Handler（仅 Req 类消息需要）

```csharp
using GameServer.Network.Messages;

namespace GameServer.{SystemName}.Handlers;

public class {MessageName}Handler : IMessageHandler<{MessageName}>
{
    private static readonly ILog Log = LogManager.GetLogger<{MessageName}Handler>();

    public async Task HandleAsync(long sessionId, {MessageName} msg)
    {
        // TODO: 实现处理逻辑
        Log.Info("Received {MessageName}");
    }
}
```

### 7. 验证
- `dotnet build` 确保编译通过

## 约束

- 全自动执行，不提问
- 回复使用中文
- 如果消息名以 Ack/Ntf 结尾，只生成消息类，不生成 Handler
- 遵循 CLAUDE.md 的协议格式和命名约定
- MsgId 不能与已有的冲突

$ARGUMENTS
