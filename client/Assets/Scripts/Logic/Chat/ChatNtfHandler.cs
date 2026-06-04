using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class ChatNtfHandler : NtfMessageHandler<ChatNtf>
    {
        protected override Task ExecuteAsync(TcpSession session, ChatNtf ntf)
        {
            ChatController.Instance.HandleChatNtf(ntf);
            return Task.CompletedTask;
        }
    }
}