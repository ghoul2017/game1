using Shared.Core.Network;
using Shared.Common.Protos;

namespace Client.Logic
{
    public static class ChatHandlers
    {
        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleChatNtf(TcpSession session, ChatNtf ntf)
        {
            ChatController.Instance.HandleChatNtf(ntf);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleSystemChatNtf(TcpSession session, SystemChatNtf ntf)
        {
            ChatController.Instance.HandleSystemChatNtf(ntf);
        }
    }
}
