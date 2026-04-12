using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using Client.Core;
using Client.Game;
using Client.Network;
using GameShared.Protos;

namespace Client.Login
{
    public class RoleSelectPanel : UIPanel
    {
        private List<RoleData> roles = new List<RoleData>();
        private int selectedRoleIndex = -1;
        private GameObject[] roleSlots = new GameObject[4];
        private Button enterBtn;
        private Text selectTip;
        private Text slotsCountText;

        // Create role dialog
        private GameObject createRoleDialog;
        private InputField createNameInput;
        private int selectedJob = 0;
        private Text[] jobLabels = new Text[4];
        private int createSlotIndex;

        private static readonly string[] JobNames = { "战士", "法师", "弓手", "刺客" };
        private static readonly Color[] JobColors = {
            new Color(0.9f, 0.4f, 0.3f),  // 战士 - 红
            new Color(0.3f, 0.5f, 0.95f), // 法师 - 蓝
            new Color(0.3f, 0.85f, 0.4f), // 弓手 - 绿
            new Color(0.75f, 0.3f, 0.85f) // 刺客 - 紫
        };

        public override void OnInit()
        {
            UIBuilder.CreateFullScreenBg(transform, UIBuilder.DarkBg);

            var bgDeco = UIBuilder.CreateText(transform, "BgDeco", "SELECT YOUR HERO", 60, new Color(1f, 0.85f, 0.3f, 0.04f),
                TextAnchor.MiddleCenter, FontStyle.Bold);
            var bgRect = bgDeco.GetComponent<RectTransform>();
            bgRect.anchoredPosition = new Vector2(0, 250);
            bgRect.sizeDelta = new Vector2(1200, 100);

            var topBar = UIBuilder.CreatePanel(transform, "TopBar", 1200, 60, new Color(0.04f, 0.04f, 0.1f, 0.7f));
            var topBarRect = topBar.GetComponent<RectTransform>();
            topBarRect.anchorMin = new Vector2(0.5f, 1);
            topBarRect.anchorMax = new Vector2(0.5f, 1);
            topBarRect.anchoredPosition = new Vector2(0, -30);
            var title = UIBuilder.CreateText(topBar.transform, "Title", "— 选 择 角 色 —", 30, UIBuilder.Gold,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.SetStretch(title.GetComponent<RectTransform>());

            var topDiv = UIBuilder.CreateDivider(topBar.transform, "Div", 400);
            var tdRect = topDiv.GetComponent<RectTransform>();
            tdRect.anchorMin = new Vector2(0, 0);
            tdRect.anchorMax = new Vector2(1, 0);
            tdRect.offsetMin = Vector2.zero;
            tdRect.offsetMax = Vector2.zero;

            var slotsArea = UIBuilder.CreateSection(transform, "SlotsArea", "已有角色 (0/4)", 1150, 420);
            slotsArea.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 30);
            slotsCountText = slotsArea.GetComponentInChildren<Text>();

            for (int i = 0; i < 4; i++)
            {
                roleSlots[i] = CreateRoleSlot(slotsArea.transform, i);
                var slotRect = roleSlots[i].GetComponent<RectTransform>();
                float xPos = (i - 1.5f) * 265;
                slotRect.anchoredPosition = new Vector2(xPos, -20);
            }

            selectTip = UIBuilder.CreateText(transform, "SelectTip", "请选择一个角色进入游戏", 18, UIBuilder.TextDim);
            var stRect = selectTip.GetComponent<RectTransform>();
            stRect.anchoredPosition = new Vector2(0, -200);
            stRect.sizeDelta = new Vector2(400, 30);

            var btnArea = UIBuilder.CreatePanel(transform, "BtnArea", 500, 70, new Color(0.04f, 0.04f, 0.1f, 0.5f));
            btnArea.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -260);

            enterBtn = UIBuilder.CreateButton(btnArea.transform, "EnterBtn", "进 入 游 戏", 280, 50, 24,
                UIBuilder.AccentBlue, UIBuilder.Gold);
            enterBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(30, 0);
            enterBtn.onClick.AddListener(OnEnterGame);
            enterBtn.interactable = false;

            var backBtn = UIBuilder.CreateButton(btnArea.transform, "BackBtn", "< 返回登录", 140, 40, 16,
                UIBuilder.ButtonNormal, UIBuilder.TextGray);
            backBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-190, 0);
            backBtn.onClick.AddListener(OnBack);

            var footer = UIBuilder.CreateText(transform, "Footer", "点击角色卡片选择  |  点击空槽位创建新角色", 13,
                UIBuilder.TextDim, TextAnchor.MiddleCenter);
            var fRect = footer.GetComponent<RectTransform>();
            fRect.anchorMin = new Vector2(0, 0);
            fRect.anchorMax = new Vector2(1, 0);
            fRect.pivot = new Vector2(0.5f, 0);
            fRect.anchoredPosition = new Vector2(0, 15);
            fRect.sizeDelta = new Vector2(500, 20);

            // Create role dialog (hidden by default)
            CreateRoleDialog();

            RefreshSlots();

            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        public override void OnShow()
        {
            RegisterHandlers();
            RequestRoleList();
        }

        public override void OnHide()
        {
            UnregisterHandlers();
            HideCreateDialog();
        }

        private void RegisterHandlers()
        {
            var nc = NetClient.Instance;
            if (nc != null && nc.Dispatcher != null)
            {
                nc.Dispatcher.Register<RoleListResp>(MsgId.RoleListResp, HandleRoleListResp);
                nc.Dispatcher.Register<CreateRoleResp>(MsgId.CreateRoleResp, HandleCreateRoleResp);
                nc.Dispatcher.Register<SelectRoleResp>(MsgId.SelectRoleResp, HandleSelectRoleResp);
                nc.Dispatcher.Register<EnterMapAck>(MsgId.EnterMapAck, HandleEnterMapAck);
            }
        }

        private void UnregisterHandlers()
        {
            var nc = NetClient.Instance;
            if (nc != null && nc.Dispatcher != null)
            {
                nc.Dispatcher.Unregister(MsgId.RoleListResp);
                nc.Dispatcher.Unregister(MsgId.CreateRoleResp);
                nc.Dispatcher.Unregister(MsgId.SelectRoleResp);
                nc.Dispatcher.Unregister(MsgId.EnterMapAck);
            }
        }

        private void RequestRoleList()
        {
            selectTip.text = "正在获取角色列表...";
            selectTip.color = UIBuilder.TextDim;
            enterBtn.interactable = false;
            var nc = NetClient.Instance;
            if (nc != null && nc.IsConnected)
                nc.Send(new RoleListReq());
            else
                selectTip.text = "网络未连接，请返回重试";
        }

        // --- Network Handlers ---

        private void HandleRoleListResp(RoleListResp resp)
        {
            if (resp.Code == 0)
            {
                roles.Clear();
                foreach (var info in resp.Roles)
                {
                    roles.Add(new RoleData
                    {
                        RoleId = info.RoleId,
                        Name = info.Name,
                        Level = info.Level,
                        Job = GameData.GetJobName(info.Job)
                    });
                }
                selectedRoleIndex = -1;
                enterBtn.interactable = false;
                selectTip.text = roles.Count > 0 ? "请选择一个角色进入游戏" : "没有角色，请创建新角色";
                selectTip.color = UIBuilder.TextDim;
                RefreshSlots();
            }
            else
            {
                selectTip.text = resp.Message;
                selectTip.color = Color.red;
            }
        }

        private void HandleCreateRoleResp(CreateRoleResp resp)
        {
            if (resp.Code == 0)
            {
                var info = resp.Role;
                roles.Add(new RoleData
                {
                    RoleId = info.RoleId,
                    Name = info.Name,
                    Level = info.Level,
                    Job = GameData.GetJobName(info.Job)
                });
                selectedRoleIndex = roles.Count - 1;
                enterBtn.interactable = true;
                selectTip.text = $"已创建并选择: {info.Name} ({GameData.GetJobName(info.Job)})";
                selectTip.color = UIBuilder.Gold;
                HideCreateDialog();
                RefreshSlots();
            }
            else
            {
                selectTip.text = resp.Message;
                selectTip.color = Color.red;
            }
        }

        private void HandleSelectRoleResp(SelectRoleResp resp)
        {
            if (resp.Code == 0)
            {
                GameData.CurrentRole = new RoleData
                {
                    RoleId = resp.RoleId,
                    Name = resp.Name,
                    Level = resp.Level,
                    Job = GameData.GetJobName(resp.Job)
                };
                Debug.Log(string.Format("[RoleSelectPanel] 选角成功: {0} Lv.{1}，正在进入地图...", resp.Name, resp.Level));
                selectTip.text = "正在进入地图...";
                selectTip.color = UIBuilder.TextDim;

                // 发送进图请求（默认进入新手村）
                NetClient.Instance.Send(new EnterMapReq { MapCfgId = 1, Line = 1 });
            }
            else
            {
                selectTip.text = resp.Message;
                selectTip.color = Color.red;
                enterBtn.interactable = true;
            }
        }

        private void HandleEnterMapAck(EnterMapAck resp)
        {
            if (resp.Code == MapErrorCode.Ok)
            {
                Debug.Log(string.Format("[RoleSelectPanel] 进图成功: MapCfgId={0} Line={1} Spawn=({2},{3})",
                    resp.MapCfgId, resp.Line, resp.X, resp.Y));

                // 缓存进图数据
                GameData.CurrentMap = new EnterMapData
                {
                    MapCfgId = resp.MapCfgId,
                    Line = resp.Line,
                    SpawnX = resp.X,
                    SpawnY = resp.Y
                };

                UIManager.Instance.ClearAll();
                SceneManager.LoadScene("GameScene");
            }
            else
            {
                Debug.LogError(string.Format("[RoleSelectPanel] 进图失败: Code={0}", resp.Code));
                selectTip.text = string.Format("进入地图失败 (错误码:{0})", resp.Code);
                selectTip.color = Color.red;
                enterBtn.interactable = true;
            }
        }

        // --- Create Role Dialog ---

        private void CreateRoleDialog()
        {
            createRoleDialog = UIBuilder.CreatePanel(transform, "CreateRoleDialog", 380, 320,
                new Color(0.05f, 0.05f, 0.12f, 0.98f)).gameObject;
            createRoleDialog.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 30);

            var border = UIBuilder.CreatePanel(createRoleDialog.transform, "Border", 384, 324,
                new Color(0f, 0f, 0f, 0f));
            border.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            var borderImg = border.GetComponent<Image>();
            borderImg.color = UIBuilder.GoldDark;

            var dlgContent = createRoleDialog.transform;

            var dlgTitle = UIBuilder.CreateText(dlgContent, "DialogTitle", "创 建 角 色", 22, UIBuilder.Gold,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            dlgTitle.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 130);
            dlgTitle.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 30);

            var nameLbl = UIBuilder.CreateText(dlgContent, "NameLabel", "角色名称", 15, UIBuilder.TextDim,
                TextAnchor.MiddleLeft);
            nameLbl.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 85);
            nameLbl.GetComponent<RectTransform>().sizeDelta = new Vector2(320, 20);

            createNameInput = UIBuilder.CreateInputField(dlgContent, "NameInput", "请输入角色名(1-12字)", 320, 38, 16);
            createNameInput.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 50);

            var jobLbl = UIBuilder.CreateText(dlgContent, "JobLabel", "选择职业", 15, UIBuilder.TextDim,
                TextAnchor.MiddleLeft);
            jobLbl.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 15);
            jobLbl.GetComponent<RectTransform>().sizeDelta = new Vector2(320, 20);

            for (int i = 0; i < 4; i++)
            {
                int jobIdx = i;
                var jobBtn = UIBuilder.CreateButton(dlgContent, $"JobBtn_{i}", JobNames[i], 70, 34, 14,
                    new Color(0.12f, 0.12f, 0.2f, 1f), JobColors[i]);
                float jx = (i - 1.5f) * 80;
                jobBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(jx, -25);
                jobLabels[i] = jobBtn.GetComponentInChildren<Text>();
                jobBtn.onClick.AddListener(() =>
                {
                    selectedJob = jobIdx;
                    UpdateJobSelection();
                });
            }
            UpdateJobSelection();

            var confirmBtn = UIBuilder.CreateButton(dlgContent, "ConfirmBtn", "确 认 创 建", 140, 42, 16,
                UIBuilder.AccentBlue, UIBuilder.Gold);
            confirmBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(80, -80);
            confirmBtn.onClick.AddListener(OnConfirmCreateRole);

            var cancelBtn = UIBuilder.CreateButton(dlgContent, "CancelBtn", "取 消", 100, 42, 16,
                UIBuilder.ButtonNormal, UIBuilder.TextGray);
            cancelBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-60, -80);
            cancelBtn.onClick.AddListener(HideCreateDialog);

            createRoleDialog.SetActive(false);
        }

        private void UpdateJobSelection()
        {
            for (int i = 0; i < 4; i++)
            {
                if (jobLabels[i] != null)
                {
                    jobLabels[i].fontSize = (i == selectedJob) ? 16 : 14;
                    jobLabels[i].fontStyle = (i == selectedJob) ? FontStyle.Bold : FontStyle.Normal;
                }
            }
        }

        private void ShowCreateDialog(int slotIndex)
        {
            createSlotIndex = slotIndex;
            selectedJob = 0;
            if (createNameInput != null) createNameInput.text = "";
            UpdateJobSelection();
            createRoleDialog.SetActive(true);
        }

        private void HideCreateDialog()
        {
            if (createRoleDialog != null)
                createRoleDialog.SetActive(false);
        }

        private void OnConfirmCreateRole()
        {
            string name = (createNameInput != null) ? createNameInput.text.Trim() : "";
            if (string.IsNullOrEmpty(name))
            {
                selectTip.text = "请输入角色名称";
                selectTip.color = Color.red;
                return;
            }
            if (name.Length > 12)
            {
                selectTip.text = "角色名称最多12个字符";
                selectTip.color = Color.red;
                return;
            }
            selectTip.text = "正在创建角色...";
            selectTip.color = UIBuilder.TextDim;
            NetClient.Instance.Send(new CreateRoleReq { Name = name, Job = selectedJob });
        }

        // --- Slot UI ---

        private GameObject CreateRoleSlot(Transform parent, int index)
        {
            var (slot, content) = UIBuilder.CreateBorderedPanel(parent, $"RoleSlot_{index}", 230, 350,
                new Color(0.06f, 0.06f, 0.14f, 0.9f), new Color(0.25f, 0.22f, 0.35f, 0.7f), 2f);

            var avatarBg = new GameObject("AvatarBg");
            avatarBg.transform.SetParent(content, false);
            var avatarRect = avatarBg.AddComponent<RectTransform>();
            avatarRect.sizeDelta = new Vector2(150, 150);
            avatarRect.anchoredPosition = new Vector2(0, 75);
            avatarBg.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.22f, 1f);

            var avatarText = UIBuilder.CreateText(avatarBg.transform, "AvatarText", "?", 48,
                UIBuilder.TextGray, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.SetStretch(avatarText.GetComponent<RectTransform>());

            var div = UIBuilder.CreateDivider(content, "Div", 190, 1f);
            div.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -15);

            var nameLabel = UIBuilder.CreateText(content, "NameLabel", "", 22, UIBuilder.TextWhite,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            var nameRect = nameLabel.GetComponent<RectTransform>();
            nameRect.anchoredPosition = new Vector2(0, -45);
            nameRect.sizeDelta = new Vector2(210, 28);

            var infoLabel = UIBuilder.CreateText(content, "InfoLabel", "", 16, UIBuilder.TextGray,
                TextAnchor.MiddleCenter);
            var infoRect = infoLabel.GetComponent<RectTransform>();
            infoRect.anchoredPosition = new Vector2(0, -75);
            infoRect.sizeDelta = new Vector2(210, 22);

            var jobTag = UIBuilder.CreateText(content, "JobTag", "", 13, UIBuilder.TextDim,
                TextAnchor.MiddleCenter);
            var jtRect = jobTag.GetComponent<RectTransform>();
            jtRect.anchoredPosition = new Vector2(0, -100);
            jtRect.sizeDelta = new Vector2(210, 20);

            var btn = slot.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            btn.colors = colors;
            btn.onClick.AddListener(() => OnSlotClick(index));

            var selectedMark = new GameObject("SelectedMark");
            selectedMark.transform.SetParent(content, false);
            UIBuilder.SetStretch(selectedMark.AddComponent<RectTransform>());
            selectedMark.AddComponent<Image>().color = new Color(0.886f, 0.718f, 0.078f, 0.12f);
            selectedMark.SetActive(false);

            return slot;
        }

        private void RefreshSlots()
        {
            if (slotsCountText != null)
                slotsCountText.text = $"已有角色 ({roles.Count}/4)";

            for (int i = 0; i < 4; i++)
            {
                var slot = roleSlots[i];
                var inner = slot.transform.Find("Inner");
                var nameLabel = inner.Find("NameLabel").GetComponent<Text>();
                var infoLabel = inner.Find("InfoLabel").GetComponent<Text>();
                var avatarText = inner.Find("AvatarBg/AvatarText").GetComponent<Text>();
                var jobTag = inner.Find("JobTag").GetComponent<Text>();
                var selectedMark = inner.Find("SelectedMark").gameObject;

                if (i < roles.Count)
                {
                    nameLabel.text = roles[i].Name;
                    nameLabel.color = UIBuilder.TextWhite;
                    infoLabel.text = string.Format("Lv.{0}", roles[i].Level);
                    avatarText.text = roles[i].Name[0].ToString();
                    avatarText.fontSize = 48;
                    jobTag.text = string.Format("[ {0} ]", roles[i].Job);
                }
                else
                {
                    nameLabel.text = "创建角色";
                    nameLabel.color = UIBuilder.TextDim;
                    infoLabel.text = "";
                    avatarText.text = "+";
                    avatarText.fontSize = 60;
                    jobTag.text = "点击创建";
                }

                selectedMark.SetActive(i == selectedRoleIndex);
            }
        }

        private void OnSlotClick(int index)
        {
            if (index < roles.Count)
            {
                selectedRoleIndex = index;
                enterBtn.interactable = true;
                selectTip.text = string.Format("已选择: {0} (Lv.{1} {2})", roles[index].Name, roles[index].Level, roles[index].Job);
                selectTip.color = UIBuilder.Gold;
                RefreshSlots();
            }
            else
            {
                ShowCreateDialog(index);
            }
        }

        private void OnEnterGame()
        {
            if (selectedRoleIndex < 0 || selectedRoleIndex >= roles.Count) return;
            enterBtn.interactable = false;
            var role = roles[selectedRoleIndex];
            Debug.Log(string.Format("[RoleSelectPanel] 请求进入游戏: {0} (Lv.{1} {2})", role.Name, role.Level, role.Job));
            selectTip.text = "正在进入游戏...";
            selectTip.color = UIBuilder.TextDim;
            NetClient.Instance.Send(new SelectRoleReq { RoleId = role.RoleId });
        }

        private void OnBack()
        {
            selectedRoleIndex = -1;
            HideCreateDialog();
            UIManager.Instance.ReplacePanel<LoginPanel>();
        }
    }
}
