using System.IO;
using ProtoBuf;

namespace Shared.Core.Network
{
    /// <summary>
    /// 消息基接口，客户端和服务端共享
    /// </summary>
    public interface IMessage
    {
    }

    /// <summary>
    /// 消息序列化扩展方法
    /// </summary>
    public static class MessageExtensions
    {
        public static byte[] Serialize(this IMessage msg)
        {
            using var ms = new MemoryStream();
            Serializer.Serialize(ms, msg);
            return ms.ToArray();
        }
    }
}
