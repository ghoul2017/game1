using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Client.View;
using Client.View;
using Shared.Common;
using Shared.Common.Protos;
using Client.Logic;

namespace Client.View
{
    public class MainPanel : UIPanel, IMainPanel
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

        // Fight UI
        private Slider hpSlider;
        private Text hpText;
        private Text pkModeText;
        private Text targetText;

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
            CreateHpBar();
            CreateSkillBar();
            CreatePkModeIndicator();
            CreateTargetIndicator();

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
            UpdateHpDisplay();
            UpdatePkModeDisplay();
        }

        public override void OnHide()
        {
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

            // 快捷键触发技能 (1-4)
            if (Input.GetKeyDown(KeyCode.Alpha1)) OnUseSkill(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) OnUseSkill(2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) OnUseSkill(3);
            if (Input.GetKeyDown(KeyCode.Alpha4)) OnUseSkill(4);
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
                    else if (name == "技能")
                        UIToast.Show("使用右下角技能栏释放技能");
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

        public void HandleChatAck(ChatAck ack)
        {
            if (!string.IsNullOrEmpty(ack.ErrorMessage))
            {
                UIToast.Show(ack.ErrorMessage);
            }
        }

        public void HandleChatNtf(ChatNtf ntf)
        {
            string chTag = ntf.Channel == ChatChannel.World ? "世界" : "私聊";
            string line = string.Format("<color=#88CCFF>[{0}][{1}]</color> {2}",
                chTag, ntf.SenderName, ntf.Content);
            AppendChatLine(line);
        }

        public void HandleSystemChatNtf(SystemChatNtf ntf)
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
                    () => { UIManager.Instance.ClearAll(); UIManager.Instance.ShowPanel<LoginPanel>(); },
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

        // ==================== Fight UI ====================

        private void CreateHpBar()
        {
            var hpBgObj = new GameObject("HpBar");
            hpBgObj.transform.SetParent(transform, false);
            var hpBgRect = hpBgObj.AddComponent<RectTransform>();
            hpBgObj.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.2f, 0.8f);
            hpBgRect.anchorMin = new Vector2(0, 1);
            hpBgRect.anchorMax = new Vector2(0, 1);
            hpBgRect.pivot = new Vector2(0, 1);
            hpBgRect.anchoredPosition = new Vector2(15, -80);
            hpBgRect.sizeDelta = new Vector2(220, 14);

            hpSlider = hpBgObj.AddComponent<Slider>();
            hpSlider.interactable = false;
            hpSlider.value = 1f;

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(hpBgObj.transform, false);
            var faRect = fillArea.AddComponent<RectTransform>();
            faRect.anchorMin = Vector2.zero;
            faRect.anchorMax = Vector2.one;
            faRect.offsetMin = Vector2.zero;
            faRect.offsetMax = Vector2.zero;

            var fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillArea.transform, false);
            var fRect = fillObj.AddComponent<RectTransform>();
            fRect.anchorMin = Vector2.zero;
            fRect.anchorMax = Vector2.one;
            fRect.offsetMin = Vector2.zero;
            fRect.offsetMax = Vector2.zero;
            var fillImg = fillObj.AddComponent<Image>();
            fillImg.color = new Color(0.2f, 0.8f, 0.3f);

            hpSlider.fillRect = fRect;
            hpSlider.targetGraphic = fillImg;

            hpText = UIBuilder.CreateText(transform, "HpText", "100/100", 12,
                UIBuilder.TextWhite, TextAnchor.MiddleRight);
            var htRect = hpText.GetComponent<RectTransform>();
            htRect.anchorMin = new Vector2(0, 1);
            htRect.anchorMax = new Vector2(0, 1);
            htRect.pivot = new Vector2(0, 1);
            htRect.anchoredPosition = new Vector2(240, -80);
            htRect.sizeDelta = new Vector2(80, 14);
        }

        private void CreateSkillBar()
        {
            var skillBar = new GameObject("SkillBar");
            skillBar.transform.SetParent(transform, false);
            var sbRect = skillBar.AddComponent<RectTransform>();
            sbRect.anchorMin = new Vector2(1, 0);
            sbRect.anchorMax = new Vector2(1, 0);
            sbRect.pivot = new Vector2(1, 0);
            sbRect.anchoredPosition = new Vector2(-10, 150);
            sbRect.sizeDelta = new Vector2(280, 55);
            skillBar.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.1f, 0.75f);

            int[] skillIds = { 1, 2, 3, 4 };
            string[] skillNames = { "普攻", "技能1", "技能2", "技能3" };
            Color[] skillColors = {
                new Color(0.7f, 0.15f, 0.15f, 0.9f),
                new Color(0.15f, 0.3f, 0.6f, 0.9f),
                new Color(0.15f, 0.5f, 0.25f, 0.9f),
                new Color(0.5f, 0.2f, 0.6f, 0.9f)
            };

            for (int i = 0; i < 4; i++)
            {
                var btn = UIBuilder.CreateButton(skillBar.transform, "Skill_" + i, skillNames[i],
                    60, 44, 14, skillColors[i], UIBuilder.TextWhite);
                btn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-10 - i * 65, 0);
                int sid = skillIds[i];
                btn.onClick.AddListener(() => OnUseSkill(sid));
            }
        }

        private void CreatePkModeIndicator()
        {
            var btn = UIBuilder.CreateButton(transform, "PkModeBtn", "[ 和平 ]", 80, 24, 13,
                new Color(0.1f, 0.3f, 0.1f, 0.8f), new Color(0.3f, 0.8f, 0.3f));
            var pkRect = btn.GetComponent<RectTransform>();
            pkRect.anchorMin = new Vector2(1, 1);
            pkRect.anchorMax = new Vector2(1, 1);
            pkRect.pivot = new Vector2(1, 1);
            pkRect.anchoredPosition = new Vector2(-95, -60);
            pkModeText = btn.GetComponentInChildren<Text>();
            btn.onClick.AddListener(OnTogglePkMode);
        }

        private void CreateTargetIndicator()
        {
            var targetBg = new GameObject("TargetIndicator");
            targetBg.transform.SetParent(transform, false);
            var trRect = targetBg.AddComponent<RectTransform>();
            trRect.anchorMin = new Vector2(0.5f, 1);
            trRect.anchorMax = new Vector2(0.5f, 1);
            trRect.pivot = new Vector2(0.5f, 1);
            trRect.anchoredPosition = new Vector2(0, -90);
            trRect.sizeDelta = new Vector2(200, 28);
            targetBg.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.1f, 0.7f);

            targetText = UIBuilder.CreateText(targetBg.transform, "TargetText", "", 14,
                new Color(1f, 0.4f, 0.4f), TextAnchor.MiddleCenter);
            UIBuilder.SetStretch(targetText.GetComponent<RectTransform>());
        }

        // ==================== Fight Actions ====================

        private void OnUseSkill(int skillId)
        {
            var pc = GameSceneEntry.PlayerController;
            if (pc == null) return;

            var req = new UseSkillReq();
            req.SkillId = skillId;
            req.X = pc.CurrentGridX;
            req.Y = pc.CurrentGridY;
            req.TargetId = GameData.SelectedTargetId;
            NetClient.Instance.Send(req);
        }

        private void OnTogglePkMode()
        {
            int nextMode = (GameData.CurrentPkMode + 1) % 4;
            var req = new SetPkModeReq();
            req.PkMode = nextMode;
            NetClient.Instance.Send(req);
        }

        // ==================== Fight Network Handlers ====================

        public void HandleUseSkillAck(UseSkillAck ack)
        {
            if (!string.IsNullOrEmpty(ack.ErrorMessage))
            {
                UIToast.Show(ack.ErrorMessage);
                return;
            }

            long selfId = GameData.CurrentRole != null ? GameData.CurrentRole.RoleId : 0;

            if (ack.AttackerId != selfId)
            {
                // Other player's skill — play attack animation
                OtherPlayerManager.Instance.PlayAttack(ack.AttackerId, ack.SkillId);
            }

            // Show damage on all hit targets (both self and other players)
            foreach (var target in ack.Targets)
            {
                if (target.TargetId == selfId)
                    continue; // Self damage shown via HpChangedNtf

                OtherPlayerManager.Instance.ShowDamage(
                    target.TargetId,
                    -target.DeltaHp,
                    target.IsCritical);
            }

            Debug.Log(string.Format("[MainPanel] 技能释放成功 Attacker={0} Skill={1} Target={2} Hits={3}",
                ack.AttackerId, ack.SkillId, ack.MainTargetId, ack.Targets.Count));
        }

        public void HandleHpChangedNtf(HpChangedNtf ntf)
        {
            long selfId = GameData.CurrentRole != null ? GameData.CurrentRole.RoleId : 0;

            if (ntf.ObjectId == selfId)
            {
                // 自己的血量变化
                GameData.CurrentHp = ntf.Hp;
                UpdateHpDisplay();

                // 同步更新PlayerView的HP条
                var pv = GameSceneEntry.PlayerController != null
                    ? GameSceneEntry.PlayerController.GetComponent<PlayerView>() : null;
                if (pv != null)
                    pv.UpdateHp(ntf.Hp, GameData.MaxHp);
            }
            else
            {
                // 其他玩家或怪物的血量变化
                var monsterView = MonsterEntityManager.Instance.GetView(ntf.ObjectId);
                if (monsterView != null)
                {
                    // 怪物HP变化
                    monsterView.UpdateHp(ntf.Hp, 100);

                    // 显示伤害数字
                    if (ntf.DeltaHp != 0)
                    {
                        monsterView.ShowDamageNumber(-ntf.DeltaHp, false);
                        if (ntf.DeltaHp < 0)
                            monsterView.PlayHitFlash();
                    }

                    // 死亡/复活
                    if (ntf.Reason == HpChangeReason.Killed && ntf.Hp <= 0)
                        monsterView.PlayDeath();
                    else if (ntf.Reason == HpChangeReason.Revive)
                        monsterView.PlayRevive();
                }
                else
                {
                    // 其他玩家的血量变化
                    OtherPlayerManager.Instance.UpdatePlayerHp(ntf.ObjectId, ntf.Hp, 100);

                    // 死亡/复活表现
                    if (ntf.Reason == HpChangeReason.Killed && ntf.Hp <= 0)
                        OtherPlayerManager.Instance.PlayDeath(ntf.ObjectId);
                    else if (ntf.Reason == HpChangeReason.Revive)
                        OtherPlayerManager.Instance.PlayRevive(ntf.ObjectId);
                }
            }
        }

        public void HandleKilledByNtf(KilledByNtf ntf)
        {
            string killerDesc = ntf.IsMonster ? "怪物" : ntf.KillerName;
            UIMessageBox.Show("阵亡", string.Format("你被 {0} 击杀了！", killerDesc),
                () =>
                {
                    // 复活：回城（重新进图）
                    var mapData = GameData.CurrentMap;
                    if (mapData != null && NetClient.Instance != null)
                    {
                        var req = new EnterMapReq();
                        req.MapCfgId = mapData.MapCfgId;
                        req.Line = mapData.Line;
                        NetClient.Instance.Send(req);
                    }
                }, showCancel: false, confirmText: "回城复活");
        }

        public void HandlePkModeChangedNtf(PkModeChangedNtf ntf)
        {
            GameData.CurrentPkMode = ntf.PkMode;
            UpdatePkModeDisplay();
        }

        // ==================== Fight UI Updates ====================

        private void UpdateHpDisplay()
        {
            int hp = GameData.CurrentHp;
            int maxHp = GameData.MaxHp;
            if (hpSlider != null)
                hpSlider.value = maxHp > 0 ? (float)hp / maxHp : 0f;
            if (hpText != null)
                hpText.text = string.Format("{0}/{1}", hp, maxHp);
        }

        private void UpdatePkModeDisplay()
        {
            if (pkModeText != null)
                pkModeText.text = "[ " + GetPkModeName(GameData.CurrentPkMode) + " ]";
        }

        public void UpdateTargetDisplay(long targetId, string targetName)
        {
            if (targetText != null)
                targetText.text = targetId > 0 ? "目标: " + targetName : "";
        }

        private static string GetPkModeName(int mode)
        {
            switch (mode)
            {
                case PkMode.Peace: return "和平";
                case PkMode.Team: return "组队";
                case PkMode.Guild: return "帮派";
                case PkMode.All: return "全体";
                default: return "未知";
            }
        }

    }
}
