using Shared.Core.Network;
using Shared.Common.Protos;

namespace Client.Logic
{
    public static class MailHandlers
    {
        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleQueryMailListAck(TcpSession session, QueryMailListAck ack)
        {
            var panel = ClientContext.UI?.GetPanel<IMailPanel>();
            if (panel != null) panel.HandleQueryMailListAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleMailReadAck(TcpSession session, MailReadAck ack)
        {
            var panel = ClientContext.UI?.GetPanel<IMailPanel>();
            if (panel != null) panel.HandleMailReadAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleMailGetItemAck(TcpSession session, MailGetItemAck ack)
        {
            var panel = ClientContext.UI?.GetPanel<IMailPanel>();
            if (panel != null) panel.HandleMailGetItemAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleMailDeleteAck(TcpSession session, MailDeleteAck ack)
        {
            var panel = ClientContext.UI?.GetPanel<IMailPanel>();
            if (panel != null) panel.HandleMailDeleteAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleMailNewNtf(TcpSession session, MailNewNtf ntf)
        {
            var panel = ClientContext.UI?.GetPanel<IMailPanel>();
            if (panel != null) panel.HandleMailNewNtf(ntf);
        }
    }
}
