using Shared.Core.Network;
using Shared.Common.Protos;

namespace Client.Logic
{
    public static class TaskHandlers
    {
        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleTaskChangedNtf(TcpSession session, TaskChangedNtf ntf)
        {
            TaskController.Instance.HandleTaskChangedNtf(ntf);
        }
    }
}
