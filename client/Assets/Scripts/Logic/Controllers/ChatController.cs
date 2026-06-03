using System;
using Shared.Common.Protos;

namespace Client.Logic
{
    public class ChatController
    {
        public static ChatController Instance { get; } = new();

        public const int MaxChatLines = 50;

        public System.Collections.Generic.List<ChatLineData> ChatLines { get; private set; } = new();
        public int CurrentChannel { get; private set; } = ChatChannel.World;

        public event Action OnMessageAdded;
        public event Action OnChannelChanged;

        public void SendChat(string content)
        {
            if (string.IsNullOrEmpty(content)) return;
            var req = new ChatReq();
            req.Channel = CurrentChannel;
            req.Content = content;
            NetClient.Instance.Send(req);
        }

        public void SwitchChannel(int channel)
        {
            CurrentChannel = channel;
            OnChannelChanged?.Invoke();
        }

        public void HandleChatAck(ChatAck ack)
        {
            if (!string.IsNullOrEmpty(ack.ErrorMessage))
            {
                Context.UI?.ShowToast(ack.ErrorMessage);
            }
        }

        public void HandleChatNtf(ChatNtf ntf)
        {
            string line = string.Format("[{0}][{1}] {2}",
                ntf.Channel == ChatChannel.World ? "世界" : "私聊",
                ntf.SenderName, ntf.Content);
            AppendLine(line);
        }

        public void HandleSystemChatNtf(SystemChatNtf ntf)
        {
            string line = string.Format("[系统] {0}", ntf.Content);
            AppendLine(line);
        }

        private void AppendLine(string line)
        {
            ChatLines.Add(new ChatLineData
            {
                Channel = CurrentChannel,
                DisplayText = line
            });
            if (ChatLines.Count > MaxChatLines)
                ChatLines.RemoveAt(0);
            OnMessageAdded?.Invoke();
        }
    }

    public struct ChatLineData
    {
        public int Channel;
        public string DisplayText;
    }
}
