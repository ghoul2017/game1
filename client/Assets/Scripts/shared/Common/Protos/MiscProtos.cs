using ProtoBuf;
using Shared.Core.Network;

namespace Shared.Common.Protos
{
    // --- 握手 ---

    [ProtoContract]
    public class HandshakeReq : IMessage
    {
        public ushort MsgId => MsgIdConst.HandshakeReq;
        [ProtoMember(1)] public string Token { get; set; } = "";
    }

    [ProtoContract]
    public class HandshakeRes : IMessage
    {
        public ushort MsgId => MsgIdConst.HandshakeRes;
        [ProtoMember(1)] public int Code { get; set; }
        [ProtoMember(2)] public string Message { get; set; } = "";
    }

    // --- 心跳 ---

    [ProtoContract]
    public class HeartbeatReq : IMessage
    {
        public ushort MsgId => MsgIdConst.HeartbeatReq;
        [ProtoMember(1)] public long ClientTime { get; set; }
    }

    [ProtoContract]
    public class HeartbeatRes : IMessage
    {
        public ushort MsgId => MsgIdConst.HeartbeatRes;
        [ProtoMember(1)] public long ServerTime { get; set; }
        [ProtoMember(2)] public long ClientTime { get; set; }
    }
}
