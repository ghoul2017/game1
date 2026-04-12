using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Client.Core;
using Client.Network;
using GameShared.Protos;

namespace Client.Game
{
    public class MainPanel : UIPanel
    {
        private Text chatDisplay;
        private InputField chatInput;
        private Text chatTitle;
        private int currentChannel = ChatChannel.World;
        private const int MAX_CHAT_LINES = 50;
        private readonly List<string> chatLines = new List<string>();

        private Text roleNameText;
        private Text roleJobText;
        private Text roleLevelText;
        private Text coordText;
        private VirtualJoystick joystick;

        public override void OnInit()
        {
            // 半透明全屏背景（让3D场景透出）
            UIBuilder.CreateFullScreenBg(transform, new Color(0.04f, 0.04f, 0.1f, 0.05f));
            CreateTopBar();
            CreateSideButtons();
            CreateCenterArea();
            CreateChatArea();
            CreateSystemButtons();
            CreateBottomBar();
            CreateJoystick();

            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        public override void OnShow()
        {
            var role = GameData.CurrentRole;
            if (role != null)
            {
                if (roleNameText != null) roleNameText.text = role.Name;
                if (roleJobText != null) roleJobText.text = $"[ {role.Job} ]";
                if (roleLevelText != null) roleLevelText.text = $"Lv.{role.Level}";
            }
            RegisterChatHandlers();
        }

        public override void OnHide()
        {
            UnregisterChatHandlers();
        }

        private void Update()
        {
            // 同步摇杆输入到 PlayerController
            if (joystick != null && GameSceneEntry.PlayerController != null)
            {
                GameSceneEntry.PlayerController.joystickInput = joystick.InputVector;
            }

            // 更新坐标显示
            if (coordText != null && GameSceneEntry.PlayerController != null)
            {
                var pc = GameSceneEntry.PlayerController;
                coordText.text = $"({pc.CurrentGridX}, {pc.CurrentGridY})";
            }
        }

        private void CreateTopBar()
        {
            var topBar = new GameObject("TopBar");
            topBar.transform.SetParent(transform, false);
            var topRect = topBar.AddComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0, 1);
            topRect.anchorMax = new Vector2(1, 1);
            topRect.pivot = new Vector2(0.5f, 1);
            topRect.sizeDelta = new Vector2(0, 75);
            topRect.anchoredPosition = Vector2.zero;
            topBar.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.1f, 0.88f);

            var div = UIBuilder.CreateDivider(topBar.transform, "Div", 0);
            var dRect = div.GetComponent<RectTransform>();
            dRect.anchorMin = new Vector2(0, 0);
            dRect.anchorMax = new Vector2(1, 0);
            dRect.offsetMin = Vector2.zero;
            dRect.offsetMax = Vector2.zero;

            var (avatarFrame, avatarContent) = UIBuilder.CreateBorderedPanel(topBar.transform, "Avatar", 52, 52,
                new Color(0.12f, 0.12f, 0.22f, 1f), UIBuilder.GoldDark, 2f);
            var afRect = avatarFrame.GetComponent<RectTransform>();
            afRect.anchorMin = new Vector2(0, 0.5f);
            afRect.anchorMax = new Vector2(0, 0.5f);
            afRect.pivot = new Vector2(0, 0.5f);
            afRect.anchoredPosition = new Vector2(15, 0);

            var avatarText = UIBuilder.CreateText(avatarContent, "AvatarText", "勇", 26, UIBuilder.Gold,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.SetStretch(avatarText.GetComponent<RectTransform>());

            roleNameText = UIBuilder.CreateText(topBar.transform, "RoleName", "勇者", 20, UIBuilder.TextWhite,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            var nRect = roleNameText.GetComponent<RectTransform>();
            nRect.anchorMin = new Vector2(0, 0.5f);
            nRect.anchorMax = new Vector2(0, 0.5f);
            nRect.pivot = new Vector2(0, 0.5f);
            nRect.anchoredPosition = new Vector2(82, 10);
            nRect.sizeDelta = new Vector2(200, 26);

            roleLevelText = UIBuilder.CreateText(topBar.transform, "RoleLevel", "Lv.1", 16, UIBuilder.Gold,
                TextAnchor.MiddleLeft);
            var lRect = roleLevelText.GetComponent<RectTransform>();
            lRect.anchorMin = new Vector2(0, 0.5f);
            lRect.anchorMax = new Vector2(0, 0.5f);
            lRect.pivot = new Vector2(0, 0.5f);
            lRect.anchoredPosition = new Vector2(82, -12);
            lRect.sizeDelta = new Vector2(80, 22);

            roleJobText = UIBuilder.CreateText(topBar.transform, "RoleJob", "[ 战士 ]", 16, UIBuilder.TextGray,
                TextAnchor.MiddleLeft);
            var jRect = roleJobText.GetComponent<RectTransform>();
            jRect.anchorMin = new Vector2(0, 0.5f);
            jRect.anchorMax = new Vector2(0, 0.5f);
            jRect.pivot = new Vector2(0, 0.5f);
            jRect.anchoredPosition = new Vector2(150, -12);
            jRect.sizeDelta = new Vector2(120, 22);

            var expBar = UIBuilder.CreateSlider(topBar.transform, "ExpBar", 220, 8);
            var expRect = expBar.GetComponent<RectTransform>();
            expRect.anchorMin = new Vector2(0.5f, 0.5f);
            expRect.anchorMax = new Vector2(0.5f, 0.5f);
            expRect.anchoredPosition = new Vector2(0, -25);
            expBar.value = 0.65f;

            var expLabel = UIBuilder.CreateText(topBar.transform, "ExpLabel", "EXP 65%", 11, UIBuilder.TextDim,
                TextAnchor.MiddleRight);
            var elRect = expLabel.GetComponent<RectTransform>();
            elRect.anchorMin = new Vector2(0.5f, 0.5f);
            elRect.anchorMax = new Vector2(0.5f, 0.5f);
            elRect.anchoredPosition = new Vector2(120, -25);
            elRect.sizeDelta = new Vector2(60, 18);

            var gold = UIBuilder.CreateText(topBar.transform, "Gold", "金币: 99,999", 17, UIBuilder.Gold,
                TextAnchor.MiddleRight);
            var gRect = gold.GetComponent<RectTransform>();
            gRect.anchorMin = new Vector2(1, 0.5f);
            gRect.anchorMax = new Vector2(1, 0.5f);
            gRect.pivot = new Vector2(1, 0.5f);
            gRect.anchoredPosition = new Vector2(-20, 10);
            gRect.sizeDelta = new Vector2(200, 26);

            var diamond = UIBuilder.CreateText(topBar.transform, "Diamond", "钻石: 888", 15, new Color(0.4f, 0.7f, 1f),
                TextAnchor.MiddleRight);
            var drRect = diamond.GetComponent<RectTransform>();
            drRect.anchorMin = new Vector2(1, 0.5f);
            drRect.anchorMax = new Vector2(1, 0.5f);
            drRect.pivot = new Vector2(1, 0.5f);
            drRect.anchoredPosition = new Vector2(-20, -12);
            drRect.sizeDelta = new Vector2(200, 22);
        }

        private void CreateSideButtons()
        {
            var sideBg = new GameObject("SideBg");
            sideBg.transform.SetParent(transform, false);
            var sbRect = sideBg.AddComponent<RectTransform>();
            sbRect.anchorMin = new Vector2(0, 0);
            sbRect.anchorMax = new Vector2(0, 1);
            sbRect.pivot = new Vector2(0, 0.5f);
            sbRect.sizeDelta = new Vector2(140, 0);
            sbRect.anchoredPosition = Vector2.zero;
            sideBg.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.1f, 0.6f);

            var div = UIBuilder.CreateDivider(sideBg.transform, "Div", 1);
            var dRect = div.GetComponent<RectTransform>();
            dRect.anchorMin = new Vector2(1, 0);
            dRect.anchorMax = new Vector2(1, 1);
            dRect.offsetMin = Vector2.zero;
            dRect.offsetMax = Vector2.zero;

            var sideTitle = UIBuilder.CreateText(sideBg.transform, "SideTitle", "功能菜单", 13, UIBuilder.TextDim,
                TextAnchor.MiddleCenter);
            var stRect = sideTitle.GetComponent<RectTransform>();
            stRect.anchorMin = new Vector2(0, 1);
            stRect.anchorMax = new Vector2(1, 1);
            stRect.anchoredPosition = new Vector2(0, -15);
            stRect.sizeDelta = new Vector2(140, 20);

            string[] btnNames = { "背包", "任务", "邮件", "商城", "技能", "公会" };
            for (int i = 0; i < btnNames.Length; i++)
            {
                var btn = UIBuilder.CreateButton(sideBg.transform, $"Btn_{btnNames[i]}", btnNames[i],
                    115, 42, 16, UIBuilder.ButtonNormal, UIBuilder.TextWhite);
                btn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 140 - i * 55);
                string name = btnNames[i];
                btn.onClick.AddListener(() =>
                {
                    if (name == "背包")
                        UIManager.Instance.ShowPanel<BagPanel>();
                    else if (name == "邮件")
                        UIManager.Instance.ShowPanel<MailPanel>();
                    else if (name == "任务")
                        UIManager.Instance.ShowPanel<TaskPanel>();
                    else
                        Debug.Log("[MainPanel] 点击: " + name);
                });
            }
        }

        private void CreateCenterArea()
        {
            // 中心区域：全透明，让3D场景可见
            // 不创建任何遮挡物

            // 小地图区域（右上角）
            var minimap = UIBuilder.CreatePanel(transform, "Minimap", 160, 160, new Color(0.04f, 0.04f, 0.1f, 0.7f));
            var mmRect = minimap.GetComponent<RectTransform>();
            mmRect.anchorMin = new Vector2(1, 1);
            mmRect.anchorMax = new Vector2(1, 1);
            mmRect.pivot = new Vector2(1, 1);
            mmRect.anchoredPosition = new Vector2(-20, -85);

            var mapLabel = UIBuilder.CreateText(minimap.transform, "MapLabel", "小地图", 14,
                UIBuilder.TextDim, TextAnchor.MiddleCenter);
            var mlRect = mapLabel.GetComponent<RectTransform>();
            mlRect.anchorMin = new Vector2(0, 1);
            mlRect.anchorMax = new Vector2(1, 1);
            mlRect.pivot = new Vector2(0.5f, 1);
            mlRect.anchoredPosition = new Vector2(0, -5);
            mlRect.sizeDelta = new Vector2(160, 18);

            // 坐标显示
            coordText = UIBuilder.CreateText(minimap.transform, "CoordText", "(0, 0)", 16,
                UIBuilder.Gold, TextAnchor.MiddleCenter);
            UIBuilder.SetStretch(coordText.GetComponent<RectTransform>());

            var mmBorder = UIBuilder.CreateDivider(minimap.transform, "Border", 160);
            var mbRect = mmBorder.GetComponent<RectTransform>();
            mbRect.anchorMin = Vector2.zero;
            mbRect.anchorMax = Vector2.one;
            mbRect.offsetMin = Vector2.zero;
            mbRect.offsetMax = Vector2.zero;

            // 传送按钮（小地图下方）
            var teleportBtn = UIBuilder.CreateButton(transform, "TeleportBtn", "传送", 80, 26, 13,
                UIBuilder.AccentBlue, UIBuilder.TextWhite);
            var tpRect = teleportBtn.GetComponent<RectTransform>();
            tpRect.anchorMin = new Vector2(1, 1);
            tpRect.anchorMax = new Vector2(1, 1);
            tpRect.pivot = new Vector2(1, 1);
            tpRect.anchoredPosition = new Vector2(-60, -250);
            teleportBtn.onClick.AddListener(() =>
            {
                UIManager.Instance.ShowPanel<ChangeMapPanel>();
            });
        }

        private void CreateChatArea()
        {
            var chatPanel = new GameObject("ChatPanel");
            chatPanel.transform.SetParent(transform, false);
            var chatRect = chatPanel.AddComponent<RectTransform>();
            chatRect.anchorMin = new Vector2(0, 0);
            chatRect.anchorMax = new Vector2(1, 0);
            chatRect.pivot = new Vector2(0.5f, 0);
            chatRect.sizeDelta = new Vector2(0, 155);
            chatRect.anchoredPosition = Vector2.zero;
            chatPanel.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.1f, 0.8f);

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
                "<color=#FFD700>[系统]</color> 欢迎来到游戏世界！\n<color=#FFD700>[系统]</color> 祝你冒险愉快！", 15,
                UIBuilder.TextWhite, TextAnchor.LowerLeft);
            var cdRect = chatDisplay.GetComponent<RectTransform>();
            cdRect.anchorMin = new Vector2(0, 0);
            cdRect.anchorMax = new Vector2(1, 1);
            cdRect.offsetMin = new Vector2(15, 45);
            cdRect.offsetMax = new Vector2(-15, -20);

            chatInput = UIBuilder.CreateInputField(chatPanel.transform, "ChatInput", "输入聊天内容...", 480, 33, 15);
            var ciRect = chatInput.GetComponent<RectTransform>();
            ciRect.anchorMin = new Vector2(0, 0);
            ciRect.anchorMax = new Vector2(0, 0);
            ciRect.pivot = new Vector2(0, 0);
            ciRect.anchoredPosition = new Vector2(155, 6);

            var sendBtn = UIBuilder.CreateButton(chatPanel.transform, "SendBtn", "发送", 75, 33, 15,
                UIBuilder.AccentBlue, UIBuilder.TextWhite);
            var sbRect = sendBtn.GetComponent<RectTransform>();
            sbRect.anchorMin = new Vector2(0, 0);
            sbRect.anchorMax = new Vector2(0, 0);
            sbRect.pivot = new Vector2(0, 0);
            sbRect.anchoredPosition = new Vector2(645, 6);

            sendBtn.onClick.AddListener(OnSendChat);

            // 初始化聊天记录
            chatLines.Add("<color=#FFD700>[系统]</color> 欢迎来到游戏世界！");
            chatLines.Add("<color=#FFD700>[系统]</color> 祝你冒险愉快！");
        }

        private void SwitchChannel(int channel)
        {
            currentChannel = channel;
            string chName = channel == ChatChannel.World ? "世界" : "私聊";
            if (chatTitle != null)
                chatTitle.text = "[ " + chName + " ]";
        }

        private void OnSendChat()
        {
            if (chatInput == null) return;
            string msg = chatInput.text.Trim();
            if (string.IsNullOrEmpty(msg)) return;

            var req = new ChatReq();
            req.Channel = currentChannel;
            req.Content = msg;
            NetClient.Instance.Send(req);
            chatInput.text = "";
        }

        // ==================== Chat Network Handlers ====================

        private void RegisterChatHandlers()
        {
            var nc = NetClient.Instance;
            if (nc == null || nc.Dispatcher == null) return;
            nc.Dispatcher.Register<ChatAck>(MsgId.ChatAck, HandleChatAck);
            nc.Dispatcher.Register<ChatNtf>(MsgId.ChatNtf, HandleChatNtf);
            nc.Dispatcher.Register<SystemChatNtf>(MsgId.SystemChatNtf, HandleSystemChatNtf);
        }

        private void UnregisterChatHandlers()
        {
            var nc = NetClient.Instance;
            if (nc == null || nc.Dispatcher == null) return;
            nc.Dispatcher.Unregister(MsgId.ChatAck);
            nc.Dispatcher.Unregister(MsgId.ChatNtf);
            nc.Dispatcher.Unregister(MsgId.SystemChatNtf);
        }

        private void HandleChatAck(ChatAck ack)
        {
            if (ack.Code != 0)
            {
                UIToast.Show(GetChatErrorText(ack.Code));
            }
        }

        private void HandleChatNtf(ChatNtf ntf)
        {
            string chTag = ntf.Channel == ChatChannel.World ? "世界" : "私聊";
            string line = string.Format("<color=#88CCFF>[{0}][{1}]</color> {2}",
                chTag, ntf.SenderName, ntf.Content);
            AppendChatLine(line);
        }

        private void HandleSystemChatNtf(SystemChatNtf ntf)
        {
            string line = string.Format("<color=#FFD700>[系统]</color> {0}", ntf.Content);
            AppendChatLine(line);
        }

        private void AppendChatLine(string line)
        {
            chatLines.Add(line);
            if (chatLines.Count > MAX_CHAT_LINES)
                chatLines.RemoveAt(0);

            if (chatDisplay != null)
                chatDisplay.text = string.Join("\n", chatLines.ToArray());
        }

        private static string GetChatErrorText(int code)
        {
            switch (code)
            {
                case 3001: return "聊天内容不能为空";
                case 3002: return "发言太快，请稍后再试";
                case 3003: return "无效的聊天频道";
                case 3004: return "无效的私聊目标";
                case 3005: return "目标玩家不在线";
                case 3006: return "未登录";
                default: return "发送失败(" + code + ")";
            }
        }

        private void CreateSystemButtons()
        {
            var arrangeBtn = UIBuilder.CreateButton(transform, "ArrangeBtn", "排行", 65, 30, 14,
                UIBuilder.ButtonNormal, UIBuilder.TextGray);
            var aRect = arrangeBtn.GetComponent<RectTransform>();
            aRect.anchorMin = new Vector2(1, 1);
            aRect.anchorMax = new Vector2(1, 1);
            aRect.pivot = new Vector2(1, 1);
            aRect.anchoredPosition = new Vector2(-20, -85);

            var eventBtn = UIBuilder.CreateButton(transform, "EventBtn", "活动", 65, 30, 14,
                UIBuilder.ButtonNormal, UIBuilder.TextGray);
            var eRect = eventBtn.GetComponent<RectTransform>();
            eRect.anchorMin = new Vector2(1, 1);
            eRect.anchorMax = new Vector2(1, 1);
            eRect.pivot = new Vector2(1, 1);
            eRect.anchoredPosition = new Vector2(-95, -85);

            var settingsBtn = UIBuilder.CreateButton(transform, "SettingsBtn", "设置", 65, 30, 14,
                UIBuilder.ButtonNormal, UIBuilder.TextGray);
            var sRect = settingsBtn.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(1, 1);
            sRect.anchorMax = new Vector2(1, 1);
            sRect.pivot = new Vector2(1, 1);
            sRect.anchoredPosition = new Vector2(-20, -125);
            settingsBtn.onClick.AddListener(() =>
            {
                Debug.Log("[MainPanel] 打开设置");
                UIToast.Show("设置功能开发中...");
            });

            var quitBtn = UIBuilder.CreateButton(transform, "QuitBtn", "退出", 65, 30, 14,
                UIBuilder.ButtonNormal, new Color(0.8f, 0.3f, 0.3f));
            var qRect = quitBtn.GetComponent<RectTransform>();
            qRect.anchorMin = new Vector2(1, 1);
            qRect.anchorMax = new Vector2(1, 1);
            qRect.pivot = new Vector2(1, 1);
            qRect.anchoredPosition = new Vector2(-95, -125);
            quitBtn.onClick.AddListener(() =>
            {
                Debug.Log("[MainPanel] 退出游戏");
                UIMessageBox.Show("退出游戏", "确定要退出游戏吗？",
                    () => { UIManager.Instance.ClearAll(); UIManager.Instance.ShowPanel<Login.LoginPanel>(); },
                    null, "退出", "取消", true);
            });
        }

        private void CreateBottomBar()
        {
            var staminaBg = new GameObject("StaminaBar");
            staminaBg.transform.SetParent(transform, false);
            var stRect = staminaBg.AddComponent<RectTransform>();
            stRect.anchorMin = new Vector2(1, 1);
            stRect.anchorMax = new Vector2(1, 1);
            stRect.pivot = new Vector2(1, 1);
            stRect.anchoredPosition = new Vector2(-20, -260);
            stRect.sizeDelta = new Vector2(160, 8);
            staminaBg.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.2f, 0.8f);

            var stFill = new GameObject("Fill");
            stFill.transform.SetParent(staminaBg.transform, false);
            var stfRect = stFill.AddComponent<RectTransform>();
            stfRect.anchorMin = Vector2.zero;
            stfRect.anchorMax = new Vector2(0.8f, 1f);
            stfRect.offsetMin = Vector2.zero;
            stfRect.offsetMax = Vector2.zero;
            stFill.AddComponent<Image>().color = new Color(0.3f, 0.8f, 0.4f);

            var stLabel = UIBuilder.CreateText(transform, "StaminaLabel", "体力 80/100", 12, UIBuilder.TextDim,
                TextAnchor.MiddleRight);
            var slRect = stLabel.GetComponent<RectTransform>();
            slRect.anchorMin = new Vector2(1, 1);
            slRect.anchorMax = new Vector2(1, 1);
            slRect.pivot = new Vector2(1, 1);
            slRect.anchoredPosition = new Vector2(-20, -272);
            slRect.sizeDelta = new Vector2(160, 18);
        }

        private void CreateJoystick()
        {
            joystick = VirtualJoystick.Create(transform);
        }
    }
}
