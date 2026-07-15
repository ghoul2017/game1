using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Event;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class UseSkillAckHandler : NtfMessageHandler<UseSkillAck>
    {
        protected override async Task ExecuteAsync(TcpSession session, UseSkillAck ack)
        {
            if (!string.IsNullOrEmpty(ack.ErrorMessage))
            {
                ClientUiEventPublisher.ShowToast(ack.ErrorMessage);
                return;
            }

            EventBus.Instance.Publish(new EventType.FightSkillCastedEvent { Ack = ack });
            await Task.CompletedTask;
        }
    }
}
