using Shared.Core.Network;
using Shared.Common.Protos;
using Client.Core;

namespace Client.Game
{
    public static class BagHandlers
    {
        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleQueryBagAck(TcpSession session, QueryBagAck ack)
        {
            var panel = UIManager.Instance?.GetPanel<BagPanel>();
            if (panel != null) panel.HandleQueryBagAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleBagTidyAck(TcpSession session, BagTidyAck ack)
        {
            var panel = UIManager.Instance?.GetPanel<BagPanel>();
            if (panel != null) panel.HandleBagTidyAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleBagUseItemAck(TcpSession session, BagUseItemAck ack)
        {
            var panel = UIManager.Instance?.GetPanel<BagPanel>();
            if (panel != null) panel.HandleBagUseItemAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleBagItemChangedNtf(TcpSession session, BagItemChangedNtf ntf)
        {
            var panel = UIManager.Instance?.GetPanel<BagPanel>();
            if (panel != null) panel.HandleBagItemChangedNtf(ntf);
        }
    }
}
