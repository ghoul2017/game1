# 扩展方法风格 Handler

Handler 使用扩展方法 + 组件模式，业务逻辑内聚到组件扩展类。

## 签名约定

RPC 风格（有响应）：
```csharp
[MessageHandler(typeof(MessageHandler<,>))]
public static async Task<IMessage?> XxxHandler(this TComponent self, TcpSession session, TReq req, TResp res)
```

通知风格（无响应）：
```csharp
[MessageHandler(typeof(MessageNtfHandler<>))]
public static async Task XxxHandler(this TComponent self, TcpSession session, TReq req)
```

## 核心文件

- `MessageHandlerTypes.cs` — 类型标记定义
- `RpcMethodHandler.cs` — RPC 适配器
- `NtfMethodHandler.cs` — 通知适配器
- `MessageRouter.cs` — 扫描 [MessageHandler] 特性的静态方法
