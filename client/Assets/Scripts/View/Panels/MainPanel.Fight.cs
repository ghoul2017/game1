using UnityEngine;
using UnityEngine.UI;
using Client.Logic;
using Shared.Common.Protos;

namespace Client.View
{
    public partial class MainPanel
    {
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

            FightController.Instance.UseSkill(skillId, pc.CurrentGridX, pc.CurrentGridY, GameData.SelectedTargetId);
        }

        private void OnTogglePkMode()
        {
            FightController.Instance.TogglePkMode();
        }

        // ==================== Fight UI Updates ====================

        internal void UpdateHpDisplay()
        {
            int hp = GameData.CurrentHp;
            int maxHp = GameData.MaxHp;
            if (hpSlider != null)
                hpSlider.value = maxHp > 0 ? (float)hp / maxHp : 0f;
            if (hpText != null)
                hpText.text = string.Format("{0}/{1}", hp, maxHp);
        }

        public void UpdatePkModeDisplay()
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
