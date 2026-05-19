using Shared.Core.Network;
using Shared.Common.Protos;

namespace Client.Logic
{
    public static class LoginHandlers
    {
        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleLoginRes(TcpSession session, LoginRes res)
        {
            LoginController.Instance.HandleLoginRes(res);
        }
    }
}
