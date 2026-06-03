using Shared.Core.Network;
using Shared.Common.Protos;

namespace Client.Logic
{
    public static class FightHandlers
    {
        [MessageHandler(typeof(NtfMethodHandler))]
        public static void OnSkillEffect(TcpSession session, UseSkillAck ack)
        {
            FightController.Instance.ApplySkillEffect(ack);
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

    }
}
