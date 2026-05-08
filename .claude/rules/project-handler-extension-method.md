---
paths: ["server/**/*.cs"]
---

# Handler 约定

Handler 使用 `[MessageHandler]` 标记的静态方法，通过 MessageRouter 自动扫描注册。

## 签名

RPC 风格（有响应）：
```csharp
[MessageHandler(typeof(RpcMethodHandler))]
public static async Task<IMessage> XxxHandler(TcpSession session, TReq req, TResp res)
```

通知风格（无响应）：
```csharp
[MessageHandler(typeof(NtfMethodHandler))]
public static async Task XxxHandler(TcpSession session, TReq req)
```
