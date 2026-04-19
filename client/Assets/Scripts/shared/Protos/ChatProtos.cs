using ProtoBuf;
using GameShared.Network;

namespace GameShared.Protos
{
    #region 聊天频道常量

    public static class ChatChannel
    {
        public const int World = 1;
        public const int System = 2;
        public const int Private = 3;
    }

    #endregion

    #region 发送聊天

    [ProtoContract]
    public class ChatReq : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.ChatReq;
        [ProtoMember(1)] public int Channel { get; set; }
        [ProtoMember(2)] public string Content { get; set; } = "";
        [ProtoMember(3)] public string TargetName { get; set; } = "";
    }

    [ProtoContract]
    public class ChatAck : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.ChatAck;
        [ProtoMember(1)] public int Code { get; set; }
    }

    #endregion

    #region 聊天消息通知

    [ProtoContract]
    public class ChatNtf : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.ChatNtf;
        [ProtoMember(1)] public int Channel { get; set; }
        [ProtoMember(2)] public long SenderId { get; set; }
        [ProtoMember(3)] public string SenderName { get; set; } = "";
        [ProtoMember(4)] public string Content { get; set; } = "";
        [ProtoMember(5)] public long SendTime { get; set; }
    }

    #endregion

    #region 系统广播通知

    [ProtoContract]
    public class SystemChatNtf : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.SystemChatNtf;
        [ProtoMember(1)] public string Content { get; set; } = "";
        [ProtoMember(2)] public long SendTime { get; set; }
    }

    #endregion
}
