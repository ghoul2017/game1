using ProtoBuf;
using Shared.Core.Network;

namespace Shared.Common.Protos
{
    // --- 握手 ---

    [ProtoContract]
    [Message("Misc.HandshakeReq")]
    public class HandshakeReq : IMessage
    {
        [ProtoMember(1)] public string Token { get; set; } = "";
    }

    [ProtoContract]
    [Message("Misc.HandshakeRes")]
    public class HandshakeRes : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
    }

    // --- 心跳 ---

    [ProtoContract]
    [Message("Misc.HeartbeatReq")]
    public class HeartbeatReq : IMessage
    {
        [ProtoMember(1)] public long ClientTime { get; set; }
    }

    [ProtoContract]
    [Message("Misc.HeartbeatRes")]
    public class HeartbeatRes : IMessage
    {
        [ProtoMember(1)] public long ServerTime { get; set; }
        [ProtoMember(2)] public long ClientTime { get; set; }
    }
}
