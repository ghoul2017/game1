using ProtoBuf;
using GameShared.Network;

namespace GameShared.Protos
{
    // --- 握手 ---

    [ProtoContract]
    public class HandshakeReq : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.HandshakeReq;
        [ProtoMember(1)] public string Token { get; set; } = "";
    }

    [ProtoContract]
    public class HandshakeResp : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.HandshakeResp;
        [ProtoMember(1)] public int Code { get; set; }
        [ProtoMember(2)] public string Message { get; set; } = "";
    }

    // --- 心跳 ---

    [ProtoContract]
    public class HeartbeatReq : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.HeartbeatReq;
        [ProtoMember(1)] public long ClientTime { get; set; }
    }

    [ProtoContract]
    public class HeartbeatResp : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.HeartbeatResp;
        [ProtoMember(1)] public long ServerTime { get; set; }
        [ProtoMember(2)] public long ClientTime { get; set; }
    }
}
