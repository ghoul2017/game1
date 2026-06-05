using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Event;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class HpChangedNtfHandler : NtfMessageHandler<HpChangedNtf>
    {
        protected override Task ExecuteAsync(TcpSession session, HpChangedNtf ntf)
        {
            long selfId = GameData.CurrentRole != null ? GameData.CurrentRole.RoleId : 0;
            if (ntf.ObjectId == selfId)
            {
                GameData.CurrentHp = ntf.Hp;
            }

            EventBus.Instance.Publish(new EventType.FightHpChangedEvent { Notification = ntf });
            return Task.CompletedTask;
        }
    }
}
