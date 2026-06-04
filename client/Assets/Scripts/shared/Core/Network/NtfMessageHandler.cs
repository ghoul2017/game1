using System;
using System.IO;
using System.Threading.Tasks;
using ProtoBuf;

#nullable enable
namespace Shared.Core.Network
{
    /// <summary>
    /// 通知消息处理器基类。自动反序列化通知，子类只实现业务逻辑。
    /// </summary>
    public abstract class NtfMessageHandler<TNtf> : IClassMessageHandler
        where TNtf : class, IMessage, new()
    {
        public Type MessageType => typeof(TNtf);

        protected abstract Task ExecuteAsync(TcpSession session, TNtf ntf);

        public async Task<IMessage?> HandleAsync(TcpSession session, byte[] payload)
        {
            TNtf ntf;
            using (var ms = new MemoryStream(payload))
                ntf = Serializer.Deserialize<TNtf>(ms);

            await ExecuteAsync(session, ntf);
            return null;
        }
    }
}
