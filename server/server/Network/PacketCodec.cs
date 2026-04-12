using System.Buffers.Binary;

namespace Server.Network;

/// <summary>
/// 协议格式: [MsgId:2B][Length:4B][Payload:Length字节]
/// 小端序
/// </summary>
public static class PacketCodec
{
    public const int HeaderSize = 6; // 2 + 4

    public static byte[] Encode(ushort msgId, byte[] payload)
    {
        var packet = new byte[HeaderSize + payload.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, msgId);
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(2), payload.Length);
        payload.CopyTo(packet, HeaderSize);
        return packet;
    }

    /// <summary>
    /// 尝试从缓冲区解码一个完整包。返回 null 表示数据不足。
    /// </summary>
    public static (ushort msgId, byte[] payload, int consumed)? TryDecode(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < HeaderSize)
            return null;

        var msgId = BinaryPrimitives.ReadUInt16LittleEndian(buffer);
        var length = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(2));

        if (length < 0 || length > 1024 * 1024) // 单包最大 1MB
            throw new InvalidDataException($"非法包长度: {length}");

        var totalSize = HeaderSize + length;
        if (buffer.Length < totalSize)
            return null;

        var payload = buffer.Slice(HeaderSize, length).ToArray();
        return (msgId, payload, totalSize);
    }
}
