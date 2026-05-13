using Shared.Core.Network;
using Shared.Common.Protos;
using Client.Core;

namespace Client.Game
{
    public static class TaskHandlers
    {
        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleQueryTaskListAck(TcpSession session, QueryTaskListAck ack)
        {
            var panel = UIManager.Instance?.GetPanel<TaskPanel>();
            if (panel != null) panel.HandleQueryTaskListAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleTaskAcceptAck(TcpSession session, TaskAcceptAck ack)
        {
            var panel = UIManager.Instance?.GetPanel<TaskPanel>();
            if (panel != null) panel.HandleTaskAcceptAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleTaskSubmitAck(TcpSession session, TaskSubmitAck ack)
        {
            var panel = UIManager.Instance?.GetPanel<TaskPanel>();
            if (panel != null) panel.HandleTaskSubmitAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleTaskGiveUpAck(TcpSession session, TaskGiveUpAck ack)
        {
            var panel = UIManager.Instance?.GetPanel<TaskPanel>();
            if (panel != null) panel.HandleTaskGiveUpAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleTaskChangedNtf(TcpSession session, TaskChangedNtf ntf)
        {
            var panel = UIManager.Instance?.GetPanel<TaskPanel>();
            if (panel != null) panel.HandleTaskChangedNtf(ntf);
        }
    }
}
