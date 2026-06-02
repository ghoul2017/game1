using System;
using System.IO;
using System.Text;

#nullable enable
namespace Shared.Core.Network
{
    /// <summary>
    /// 协议格式: [FrameLength:4B][NameLength:2B][Name:UTF8][Payload]
    /// 小端序，客户端和服务端共享
    /// </summary>
    public static class PacketCodec
    {
        public const int HeaderSize = 4;
        public const int MaxPacketSize = 1024 * 1024; // 单包最大 1MB
        public const int MaxMessageNameBytes = 512;

        public static byte[] Encode(string messageName, byte[] payload)
        {
            if (string.IsNullOrWhiteSpace(messageName))
                throw new InvalidDataException("消息名不能为空");
            if (payload == null) payload = Array.Empty<byte>();
            var nameBytes = Encoding.UTF8.GetBytes(messageName);
            if (nameBytes.Length <= 0 || nameBytes.Length > MaxMessageNameBytes)
                throw new InvalidDataException($"非法消息名长度: {nameBytes.Length}");

            var frameLength = 2 + nameBytes.Length + payload.Length;
            if (frameLength > MaxPacketSize)
                throw new InvalidDataException($"非法包长度: {frameLength}");

            var packet = new byte[HeaderSize + frameLength];
            WriteInt32LE(packet, 0, frameLength);
            WriteUInt16LE(packet, 4, (ushort)nameBytes.Length);
            Buffer.BlockCopy(nameBytes, 0, packet, 6, nameBytes.Length);
            Buffer.BlockCopy(payload, 0, packet, 6 + nameBytes.Length, payload.Length);
            return packet;
        }

        /// <summary>
        /// 尝试从缓冲区解码一个完整包。
        /// 返回 null 表示数据不足，需要继续接收。
        /// </summary>
        public static DecodeResult? TryDecode(byte[] buffer, int offset, int count)
        {
            if (count < HeaderSize)
                return null;

            var frameLength = ReadInt32LE(buffer, offset);

            if (frameLength < 2 || frameLength > MaxPacketSize)
                throw new InvalidDataException($"非法包长度: {frameLength}");

            var totalSize = HeaderSize + frameLength;
            if (count < totalSize)
                return null;

            var nameLength = ReadUInt16LE(buffer, offset + HeaderSize);
            if (nameLength <= 0 || nameLength > MaxMessageNameBytes || nameLength > frameLength - 2)
                throw new InvalidDataException($"非法消息名长度: {nameLength}");

            var nameOffset = offset + HeaderSize + 2;
            var messageName = Encoding.UTF8.GetString(buffer, nameOffset, nameLength);
            var payloadLength = frameLength - 2 - nameLength;
            var payload = new byte[payloadLength];
            if (payloadLength > 0)
                Buffer.BlockCopy(buffer, nameOffset + nameLength, payload, 0, payloadLength);

            return new DecodeResult(messageName, payload, totalSize);
        }

        public class DecodeResult
        {
            public readonly string MessageName;
            public readonly byte[] Payload;
            public readonly int Consumed;

            public DecodeResult(string messageName, byte[] payload, int consumed)
            {
                MessageName = messageName;
                Payload = payload;
                Consumed = consumed;
            }
        }

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
    }
}
