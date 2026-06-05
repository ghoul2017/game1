using System;
using System.Threading.Tasks;

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

    /// <summary>
    /// 继承式消息处理器需要提供可注册的消息类型。
    /// </summary>
    public interface IClassMessageHandler : IMessageHandler
    {
        Type MessageType { get; }
    }

}
