using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Event;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class ChatNtfHandler : NtfMessageHandler<ChatNtf>
    {
        protected override Task ExecuteAsync(TcpSession session, ChatNtf ntf)
        {
            string line = string.Format("[{0}][{1}] {2}",
                ntf.Channel == ChatChannel.World ? "世界" : "私聊",
                ntf.SenderName, ntf.Content);
            AppendLine(line);
            return Task.CompletedTask;
        }

        private static void AppendLine(string line)
        {
            var controller = ChatController.Instance;
            controller.ChatLines.Add(new ChatLineData
            {
                Channel = controller.CurrentChannel,
                DisplayText = line
            });
            if (controller.ChatLines.Count > ChatController.MaxChatLines)
                controller.ChatLines.RemoveAt(0);

            EventBus.Instance.Publish(new EventType.ChatMessageAddedEvent());
        }
    }
}
