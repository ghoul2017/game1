using Shared.Core.Network;
using Shared.Common.Protos;
using Client.Core;

namespace Client.Login
{
    public static class LoginHandlers
    {
        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleLoginRes(TcpSession session, LoginRes res)
        {
            var panel = UIManager.Instance?.GetPanel<LoginPanel>();
            if (panel != null) panel.HandleLoginRes(res);
        }
    }
}
