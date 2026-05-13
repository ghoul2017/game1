using ProtoBuf;
using Shared.Core.Network;

namespace Shared.Common.Protos
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
        public ushort MsgId => MsgIdConst.ChatReq;
        [ProtoMember(1)] public int Channel { get; set; }
        [ProtoMember(2)] public string Content { get; set; } = "";
        [ProtoMember(3)] public string TargetName { get; set; } = "";
    }

    [ProtoContract]
    public class ChatAck : IMessage
    {
        public ushort MsgId => MsgIdConst.ChatAck;
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
    }

    #endregion

    #region 聊天消息通知

    [ProtoContract]
    public class ChatNtf : IMessage
    {
        public ushort MsgId => MsgIdConst.ChatNtf;
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
        public ushort MsgId => MsgIdConst.SystemChatNtf;
        [ProtoMember(1)] public string Content { get; set; } = "";
        [ProtoMember(2)] public long SendTime { get; set; }
    }

    #endregion
}
