# C# 反射获取属性值

接口定义的属性（如 `IMessage.MsgId`）是实例属性，不能通过 `PropertyInfo.GetValue(null)` 静态获取。

```csharp
// 错误
var msgId = (ushort)reqType.GetProperty("MsgId")?.GetValue(null);

// 正确：先创建实例
var instance = (IMessage)Activator.CreateInstance(reqType);
var msgId = instance.MsgId;
```
