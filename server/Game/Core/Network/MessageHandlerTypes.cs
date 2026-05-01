using Shared.Core.Network;

namespace Game.Core.Network;

/// <summary>
/// RPC 风格消息处理器基类标记（请求-响应模式）。
/// 用于 [MessageHandler(typeof(MessageHandler&lt;,&gt;))] 标记扩展方法。
/// </summary>
/// <typeparam name="TReq">请求消息类型</typeparam>
/// <typeparam name="TResp">响应消息类型</typeparam>
public abstract class MessageHandler<TReq, TResp>
    where TReq : IMessage
    where TResp : IMessage, new()
{
}

/// <summary>
/// 通知风格消息处理器基类标记（无响应模式）。
/// 用于 [MessageHandler(typeof(MessageNtfHandler&lt;&gt;))] 标记扩展方法。
/// </summary>
/// <typeparam name="TReq">请求消息类型</typeparam>
public abstract class MessageNtfHandler<TReq>
    where TReq : IMessage
{
}
