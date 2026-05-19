using Shared.Core.Network;
using Shared.Common.Protos;

namespace Client.Logic
{
    public static class MailHandlers
    {
        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleQueryMailListAck(TcpSession session, QueryMailListAck ack)
        {
            MailController.Instance.HandleQueryMailListAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleMailReadAck(TcpSession session, MailReadAck ack)
        {
            MailController.Instance.HandleMailReadAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleMailGetItemAck(TcpSession session, MailGetItemAck ack)
        {
            MailController.Instance.HandleMailGetItemAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleMailDeleteAck(TcpSession session, MailDeleteAck ack)
        {
            MailController.Instance.HandleMailDeleteAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleMailNewNtf(TcpSession session, MailNewNtf ntf)
        {
            MailController.Instance.HandleMailNewNtf(ntf);
        }
    }
}
