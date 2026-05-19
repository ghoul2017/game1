using Shared.Core.Network;
using Shared.Common.Protos;

namespace Client.Logic
{
    public static class RoleSelectHandlers
    {
        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleCreateRoleRes(TcpSession session, CreateRoleRes res)
        {
            RoleSelectController.Instance.HandleCreateRoleRes(res);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleEnterGameRes(TcpSession session, EnterGameRes res)
        {
            RoleSelectController.Instance.HandleEnterGameRes(res);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleEnterGameNtf(TcpSession session, EnterGameNtf ntf)
        {
            RoleSelectController.Instance.HandleEnterGameNtf(ntf);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleEnterMapAck(TcpSession session, EnterMapAck ack)
        {
            RoleSelectController.Instance.HandleEnterMapAck(ack);
        }
    }
}
