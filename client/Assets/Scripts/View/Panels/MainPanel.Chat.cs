using UnityEngine;
using UnityEngine.UI;
using Client.Logic;
using Shared.Common.Protos;

namespace Client.View
{
    public partial class MainPanel
    {
        private void CreateChatArea()
        {
            var chatPanel = new GameObject("ChatPanel");
            chatPanel.transform.SetParent(transform, false);
            var chatRect = chatPanel.AddComponent<RectTransform>();
            chatRect.anchorMin = new Vector2(0, 0);
            chatRect.anchorMax = new Vector2(0.6f, 0);
            chatRect.pivot = new Vector2(0, 0);
            chatRect.sizeDelta = new Vector2(0, 140);
            chatRect.anchoredPosition = Vector2.zero;
            chatPanel.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.1f, 0.85f);

            var div = UIBuilder.CreateDivider(chatPanel.transform, "Div", 0);
            var dRect = div.GetComponent<RectTransform>();
            dRect.anchorMin = new Vector2(0, 1);
            dRect.anchorMax = new Vector2(1, 1);
            dRect.offsetMin = Vector2.zero;
            dRect.offsetMax = Vector2.zero;

            // 频道切换按钮
            var worldBtn = UIBuilder.CreateButton(chatPanel.transform, "WorldBtn", "世界", 55, 20, 12,
                UIBuilder.AccentBlue, UIBuilder.TextWhite);
            var wbRect = worldBtn.GetComponent<RectTransform>();
            wbRect.anchorMin = new Vector2(0, 1);
            wbRect.anchorMax = new Vector2(0, 1);
            wbRect.pivot = new Vector2(0, 1);
            wbRect.anchoredPosition = new Vector2(10, -3);
            worldBtn.onClick.AddListener(() => SwitchChannel(ChatChannel.World));

            var privateBtn = UIBuilder.CreateButton(chatPanel.transform, "PrivateBtn", "私聊", 55, 20, 12,
                UIBuilder.ButtonNormal, UIBuilder.TextGray);
            var pbRect = privateBtn.GetComponent<RectTransform>();
            pbRect.anchorMin = new Vector2(0, 1);
            pbRect.anchorMax = new Vector2(0, 1);
            pbRect.pivot = new Vector2(0, 1);
            pbRect.anchoredPosition = new Vector2(70, -3);
            privateBtn.onClick.AddListener(() => SwitchChannel(ChatChannel.Private));

            chatTitle = UIBuilder.CreateText(chatPanel.transform, "ChatTitle", "[ 世界 ]", 13,
                UIBuilder.Gold, TextAnchor.MiddleLeft);
            var ctRect = chatTitle.GetComponent<RectTransform>();
            ctRect.anchorMin = new Vector2(0, 1);
            ctRect.anchorMax = new Vector2(1, 1);
            ctRect.pivot = new Vector2(0, 1);
            ctRect.anchoredPosition = new Vector2(130, -3);
            ctRect.sizeDelta = new Vector2(300, 18);

            chatDisplay = UIBuilder.CreateText(chatPanel.transform, "ChatDisplay",
                "<color=#FFD700>[系统]</color> 欢迎来到游戏世界！\n<color=#FFD700>[系统]</color> 祝你冒险愉快！", 14,
                UIBuilder.TextWhite, TextAnchor.LowerLeft);
            var cdRect = chatDisplay.GetComponent<RectTransform>();
            cdRect.anchorMin = new Vector2(0, 0);
            cdRect.anchorMax = new Vector2(1, 1);
            cdRect.offsetMin = new Vector2(10, 38);
            cdRect.offsetMax = new Vector2(-10, -22);

            chatInput = UIBuilder.CreateInputField(chatPanel.transform, "ChatInput", "输入聊天内容...", 360, 30, 14);
            var ciRect = chatInput.GetComponent<RectTransform>();
            ciRect.anchorMin = new Vector2(0, 0);
            ciRect.anchorMax = new Vector2(0, 0);
            ciRect.pivot = new Vector2(0, 0);
            ciRect.anchoredPosition = new Vector2(10, 4);

            var sendBtn = UIBuilder.CreateButton(chatPanel.transform, "SendBtn", "发送", 65, 30, 14,
                UIBuilder.AccentBlue, UIBuilder.TextWhite);
            var sbRect = sendBtn.GetComponent<RectTransform>();
            sbRect.anchorMin = new Vector2(0, 0);
            sbRect.anchorMax = new Vector2(0, 0);
            sbRect.pivot = new Vector2(0, 0);
            sbRect.anchoredPosition = new Vector2(380, 4);

            sendBtn.onClick.AddListener(OnSendChat);
        }

        private void SwitchChannel(int channel)
        {
            ChatController.Instance.SwitchChannel(channel);
            RefreshChannelDisplay();
        }

        private void RefreshChannelDisplay()
        {
            if (chatTitle != null)
            {
                string chName = ChatController.Instance.CurrentChannel == ChatChannel.World ? "世界" : "私聊";
                chatTitle.text = "[ " + chName + " ]";
            }
        }

        private void OnSendChat()
        {
            if (chatInput == null) return;
            string msg = chatInput.text.Trim();
            if (string.IsNullOrEmpty(msg)) return;

            ChatController.Instance.SendChat(msg);
            chatInput.text = "";
        }

        // ==================== Chat Display ====================

        private void RefreshChatDisplay()
        {
            if (chatDisplay == null) return;

            var lines = ChatController.Instance.ChatLines;
            if (lines.Count == 0)
            {
                chatDisplay.text = "<color=#FFD700>[系统]</color> 欢迎来到游戏世界！\n<color=#FFD700>[系统]</color> 祝你冒险愉快！";
                return;
            }

            var formatted = new string[lines.Count];
            for (int i = 0; i < lines.Count; i++)
            {
                var item = lines[i];
                string color;
                if (item.DisplayText.StartsWith("[系统]"))
                    color = "#FFD700";
                else
                    color = "#88CCFF";
                formatted[i] = string.Format("<color={0}>{1}</color>", color, item.DisplayText);
            }
            chatDisplay.text = string.Join("\n", formatted);
        }
    }
}
