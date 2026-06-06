using System.Threading.Tasks;
using Shared.Common.Enums;
using Shared.Common.Protos;
using Shared.Core.Event;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class KickNtfHandler : NtfMessageHandler<KickNtf>
    {
        private static readonly ILog Log = LogManager.GetLogger<KickNtfHandler>();

        protected override Task ExecuteAsync(TcpSession session, KickNtf ntf)
        {
            Log.Info(string.Format("[NetClient] Kicked, reason={0}", ntf.Reason));

            var client = NetClient.Instance;
            if (client == null)
                return Task.CompletedTask;

            client.UserDisconnect = true;
            client.HadSession = false;
            GameData.Token = "";
            GameData.CurrentRole = null;
            GameData.CurrentMap = null;
            EventBus.Instance.Publish(new EventType.NetKickedEvent { Reason = ntf.Reason });
            client.DisposeSession(LogoutReason.NetDisconnected, "Kicked by server");
            return Task.CompletedTask;
        }
    }
}
