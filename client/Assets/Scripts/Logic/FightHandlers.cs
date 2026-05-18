using Shared.Core.Network;
using Shared.Common.Protos;

namespace Client.Logic
{
    public static class FightHandlers
    {
        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleUseSkillAck(TcpSession session, UseSkillAck ack)
        {
            var panel = ClientContext.UI?.GetPanel<IMainPanel>();
            if (panel != null) panel.HandleUseSkillAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleHpChangedNtf(TcpSession session, HpChangedNtf ntf)
        {
            var panel = ClientContext.UI?.GetPanel<IMainPanel>();
            if (panel != null) panel.HandleHpChangedNtf(ntf);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleKilledByNtf(TcpSession session, KilledByNtf ntf)
        {
            var panel = ClientContext.UI?.GetPanel<IMainPanel>();
            if (panel != null) panel.HandleKilledByNtf(ntf);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandlePkModeChangedNtf(TcpSession session, PkModeChangedNtf ntf)
        {
            var panel = ClientContext.UI?.GetPanel<IMainPanel>();
            if (panel != null) panel.HandlePkModeChangedNtf(ntf);
        }
    }
}
