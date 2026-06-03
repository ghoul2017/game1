using Shared.Core.Network;
using Shared.Common.Protos;

namespace Client.Logic
{
    public static class RoleSelectHandlers
    {
        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleEnterGameNtf(TcpSession session, EnterGameNtf ntf)
        {
            RoleSelectController.Instance.HandleEnterGameNtf(ntf);
        }
    }
}
