using Shared.Core.Network;
using Shared.Common.Protos;

namespace Client.Logic
{
    public static class BagHandlers
    {
        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleQueryBagAck(TcpSession session, QueryBagAck ack)
        {
            BagController.Instance.HandleQueryBagAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleBagTidyAck(TcpSession session, BagTidyAck ack)
        {
            BagController.Instance.HandleBagTidyAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleBagUseItemAck(TcpSession session, BagUseItemAck ack)
        {
            BagController.Instance.HandleBagUseItemAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleBagItemChangedNtf(TcpSession session, BagItemChangedNtf ntf)
        {
            BagController.Instance.HandleBagItemChangedNtf(ntf);
        }
    }
}
