using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using Client.Core;
using Client.Game;

namespace Client.Login
{
    public class RoleSelectPanel : UIPanel
    {
        private List<RoleData> roles = new List<RoleData>();
        private int selectedRoleIndex = -1;
        private GameObject[] roleSlots = new GameObject[4];
        private Button enterBtn;
        private Text selectTip;

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

            var slotsArea = UIBuilder.CreateSection(transform, "SlotsArea", "已有角色 (2/4)", 1150, 420);
            slotsArea.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 30);

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

            LoadMockRoles();
            RefreshSlots();

            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

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

        private void LoadMockRoles()
        {
            roles.Add(new RoleData { RoleId = 1, Name = "剑心", Level = 35, Job = "剑士" });
            roles.Add(new RoleData { RoleId = 2, Name = "月影", Level = 22, Job = "法师" });
        }

        private void RefreshSlots()
        {
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
                    infoLabel.text = $"Lv.{roles[i].Level}";
                    avatarText.text = roles[i].Name[0].ToString();
                    jobTag.text = $"[ {roles[i].Job} ]";
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
            selectedRoleIndex = index;
            if (index >= roles.Count)
            {
                CreateNewRole(index);
                return;
            }
            enterBtn.interactable = true;
            selectTip.text = $"已选择: {roles[index].Name} (Lv.{roles[index].Level} {roles[index].Job})";
            selectTip.color = UIBuilder.Gold;
            RefreshSlots();
        }

        private void CreateNewRole(int slotIndex)
        {
            string[] defaultNames = { "勇者", "旅人", "冒险家", "探索者" };
            string[] jobs = { "战士", "法师", "弓手", "刺客" };
            int nameIndex = slotIndex - roles.Count;
            if (nameIndex < defaultNames.Length)
            {
                roles.Add(new RoleData
                {
                    RoleId = System.DateTime.Now.Ticks,
                    Name = defaultNames[nameIndex],
                    Level = 1,
                    Job = jobs[nameIndex]
                });
                selectedRoleIndex = roles.Count - 1;
                enterBtn.interactable = true;
                selectTip.text = $"已创建并选择: {defaultNames[nameIndex]} ({jobs[nameIndex]})";
                selectTip.color = UIBuilder.Gold;
                RefreshSlots();
            }
        }

        private void OnEnterGame()
        {
            if (selectedRoleIndex < 0 || selectedRoleIndex >= roles.Count) return;
            var role = roles[selectedRoleIndex];
            Debug.Log($"[RoleSelectPanel] 选择角色: {role.Name} (Lv.{role.Level} {role.Job})");
            GameData.CurrentRole = role;
            UIManager.Instance.ClearAll();
            SceneManager.LoadScene("GameScene");
        }

        private void OnBack()
        {
            selectedRoleIndex = -1;
            UIManager.Instance.ReplacePanel<LoginPanel>();
        }
    }
}
