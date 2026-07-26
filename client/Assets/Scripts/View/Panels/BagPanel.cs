using System.Linq;
using Client.Logic;
using Shared.Common.Config;
using Shared.Common.Protos;
using Shared.Core.Config;
using UnityEngine;
using UnityEngine.UI;

namespace Client.View
{
    public class BagPanel : UIPanel
    {
        private const int Columns = 8;
        private const float SlotSize = 68f;
        private const float SlotGap = 4f;
        private const float GridLeftPadding = 20f;

        private GameObject _listContent;
        private Text _capacityText;

        public override void OnInit()
        {
            UIBuilder.CreateFullScreenBg(transform, new Color(0, 0, 0, 0.6f));

            const float panelWidth = 640f;
            const float panelHeight = 720f;
            var (panelRoot, content) = UIBuilder.CreateBorderedPanel(transform, "BagCard",
                panelWidth, panelHeight, UIBuilder.PanelBg, UIBuilder.GoldDark, 2f);
            panelRoot.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

            GameObject titleBar = UIBuilder.CreateTitleBar(content, "TitleBar", "背 包", panelWidth, 50f);
            RectTransform titleRect = titleBar.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.offsetMin = new Vector2(0, -50);
            titleRect.offsetMax = Vector2.zero;

            _capacityText = UIBuilder.CreateText(titleBar.transform, "CapacityText", "0 / 0", 16,
                UIBuilder.TextDim, TextAnchor.MiddleRight);
            RectTransform capacityRect = _capacityText.GetComponent<RectTransform>();
            capacityRect.anchorMin = new Vector2(1, 0);
            capacityRect.anchorMax = new Vector2(1, 1);
            capacityRect.pivot = new Vector2(1, 0.5f);
            capacityRect.offsetMin = new Vector2(-110, 0);
            capacityRect.offsetMax = new Vector2(-10, 0);

            GameObject listArea = UIBuilder.CreatePanel(content, "ListArea", panelWidth - 20f, 590f,
                new Color(0.06f, 0.06f, 0.12f, 0.9f));
            RectTransform listRect = listArea.GetComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0.5f, 1);
            listRect.anchorMax = new Vector2(0.5f, 1);
            listRect.pivot = new Vector2(0.5f, 1);
            listRect.anchoredPosition = new Vector2(0, -58f);
            listRect.sizeDelta = new Vector2(panelWidth - 20f, 590f);

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(listArea.transform, false);
            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(4, 4);
            viewportRect.offsetMax = new Vector2(-4, -4);
            viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            _listContent = new GameObject("Content");
            _listContent.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = _listContent.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;

            ScrollRect scrollRect = listArea.AddComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.scrollSensitivity = 30f;

            float bottomY = -panelHeight * 0.5f + 30f;
            Button closeButton = UIBuilder.CreateButton(content, "CloseButton", "关 闭", 120, 40, 18,
                UIBuilder.ButtonNormal, UIBuilder.TextGray);
            closeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, bottomY);
            closeButton.onClick.AddListener(OnCloseClick);

            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        public override void OnShow()
        {
            RefreshGrid();
            if (!BagController.Instance.IsLoaded)
                _ = BagController.Instance.QueryBag();
        }

        public override void OnHide()
        {
        }

        internal void RefreshGrid()
        {
            BagController controller = BagController.Instance;
            BagItemInfo[] items = controller.Items.Values
                .OrderBy(item => item.ConfigId)
                .ThenBy(item => item.Uid)
                .ToArray();

            _capacityText.text = items.Length + " / " + controller.Capacity;

            for (int index = _listContent.transform.childCount - 1; index >= 0; index--)
                Destroy(_listContent.transform.GetChild(index).gameObject);

            int rowCount = (items.Length + Columns - 1) / Columns;
            float contentHeight = rowCount == 0 ? 0 : rowCount * (SlotSize + SlotGap) - SlotGap + 16f;
            _listContent.GetComponent<RectTransform>().sizeDelta = new Vector2(0, contentHeight);

            for (int index = 0; index < items.Length; index++)
            {
                int row = index / Columns;
                int column = index % Columns;
                GameObject slot = CreateItemSlot(_listContent.transform, items[index]);
                RectTransform slotRect = slot.GetComponent<RectTransform>();
                slotRect.anchoredPosition = new Vector2(
                    GridLeftPadding + column * (SlotSize + SlotGap),
                    -8f - row * (SlotSize + SlotGap));
            }
        }

        private GameObject CreateItemSlot(Transform parent, BagItemInfo item)
        {
            GameObject slot = new GameObject("Item_" + item.Uid);
            slot.transform.SetParent(parent, false);
            RectTransform slotRect = slot.AddComponent<RectTransform>();
            slotRect.sizeDelta = new Vector2(SlotSize, SlotSize);
            slotRect.anchorMin = new Vector2(0, 1);
            slotRect.anchorMax = new Vector2(0, 1);
            slotRect.pivot = new Vector2(0, 1);

            Image background = slot.AddComponent<Image>();
            background.color = new Color(0.1f, 0.1f, 0.2f, 0.95f);

            Button button = slot.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.fadeDuration = 0.1f;
            button.colors = colors;

            ItemConfig config = ConfigManager.Get<ItemConfig>(item.ConfigId);
            string itemName = config != null ? config.Name : "?";
            Color qualityColor = GetQualityColor(config != null ? config.Quality : 0);

            GameObject borderObject = new GameObject("Border");
            borderObject.transform.SetParent(slot.transform, false);
            RectTransform borderRect = borderObject.AddComponent<RectTransform>();
            UIBuilder.SetStretch(borderRect);
            Image border = borderObject.AddComponent<Image>();
            border.color = qualityColor;
            border.raycastTarget = false;

            string displayName = itemName.Length > 2 ? itemName.Substring(0, 2) : itemName;
            Text nameText = UIBuilder.CreateText(slot.transform, "Name", displayName, 12,
                qualityColor, TextAnchor.MiddleCenter);
            RectTransform nameRect = nameText.GetComponent<RectTransform>();
            nameRect.anchorMin = Vector2.zero;
            nameRect.anchorMax = new Vector2(1f, 0.72f);
            nameRect.offsetMin = new Vector2(2, 0);
            nameRect.offsetMax = new Vector2(-2, 0);

            Text countText = UIBuilder.CreateText(slot.transform, "Count", item.Count.ToString(), 11,
                UIBuilder.Gold, TextAnchor.LowerRight, FontStyle.Bold);
            RectTransform countRect = countText.GetComponent<RectTransform>();
            countRect.anchorMin = Vector2.zero;
            countRect.anchorMax = Vector2.one;
            countRect.offsetMin = new Vector2(2, 1);
            countRect.offsetMax = new Vector2(-3, -1);

            long uid = item.Uid;
            button.onClick.AddListener(() => OnItemClick(uid));
            return slot;
        }

        private void OnItemClick(long uid)
        {
            BagItemInfo item = BagController.Instance.GetItem(uid);
            if (item == null)
                return;

            ItemConfig config = ConfigManager.Get<ItemConfig>(item.ConfigId);
            string itemName = config != null ? config.Name : "物品";
            UIMessageBox.Show("使用物品", "使用 " + itemName + " x1 ?",
                () => { _ = BagController.Instance.UseItem(uid); });
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
