using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Event;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class SystemChatNtfHandler : NtfMessageHandler<SystemChatNtf>
    {
        protected override Task ExecuteAsync(TcpSession session, SystemChatNtf ntf)
        {
            var controller = ChatController.Instance;
            controller.ChatLines.Add(new ChatLineData
            {
                Channel = controller.CurrentChannel,
                DisplayText = string.Format("[系统] {0}", ntf.Content)
            });
            if (controller.ChatLines.Count > ChatController.MaxChatLines)
                controller.ChatLines.RemoveAt(0);

            EventBus.Instance.Publish(new EventType.ChatMessageAddedEvent());
            return Task.CompletedTask;
        }
    }
}
