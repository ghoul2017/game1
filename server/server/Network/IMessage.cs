using ProtoBuf;

namespace Server.Network;

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