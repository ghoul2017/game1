using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Event;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class UseSkillAckHandler : NtfMessageHandler<UseSkillAck>
    {
        protected override Task ExecuteAsync(TcpSession session, UseSkillAck ack)
        {
            if (!string.IsNullOrEmpty(ack.ErrorMessage))
            {
                ClientUiEventPublisher.ShowToast(ack.ErrorMessage);
                return Task.CompletedTask;
            }

            EventBus.Instance.Publish(new EventType.FightSkillCastedEvent { Ack = ack });
            return Task.CompletedTask;
        }
    }
}
