using Shared.Core.Network;
using Shared.Common.Protos;

namespace Client.Logic
{
    public static class FightHandlers
    {
        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleUseSkillAck(TcpSession session, UseSkillAck ack)
        {
            FightController.Instance.HandleUseSkillAck(ack);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleHpChangedNtf(TcpSession session, HpChangedNtf ntf)
        {
            FightController.Instance.HandleHpChangedNtf(ntf);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleKilledByNtf(TcpSession session, KilledByNtf ntf)
        {
            FightController.Instance.HandleKilledByNtf(ntf);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandlePkModeChangedNtf(TcpSession session, PkModeChangedNtf ntf)
        {
            FightController.Instance.HandlePkModeChangedNtf(ntf);
        }
    }
}
