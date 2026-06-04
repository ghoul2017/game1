using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class SystemChatNtfHandler : NtfMessageHandler<SystemChatNtf>
    {
        protected override Task ExecuteAsync(TcpSession session, SystemChatNtf ntf)
        {
            ChatController.Instance.HandleSystemChatNtf(ntf);
            return Task.CompletedTask;
        }
    }
}
