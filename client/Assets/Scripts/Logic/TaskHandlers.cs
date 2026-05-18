using Shared.Core.Network;
using Shared.Common.Protos;

namespace Client.Logic
{
    public static class TaskHandlers
    {
        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleQueryTaskListAck(TcpSession session, QueryTaskListAck ack)
        {
            var panel = ClientContext.UI?.GetPanel<ITaskPanel>();
            if (panel != null) panel.HandleQueryTaskListAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleTaskAcceptAck(TcpSession session, TaskAcceptAck ack)
        {
            var panel = ClientContext.UI?.GetPanel<ITaskPanel>();
            if (panel != null) panel.HandleTaskAcceptAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleTaskSubmitAck(TcpSession session, TaskSubmitAck ack)
        {
            var panel = ClientContext.UI?.GetPanel<ITaskPanel>();
            if (panel != null) panel.HandleTaskSubmitAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleTaskGiveUpAck(TcpSession session, TaskGiveUpAck ack)
        {
            var panel = ClientContext.UI?.GetPanel<ITaskPanel>();
            if (panel != null) panel.HandleTaskGiveUpAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleTaskChangedNtf(TcpSession session, TaskChangedNtf ntf)
        {
            var panel = ClientContext.UI?.GetPanel<ITaskPanel>();
            if (panel != null) panel.HandleTaskChangedNtf(ntf);
        }
    }
}
