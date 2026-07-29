using System;
using System.IO;
using ProtoBuf;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace P0PlatformSmoke
{
    public static class P0PlatformSmokePayload
    {
        public const long ExpectedClientTime = 123456789L;
        public const string ExpectedMessageName = "Misc.HeartbeatReq";

        private const string ExpectedConfigKey = "p0_smoke";
        private const string ExpectedConfigValue = "20260729";

        public static byte[] CreateValidatedPacket()
        {
            HeartbeatReq request = new HeartbeatReq { ClientTime = ExpectedClientTime };
            byte[] payload;
            using (MemoryStream stream = new MemoryStream())
            {
                // 显式闭合泛型，使 IL2CPP 在构建期生成 HeartbeatReq 的 AOT 序列化代码。
                Serializer.Serialize<HeartbeatReq>(stream, request);
                payload = stream.ToArray();
            }

            string messageName = MessageRegistry.GetName(typeof(HeartbeatReq));
            Type registeredType = MessageRegistry.GetType(ExpectedMessageName);

            if (messageName != ExpectedMessageName)
            {
                throw new InvalidDataException($"消息名不一致: {messageName}");
            }

            if (registeredType != typeof(HeartbeatReq))
            {
                throw new InvalidDataException($"反向消息类型不一致: {registeredType.FullName}");
            }

            byte[] packet = PacketCodec.Encode(messageName, payload);
            ValidatePacket(packet);
            return packet;
        }

        public static void ValidatePacket(byte[] packet)
        {
            PacketCodec.DecodeResult result = PacketCodec.TryDecode(packet, 0, packet.Length)
                ?? throw new InvalidDataException("PacketCodec 未返回完整包");

            if (result.Consumed != packet.Length)
            {
                throw new InvalidDataException($"PacketCodec 消费长度不一致: {result.Consumed}/{packet.Length}");
            }

            if (result.MessageName != ExpectedMessageName)
            {
                throw new InvalidDataException($"PacketCodec 消息名不一致: {result.MessageName}");
            }

            using (MemoryStream stream = new MemoryStream(result.Payload, false))
            {
                HeartbeatReq request = Serializer.Deserialize<HeartbeatReq>(stream);
                if (request.ClientTime != ExpectedClientTime)
                {
                    throw new InvalidDataException($"Protobuf 字段不一致: {request.ClientTime}");
                }
            }
        }

        public static void ValidateConfig(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            string normalized = text.Replace("\r\n", "\n").TrimEnd('\n');
            string[] lines = normalized.Split('\n');
            if (lines.Length != 2 || lines[0] != "key,value")
            {
                throw new InvalidDataException("P0 配置表头或行数不一致");
            }

            string[] fields = lines[1].Split(',');
            if (fields.Length != 2 || fields[0] != ExpectedConfigKey || fields[1] != ExpectedConfigValue)
            {
                throw new InvalidDataException("P0 配置固定字段不一致");
            }
        }

        public static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (int index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
