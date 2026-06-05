using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Event;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class KilledByNtfHandler : NtfMessageHandler<KilledByNtf>
    {
        protected override Task ExecuteAsync(TcpSession session, KilledByNtf ntf)
        {
            EventBus.Instance.Publish(new EventType.FightKilledByEvent { Notification = ntf });
            return Task.CompletedTask;
        }
    }
}
