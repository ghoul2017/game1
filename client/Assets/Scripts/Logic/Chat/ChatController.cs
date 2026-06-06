using System;
using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Event;

namespace Client.Logic
{
    public class ChatController
    {
        public static ChatController Instance { get; } = new();

        public const int MaxChatLines = 50;

        public System.Collections.Generic.List<ChatLineData> ChatLines { get; private set; } = new();
        public int CurrentChannel { get; private set; } = ChatChannel.World;

        public async Task SendChat(string content)
        {
            if (string.IsNullOrEmpty(content))
                return;
            var req = new ChatReq();
            req.Channel = CurrentChannel;
            req.Content = content;
            try
            {
                ChatAck ack = await NetClient.Instance.CallAsync<ChatAck>(req);
                if (!string.IsNullOrEmpty(ack.ErrorMessage))
                {
                    ClientUiEventPublisher.ShowToast(ack.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                ClientUiEventPublisher.ShowToast("发送聊天失败: " + ex.Message);
            }
        }

        public void SwitchChannel(int channel)
        {
            CurrentChannel = channel;
            EventBus.Instance.Publish(new EventType.ChatChannelChangedEvent());
        }
    }

    public struct ChatLineData
    {
        public int Channel;
        public string DisplayText;
    }
}
