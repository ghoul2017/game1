using System.IO;
using ProtoBuf;

namespace Client.Shared.Messages
{
    /// <summary>
    /// 客户端消息基接口，每个消息需实现 MsgId 并可序列化
    /// </summary>
    public interface IMessage
    {
        ushort MsgId { get; }

        byte[] Serialize()
        {
            using var ms = new MemoryStream();
            Serializer.Serialize(ms, this);
            return ms.ToArray();
        }
    }

    /// <summary>
    /// 消息反序列化工具
    /// </summary>
    public static class MessageSerializer
    {
        public static T Deserialize<T>(byte[] payload) where T : class, IMessage, new()
        {
            using var ms = new MemoryStream(payload);
            return Serializer.Deserialize<T>(ms);
        }

        public static IMessage Deserialize(byte[] payload, ushort msgId)
        {
            var type = MsgTypeRegistry.GetType(msgId);
            if (type == null) return null;
            using var ms = new MemoryStream(payload);
            return (IMessage)Serializer.NonGeneric.Deserialize(type, ms);
        }
    }
}
