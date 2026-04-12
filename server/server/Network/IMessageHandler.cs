using GameShared.Network;
using ProtoBuf;
using Server.Game.Player;

namespace Server.Network;

/// <summary>
/// 消息处理器接口（Router 使用）。
/// </summary>
public interface IMessageHandler
{
    /// <summary>监听的请求消息ID</summary>
    ushort ReqMsgId { get; }

    /// <summary>处理消息（Router 调用，内部反序列化后交给 ExecuteAsync）</summary>
    Task<IMessage?> HandleAsync(TcpSession session, byte[] payload);
}

/// <summary>
/// 泛型消息处理器基类。自动反序列化请求，子类只需实现业务逻辑。
/// 用法: public class XxxHandler : MessageHandler&lt;XxxReq&gt;
/// </summary>
public abstract class MessageHandler<TReq> : IMessageHandler
    where TReq : class, IMessage, new()
{
    /// <summary>从 Req 类型自动获取 MsgId</summary>
    public ushort ReqMsgId { get; } = new TReq().MsgId;

    /// <summary>业务逻辑 — 接收反序列化后的请求，返回响应（或 null 表示不回复）</summary>
    protected abstract Task<IMessage?> ExecuteAsync(TcpSession session, TReq req);

    /// <summary>IMessageHandler 实现 — 反序列化 + 调用业务逻辑</summary>
    public async Task<IMessage?> HandleAsync(TcpSession session, byte[] payload)
    {
        TReq req;
        using (var ms = new MemoryStream(payload))
            req = Serializer.Deserialize<TReq>(ms);
        return await ExecuteAsync(session, req);
    }
}

public abstract  class PlayerMessageHandler<TReq> :  MessageHandler<TReq>
    where TReq : class, IMessage, new()
{
    protected override async Task<IMessage?> ExecuteAsync(TcpSession session, TReq req)
    {
        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            return null;
        }
        return await HandleAsync(player, req);
    }

    /// <summary>业务逻辑 — 接收 Player 和反序列化后的请求，子类实现</summary>
    protected abstract Task<IMessage?> HandleAsync(Player player, TReq req);
}
