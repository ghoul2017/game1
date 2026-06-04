using System;
using System.IO;
using System.Threading.Tasks;
using ProtoBuf;

#nullable enable
namespace Shared.Core.Network
{
    /// <summary>
    /// RPC 消息处理器基类。自动反序列化请求并创建响应对象。
    /// </summary>
    public abstract class RpcMessageHandler<TReq, TRes> : IClassMessageHandler
        where TReq : class, IMessage, new()
        where TRes : class, IMessage, new()
    {
        public Type MessageType => typeof(TReq);

        protected abstract Task ExecuteAsync(TcpSession session, TReq req, TRes res);

        public async Task<IMessage?> HandleAsync(TcpSession session, byte[] payload)
        {
            TReq req;
            using (var ms = new MemoryStream(payload))
                req = Serializer.Deserialize<TReq>(ms);

            var res = new TRes();
            await ExecuteAsync(session, req, res);
            return res;
        }
    }
}
