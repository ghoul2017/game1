using ProtoBuf;
using Shared.Core.Network;

namespace Shared.Common.Protos
{

    public static class ChatChannel
    {
        public const int World = 1;
        public const int System = 2;
        public const int Private = 3;
    }



    [ProtoContract]
    [Message("Chat.ChatReq")]
    public class ChatReq : IMessage
    {
        [ProtoMember(1)] public int Channel { get; set; }
        [ProtoMember(2)] public string Content { get; set; } = "";
        [ProtoMember(3)] public string TargetName { get; set; } = "";
    }

    [ProtoContract]
    [Message("Chat.ChatAck")]
    public class ChatAck : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
    }



    [ProtoContract]
    [Message("Chat.ChatNtf")]
    public class ChatNtf : IMessage
    {
        [ProtoMember(1)] public int Channel { get; set; }
        [ProtoMember(2)] public long SenderId { get; set; }
        [ProtoMember(3)] public string SenderName { get; set; } = "";
        [ProtoMember(4)] public string Content { get; set; } = "";
        [ProtoMember(5)] public long SendTime { get; set; }
    }



    [ProtoContract]
    [Message("Chat.SystemChatNtf")]
    public class SystemChatNtf : IMessage
    {
        [ProtoMember(1)] public string Content { get; set; } = "";
        [ProtoMember(2)] public long SendTime { get; set; }
    }

}
