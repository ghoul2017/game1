using Shared.Core.Network;
using Shared.Common.Protos;

namespace Client.Logic
{
    public static class RoleSelectHandlers
    {
        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleCreateRoleRes(TcpSession session, CreateRoleRes res)
        {
            var panel = ClientContext.UI?.GetPanel<IRoleSelectPanel>();
            if (panel != null) panel.HandleCreateRoleRes(res);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleEnterGameRes(TcpSession session, EnterGameRes res)
        {
            var panel = ClientContext.UI?.GetPanel<IRoleSelectPanel>();
            if (panel != null) panel.HandleEnterGameRes(res);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleEnterGameNtf(TcpSession session, EnterGameNtf ntf)
        {
            var panel = ClientContext.UI?.GetPanel<IRoleSelectPanel>();
            if (panel != null) panel.HandleEnterGameNtf(ntf);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleEnterMapAck(TcpSession session, EnterMapAck ack)
        {
            var panel = ClientContext.UI?.GetPanel<IRoleSelectPanel>();
            if (panel != null) panel.HandleEnterMapAck(ack);
        }
    }
}
