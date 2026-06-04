using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class UseSkillAckHandler : NtfMessageHandler<UseSkillAck>
    {
        protected override Task ExecuteAsync(TcpSession session, UseSkillAck ack)
        {
            FightController.Instance.ApplySkillEffect(ack);
            return Task.CompletedTask;
        }
    }
}
