using Shared.Core.Network;
using Shared.Common.Protos;

namespace Client.Logic
{
    public static class BagHandlers
    {
        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleQueryBagAck(TcpSession session, QueryBagAck ack)
        {
            var panel = ClientContext.UI?.GetPanel<IBagPanel>();
            if (panel != null) panel.HandleQueryBagAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleBagTidyAck(TcpSession session, BagTidyAck ack)
        {
            var panel = ClientContext.UI?.GetPanel<IBagPanel>();
            if (panel != null) panel.HandleBagTidyAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleBagUseItemAck(TcpSession session, BagUseItemAck ack)
        {
            var panel = ClientContext.UI?.GetPanel<IBagPanel>();
            if (panel != null) panel.HandleBagUseItemAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleBagItemChangedNtf(TcpSession session, BagItemChangedNtf ntf)
        {
            var panel = ClientContext.UI?.GetPanel<IBagPanel>();
            if (panel != null) panel.HandleBagItemChangedNtf(ntf);
        }
    }
}
