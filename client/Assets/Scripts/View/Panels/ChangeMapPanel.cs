using UnityEngine;
using UnityEngine.UI;
using Client.Logic;
using Shared.Common.Config;
using Shared.Core.Config;

namespace Client.View
{
    public class ChangeMapPanel : UIPanel
    {
        public override void OnInit()
        {
            UIBuilder.CreateFullScreenBg(transform, new Color(0, 0, 0, 0.7f));

            var panel = UIBuilder.CreatePanel(transform, "Panel", 400, 350,
                new Color(0.08f, 0.08f, 0.15f, 0.95f));
            var pRect = panel.GetComponent<RectTransform>();
            pRect.anchorMin = new Vector2(0.5f, 0.5f);
            pRect.anchorMax = new Vector2(0.5f, 0.5f);
            pRect.anchoredPosition = Vector2.zero;

            // Title
            var title = UIBuilder.CreateText(panel.transform, "Title", "地图传送", 22, UIBuilder.Gold,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            var tRect = title.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0, 1);
            tRect.anchorMax = new Vector2(1, 1);
            tRect.pivot = new Vector2(0.5f, 1);
            tRect.anchoredPosition = new Vector2(0, -15);
            tRect.sizeDelta = new Vector2(400, 30);

            UIBuilder.CreateDivider(panel.transform, "Div", 380);

            // Map buttons
            var currentMap = GameData.CurrentMap;
            var maps = ConfigManager.GetAll<MapConfig>();
            for (int i = 0; i < maps.Count; i++)
            {
                CreateMapButton(panel.transform, maps[i], i,
                    currentMap != null && currentMap.MapCfgId == maps[i].Id);
            }

            // Close button
            var closeBtn = UIBuilder.CreateButton(panel.transform, "CloseBtn", "关闭", 120, 36, 16,
                UIBuilder.ButtonNormal, UIBuilder.TextWhite);
            var cRect = closeBtn.GetComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0.5f, 0);
            cRect.anchorMax = new Vector2(0.5f, 0);
            cRect.pivot = new Vector2(0.5f, 0);
            cRect.anchoredPosition = new Vector2(0, 15);
            closeBtn.onClick.AddListener(() => UIManager.Instance.HidePanel<ChangeMapPanel>());
        }

        private void CreateMapButton(Transform parent, MapConfig map, int index, bool isCurrent)
        {
            int yOffset = -70 - index * 75;

            var btnBg = new GameObject("MapBtn_" + map.Id);
            btnBg.transform.SetParent(parent, false);
            var bgRect = btnBg.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.5f, 1);
            bgRect.anchorMax = new Vector2(0.5f, 1);
            bgRect.pivot = new Vector2(0.5f, 1);
            bgRect.anchoredPosition = new Vector2(0, yOffset);
            bgRect.sizeDelta = new Vector2(350, 60);
            btnBg.AddComponent<Image>().color = isCurrent
                ? new Color(0.15f, 0.25f, 0.15f, 0.8f)
                : new Color(0.12f, 0.12f, 0.2f, 0.8f);

            var nameText = UIBuilder.CreateText(btnBg.transform, "Name", map.Name, 18,
                isCurrent ? new Color(0.3f, 0.9f, 0.3f) : UIBuilder.TextWhite,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            var nRect = nameText.GetComponent<RectTransform>();
            nRect.anchorMin = new Vector2(0, 0.5f);
            nRect.anchorMax = new Vector2(0, 0.5f);
            nRect.pivot = new Vector2(0, 0.5f);
            nRect.anchoredPosition = new Vector2(20, 5);
            nRect.sizeDelta = new Vector2(200, 24);

            var infoText = UIBuilder.CreateText(btnBg.transform, "Info",
                string.Format("{0}x{1}  出生点({2},{3})", map.Width, map.Height, map.BirthX, map.BirthY),
                12, UIBuilder.TextDim, TextAnchor.MiddleLeft);
            var iRect = infoText.GetComponent<RectTransform>();
            iRect.anchorMin = new Vector2(0, 0.5f);
            iRect.anchorMax = new Vector2(0, 0.5f);
            iRect.pivot = new Vector2(0, 0.5f);
            iRect.anchoredPosition = new Vector2(20, -12);
            iRect.sizeDelta = new Vector2(250, 18);

            if (isCurrent)
            {
                var curText = UIBuilder.CreateText(btnBg.transform, "Current", "当前地图", 14,
                    new Color(0.3f, 0.9f, 0.3f), TextAnchor.MiddleCenter);
                var ctRect = curText.GetComponent<RectTransform>();
                ctRect.anchorMin = new Vector2(1, 0.5f);
                ctRect.anchorMax = new Vector2(1, 0.5f);
                ctRect.pivot = new Vector2(1, 0.5f);
                ctRect.anchoredPosition = new Vector2(-20, 0);
                ctRect.sizeDelta = new Vector2(100, 24);
            }
            else
            {
                var tpBtn = UIBuilder.CreateButton(btnBg.transform, "TpBtn", "传送", 80, 30, 14,
                    UIBuilder.AccentBlue, UIBuilder.TextWhite);
                var tpRect = tpBtn.GetComponent<RectTransform>();
                tpRect.anchorMin = new Vector2(1, 0.5f);
                tpRect.anchorMax = new Vector2(1, 0.5f);
                tpRect.pivot = new Vector2(1, 0.5f);
                tpRect.anchoredPosition = new Vector2(-20, 0);

                int cfgId = map.Id;
                tpBtn.onClick.AddListener(() => OnTeleport(cfgId));
            }
        }

        private void OnTeleport(int targetMapCfgId)
        {
            _ = MapController.Instance.ChangeMap(targetMapCfgId);

            UIManager.Instance.HidePanel<ChangeMapPanel>();
            UIToast.Show("正在传送...");
        }
    }
}
