using System;
using System.IO;

namespace Client.Network
{
    /// <summary>
    /// 协议格式: [MsgId:2B][Length:4B][Payload:Length字节]
    /// 小端序，与服务端 PacketCodec 保持一致
    /// </summary>
    public static class PacketCodec
    {
        public const int HeaderSize = 6; // 2 + 4
        public const int MaxPacketSize = 1024 * 1024; // 单包最大 1MB

        public static byte[] Encode(ushort msgId, byte[] payload)
        {
            if (payload == null) payload = Array.Empty<byte>();
            var packet = new byte[HeaderSize + payload.Length];
            WriteUInt16LE(packet, 0, msgId);
            WriteInt32LE(packet, 2, payload.Length);
            Buffer.BlockCopy(payload, 0, packet, HeaderSize, payload.Length);
            return packet;
        }

        /// <summary>
        /// 尝试从缓冲区解码一个完整包。
        /// 返回 null 表示数据不足，需要继续接收。
        /// </summary>
        public static DecodeResult TryDecode(byte[] buffer, int offset, int count)
        {
            if (count < HeaderSize)
                return null;

            var msgId = ReadUInt16LE(buffer, offset);
            var length = ReadInt32LE(buffer, offset + 2);

            if (length < 0 || length > MaxPacketSize)
                throw new InvalidDataException($"非法包长度: {length}");

            var totalSize = HeaderSize + length;
            if (count < totalSize)
                return null;

            var payload = new byte[length];
            if (length > 0)
                Buffer.BlockCopy(buffer, offset + HeaderSize, payload, 0, length);

            return new DecodeResult(msgId, payload, totalSize);
        }

        public class DecodeResult
        {
            public readonly ushort MsgId;
            public readonly byte[] Payload;
            public readonly int Consumed;

            public DecodeResult(ushort msgId, byte[] payload, int consumed)
            {
                MsgId = msgId;
                Payload = payload;
                Consumed = consumed;
            }
        }

        #region Little-Endian Read/Write (Unity 4.7.1 compatible)

        internal static ushort ReadUInt16LE(byte[] buf, int offset)
        {
            return (ushort)(buf[offset] | (buf[offset + 1] << 8));
        }

        internal static int ReadInt32LE(byte[] buf, int offset)
        {
            return buf[offset]
                   | (buf[offset + 1] << 8)
                   | (buf[offset + 2] << 16)
                   | (buf[offset + 3] << 24);
        }

        internal static void WriteUInt16LE(byte[] buf, int offset, ushort value)
        {
            buf[offset] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        internal static void WriteInt32LE(byte[] buf, int offset, int value)
        {
            buf[offset] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)((value >> 8) & 0xFF);
            buf[offset + 2] = (byte)((value >> 16) & 0xFF);
            buf[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        #endregion
    }
}
