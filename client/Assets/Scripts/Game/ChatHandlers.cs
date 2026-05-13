using Shared.Core.Network;
using Shared.Common.Protos;
using Client.Core;

namespace Client.Game
{
    public static class ChatHandlers
    {
        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleChatAck(TcpSession session, ChatAck ack)
        {
            var panel = UIManager.Instance?.GetPanel<MainPanel>();
            if (panel != null) panel.HandleChatAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleChatNtf(TcpSession session, ChatNtf ntf)
        {
            var panel = UIManager.Instance?.GetPanel<MainPanel>();
            if (panel != null) panel.HandleChatNtf(ntf);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleSystemChatNtf(TcpSession session, SystemChatNtf ntf)
        {
            var panel = UIManager.Instance?.GetPanel<MainPanel>();
            if (panel != null) panel.HandleSystemChatNtf(ntf);
        }
    }
}
