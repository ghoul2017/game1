using Shared.Core.Network;
using Shared.Common.Protos;

namespace Client.Logic
{
    public static class TaskHandlers
    {
        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleQueryTaskListAck(TcpSession session, QueryTaskListAck ack)
        {
            TaskController.Instance.HandleQueryTaskListAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleTaskAcceptAck(TcpSession session, TaskAcceptAck ack)
        {
            TaskController.Instance.HandleTaskAcceptAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleTaskSubmitAck(TcpSession session, TaskSubmitAck ack)
        {
            TaskController.Instance.HandleTaskSubmitAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleTaskGiveUpAck(TcpSession session, TaskGiveUpAck ack)
        {
            TaskController.Instance.HandleTaskGiveUpAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleTaskChangedNtf(TcpSession session, TaskChangedNtf ntf)
        {
            TaskController.Instance.HandleTaskChangedNtf(ntf);
        }
    }
}
