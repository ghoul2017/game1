using Shared.Core.Network;
using Shared.Common.Protos;

namespace Client.Logic
{
    public static class MailHandlers
    {
        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleMailNewNtf(TcpSession session, MailNewNtf ntf)
        {
            MailController.Instance.HandleMailNewNtf(ntf);
        }
    }
}
