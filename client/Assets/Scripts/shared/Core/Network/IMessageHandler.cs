using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Shared.Core.Network;
using ProtoBuf;

#nullable enable
namespace Shared.Core.Network
{
    /// <summary>
    /// 消息处理器接口（Router 使用）。
    /// </summary>
    public interface IMessageHandler
    {
        /// <summary>处理消息（Router 调用，内部反序列化后交给 ExecuteAsync）</summary>
        Task<IMessage?> HandleAsync(TcpSession session, byte[] payload);
    }

    public abstract class AMethodHandler : IMessageHandler
    {
        public abstract Task<IMessage?> HandleAsync(TcpSession session, byte[] payload);
        public abstract void Init(MethodInfo method);
    }


    /// <summary>
    /// 泛型消息处理器基类。自动反序列化请求，子类只需实现业务逻辑。
    /// 用法: public class XxxHandler : MessageHandler&lt;XxxReq&gt;
    /// </summary>
    public abstract class MessageHandler<TReq> : IMessageHandler
        where TReq : class, IMessage, new()
    {
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
}
