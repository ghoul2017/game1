# Handler 统一迁移：继承式 → 扩展方法式

从 `MessageHandler<TReq>` 继承式迁移为 static + `[MessageHandler(typeof(RpcMethodHandler))]` 扩展方法式：

## 签名变更

```
// 旧：继承式
public class XxxHandler : MessageHandler<XxxReq>
{
    protected override async Task<IMessage?> ExecuteAsync(TcpSession session, XxxReq req)
    {
        var res = new XxxAck();
        // ...
        return res;
    }
}

// 新：扩展方法式
public static class XxxHandlers
{
    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> XxxHandler(TcpSession session, XxxReq req, XxxAck res)
    {
        // ...
        return res;
    }
}
```

## 关键差异

| 项 | 继承式 | 扩展方法式 |
|---|---|---|
| 类 | 非静态，每个 Handler 一个类 | static，同模块多个 Handler 放一个类 |
| 方法 | 实例 `protected override` | `static` |
| 响应对象 | 方法内 `new` | 参数传入 |
| 返回类型 | `Task<IMessage?>` | `Task<IMessage>` |
| 实例方法 | 可用 | 需改为 static 或提取到 Manager |

## 注意

- MapHandlers 中如果有实例方法（如 SendVisibleObjects），需改为 static 或提取到 Manager
- 多个 Handler 共用的工具方法放 static 类内即可
