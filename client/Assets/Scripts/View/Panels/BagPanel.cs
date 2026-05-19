using UnityEngine;
using UnityEngine.UI;
using Shared.Common.Config;
using Shared.Core.Config;
using Client.Logic;

namespace Client.View
{
    public class BagPanel : UIPanel, IBagPanel
    {
        private const int COLS = 8;
        private const int ROWS = 8;
        private const float SLOT_SIZE = 68f;
        private const float SLOT_GAP = 4f;

        private Image[] _slotBgs = new Image[COLS * ROWS];
        private Image[] _slotBorders = new Image[COLS * ROWS];
        private Text[] _slotNameTexts = new Text[COLS * ROWS];
        private Text[] _slotCountTexts = new Text[COLS * ROWS];
        private Text _capacityText;

        public override void OnInit()
        {
            UIBuilder.CreateFullScreenBg(transform, new Color(0, 0, 0, 0.6f));

            float panelW = 640f;
            float panelH = 720f;
            var (panelRoot, content) = UIBuilder.CreateBorderedPanel(transform, "BagCard",
                panelW, panelH, UIBuilder.PanelBg, UIBuilder.GoldDark, 2f);
            panelRoot.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

            var titleBar = UIBuilder.CreateTitleBar(content, "TitleBar", "背 包", panelW, 50);
            var tbRect = titleBar.GetComponent<RectTransform>();
            tbRect.anchorMin = new Vector2(0, 1);
            tbRect.anchorMax = new Vector2(1, 1);
            tbRect.pivot = new Vector2(0.5f, 1);
            tbRect.offsetMin = new Vector2(0, -50);
            tbRect.offsetMax = Vector2.zero;

            _capacityText = UIBuilder.CreateText(titleBar.transform, "CapText", "0/100", 16,
                UIBuilder.TextDim, TextAnchor.MiddleRight);
            var capRect = _capacityText.GetComponent<RectTransform>();
            capRect.anchorMin = new Vector2(1, 0);
            capRect.anchorMax = new Vector2(1, 1);
            capRect.pivot = new Vector2(1, 0.5f);
            capRect.offsetMin = new Vector2(-80, 0);
            capRect.offsetMax = new Vector2(-10, 0);

            float gridW = COLS * (SLOT_SIZE + SLOT_GAP) - SLOT_GAP;
            float gridH = ROWS * (SLOT_SIZE + SLOT_GAP) - SLOT_GAP;

            var gridObj = new GameObject("Grid");
            gridObj.transform.SetParent(content, false);
            var gridRect = gridObj.AddComponent<RectTransform>();
            gridRect.sizeDelta = new Vector2(gridW, gridH);
            gridRect.anchorMin = new Vector2(0.5f, 1);
            gridRect.anchorMax = new Vector2(0.5f, 1);
            gridRect.pivot = new Vector2(0.5f, 1);
            gridRect.anchoredPosition = new Vector2(0, -60);

            for (int row = 0; row < ROWS; row++)
            {
                for (int col = 0; col < COLS; col++)
                {
                    int idx = row * COLS + col;
                    float x = col * (SLOT_SIZE + SLOT_GAP);
                    float y = -row * (SLOT_SIZE + SLOT_GAP);

                    var slotObj = new GameObject("Slot_" + idx);
                    slotObj.transform.SetParent(gridObj.transform, false);
                    var slotRect = slotObj.AddComponent<RectTransform>();
                    slotRect.sizeDelta = new Vector2(SLOT_SIZE, SLOT_SIZE);
                    slotRect.pivot = new Vector2(0, 1);
                    slotRect.anchoredPosition = new Vector2(x, y);

                    var bgImg = slotObj.AddComponent<Image>();
                    bgImg.color = new Color(0.08f, 0.08f, 0.15f, 0.9f);
                    _slotBgs[idx] = bgImg;

                    var btn = slotObj.AddComponent<Button>();
                    var colors = btn.colors;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
                    colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
                    colors.fadeDuration = 0.1f;
                    btn.colors = colors;

                    var borderObj = new GameObject("Border");
                    borderObj.transform.SetParent(slotObj.transform, false);
                    var borderRect = borderObj.AddComponent<RectTransform>();
                    UIBuilder.SetStretch(borderRect);
                    var borderImg = borderObj.AddComponent<Image>();
                    borderImg.color = new Color(0.3f, 0.3f, 0.35f, 0.5f);
                    borderImg.raycastTarget = false;
                    _slotBorders[idx] = borderImg;

                    var nameText = UIBuilder.CreateText(slotObj.transform, "Name", "", 12,
                        UIBuilder.TextWhite, TextAnchor.MiddleCenter);
                    var nRect = nameText.GetComponent<RectTransform>();
                    nRect.anchorMin = Vector2.zero;
                    nRect.anchorMax = new Vector2(1f, 0.7f);
                    nRect.offsetMin = new Vector2(2, 0);
                    nRect.offsetMax = new Vector2(-2, 0);
                    _slotNameTexts[idx] = nameText;

                    var countText = UIBuilder.CreateText(slotObj.transform, "Count", "", 11,
                        UIBuilder.Gold, TextAnchor.LowerRight, FontStyle.Bold);
                    var cRect = countText.GetComponent<RectTransform>();
                    cRect.anchorMin = Vector2.zero;
                    cRect.anchorMax = Vector2.one;
                    cRect.offsetMin = new Vector2(2, 1);
                    cRect.offsetMax = new Vector2(-3, -1);
                    _slotCountTexts[idx] = countText;

                    int slotIndex = idx;
                    btn.onClick.AddListener(() => OnSlotClick(slotIndex));
                }
            }

            float bottomY = -60 - gridH - 20;

            var tidyBtn = UIBuilder.CreateButton(content, "TidyBtn", "整 理", 120, 40, 18,
                UIBuilder.ButtonNormal, UIBuilder.Gold);
            tidyBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-80, bottomY);
            tidyBtn.onClick.AddListener(OnTidyClick);

            var closeBtn = UIBuilder.CreateButton(content, "CloseBtn", "关 闭", 120, 40, 18,
                UIBuilder.ButtonNormal, UIBuilder.TextGray);
            closeBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(80, bottomY);
            closeBtn.onClick.AddListener(OnCloseClick);

            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        public override void OnShow()
        {
            BagController.Instance.OnDataChanged += RefreshGrid;
            BagController.Instance.QueryBag();
        }

        public override void OnHide()
        {
            BagController.Instance.OnDataChanged -= RefreshGrid;
        }

        private void RefreshGrid()
        {
            var items = BagController.Instance.Items;
            var itemMap = BagController.Instance.ItemMap;
            int capacity = BagController.Instance.Capacity;

            _capacityText.text = items.Count + "/" + capacity;

            int totalSlots = COLS * ROWS;
            for (int i = 0; i < totalSlots; i++)
            {
                bool hasItem = itemMap.TryGetValue(i, out var item);

                if (hasItem)
                {
                    var cfg = ConfigManager.Get<ItemConfig>(item.ConfigId);
                    string itemName = cfg != null ? cfg.Name : "?";
                    Color qc = GetQualityColor(cfg != null ? cfg.Quality : 0);

                    _slotBgs[i].color = new Color(0.1f, 0.1f, 0.2f, 0.95f);
                    _slotBorders[i].color = qc;

                    string display = itemName.Length > 2 ? itemName.Substring(0, 2) : itemName;
                    _slotNameTexts[i].text = display;
                    _slotNameTexts[i].color = qc;
                    _slotCountTexts[i].text = item.Count > 1 ? item.Count.ToString() : "";
                }
                else
                {
                    _slotBgs[i].color = new Color(0.08f, 0.08f, 0.15f, 0.9f);
                    _slotBorders[i].color = new Color(0.3f, 0.3f, 0.35f, 0.5f);
                    _slotNameTexts[i].text = "";
                    _slotCountTexts[i].text = "";
                }
            }
        }

        private void OnSlotClick(int slotIndex)
        {
            if (!BagController.Instance.ItemMap.TryGetValue(slotIndex, out var item)) return;

            var cfg = ConfigManager.Get<ItemConfig>(item.ConfigId);
            string itemName = cfg != null ? cfg.Name : "物品";

            UIMessageBox.Show("使用物品", "使用 " + itemName + " x1 ?",
                () => { BagController.Instance.UseItem(slotIndex, 1); });
        }

        private void OnTidyClick()
        {
            BagController.Instance.TidyBag();
        }

        private void OnCloseClick()
        {
            UIManager.Instance.Back();
        }

        private static Color GetQualityColor(int quality)
        {
            switch (quality)
            {
                case 1: return new Color(0.3f, 0.9f, 0.3f);
                case 2: return new Color(0.3f, 0.5f, 1f);
                case 3: return new Color(0.7f, 0.3f, 0.9f);
                case 4: return new Color(1f, 0.6f, 0.1f);
                default: return new Color(0.55f, 0.55f, 0.55f);
            }
        }

    }
}
