using UnityEngine;
using UnityEngine.UI;
using Client.Logic;
using Shared.Common.Protos;

namespace Client.View
{
    public partial class MainPanel : UIPanel
    {
        private Text chatDisplay;
        private InputField chatInput;
        private Text chatTitle;

        private Text roleNameText;
        private Text roleJobText;
        private Text roleLevelText;
        private Text coordText;
        private VirtualJoystick joystick;

        // 战斗界面
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
            RefreshChatDisplay();
            RefreshChannelDisplay();
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
    }
}
