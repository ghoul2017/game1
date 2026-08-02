using System;
using GameplayCore;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Expedition
{
    /// <summary>
    /// 显示单次结算、局外资源、唯一设施升级与下一局入口。
    /// </summary>
    public sealed class ExpeditionBaseView : MonoBehaviour
    {
        private static Font _font;

        private Text _balanceText;
        private Button _departButton;
        private Image _facilityBlock;
        private Text _facilityText;
        private Text _settlementText;
        private Text _upgradeResultText;
        private Button _upgradeButton;
        private Text _upgradeButtonText;

        public string BalanceText => _balanceText.text;

        public Button DepartButton => _departButton;

        public string FacilityText => _facilityText.text;

        public FacilityUpgradeReceipt LatestUpgradeReceipt { get; private set; }

        public string SettlementText => _settlementText.text;

        public Button UpgradeButton => _upgradeButton;

        public string UpgradeResultText => _upgradeResultText.text;

        public static ExpeditionBaseView Create(Transform parent, Action upgrade, Action depart)
        {
            GameObject canvasObject = new GameObject(
                "ExpeditionBase",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080.0f, 1920.0f);
            scaler.matchWidthOrHeight = 0.5f;

            ExpeditionBaseView view = canvasObject.AddComponent<ExpeditionBaseView>();
            view.Build(upgrade, depart);
            return view;
        }

        public void Synchronize(ExpeditionCampaignSession session, FacilityUpgradeReceipt upgradeReceipt)
        {
            ExpeditionCampaignState state = session.State;
            SettlementReceipt settlement = state.LatestSettlement;
            string phase = settlement.Phase == ExpeditionPhase.Completed ? "远征成功" : "远征失败";
            _settlementText.text =
                $"上局结算：{phase}\n局内获得 {settlement.CollectedResource}   " +
                $"带回基地 {settlement.BankedResource}";
            _balanceText.text = $"基地资源：{state.BankedResource}";
            _facilityText.text =
                $"训练设施 等级 {state.FacilityLevel}\n代表队员伤害：{session.CurrentRepresentativeDamage}";
            _facilityBlock.color = state.FacilityLevel == 0
                ? new Color(0.34f, 0.37f, 0.42f, 1.0f)
                : new Color(0.12f, 0.62f, 0.85f, 1.0f);
            _facilityBlock.rectTransform.sizeDelta = state.FacilityLevel == 0
                ? new Vector2(280.0f, 210.0f)
                : new Vector2(330.0f, 260.0f);

            LatestUpgradeReceipt = upgradeReceipt;
            _upgradeResultText.text = upgradeReceipt == null
                ? "升级后会永久提高下一局的队员伤害"
                : $"升级完成：伤害 {upgradeReceipt.PreviousDamage} → {upgradeReceipt.CurrentDamage}";
            _upgradeButton.interactable = !state.HasActiveRun && state.FacilityLevel == 0 &&
                state.BankedResource >= session.UpgradeCost;
            _upgradeButtonText.text = state.FacilityLevel == 0
                ? $"升级设施（消耗 {session.UpgradeCost}）"
                : "设施已升级";
            _departButton.interactable = !state.HasActiveRun;
        }

        private void Build(Action upgrade, Action depart)
        {
            Image background = gameObject.AddComponent<Image>();
            background.color = new Color(0.035f, 0.05f, 0.075f, 1.0f);

            Text title = CreateText("Title", transform, 66, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.08f, 0.83f), new Vector2(0.92f, 0.96f));
            title.text = "远征基地";

            _settlementText = CreateText("Settlement", transform, 40, TextAnchor.MiddleCenter);
            SetRect(_settlementText.rectTransform, new Vector2(0.08f, 0.67f), new Vector2(0.92f, 0.83f));

            _balanceText = CreateText("Balance", transform, 50, TextAnchor.MiddleCenter);
            SetRect(_balanceText.rectTransform, new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.68f));

            GameObject facilityObject = new GameObject("FacilityBlock", typeof(RectTransform), typeof(Image));
            facilityObject.transform.SetParent(transform, false);
            RectTransform facilityRect = facilityObject.GetComponent<RectTransform>();
            facilityRect.anchorMin = new Vector2(0.5f, 0.48f);
            facilityRect.anchorMax = new Vector2(0.5f, 0.48f);
            facilityRect.pivot = new Vector2(0.5f, 0.5f);
            _facilityBlock = facilityObject.GetComponent<Image>();
            _facilityBlock.raycastTarget = false;

            _facilityText = CreateText("FacilityStatus", transform, 38, TextAnchor.MiddleCenter);
            SetRect(_facilityText.rectTransform, new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.55f));

            _upgradeResultText = CreateText("UpgradeResult", transform, 32, TextAnchor.MiddleCenter);
            SetRect(_upgradeResultText.rectTransform, new Vector2(0.08f, 0.27f), new Vector2(0.92f, 0.36f));

            _upgradeButton = CreateButton(
                "UpgradeButton",
                transform,
                new Vector2(0.14f, 0.13f),
                new Vector2(0.86f, 0.23f),
                new Color(0.12f, 0.55f, 0.82f, 1.0f),
                upgrade,
                out _upgradeButtonText);
            _departButton = CreateButton(
                "DepartButton",
                transform,
                new Vector2(0.14f, 0.02f),
                new Vector2(0.86f, 0.11f),
                new Color(0.12f, 0.65f, 0.35f, 1.0f),
                depart,
                out Text departText);
            departText.text = "开始下一局";
        }

        private static Button CreateButton(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color,
            Action action,
            out Text label)
        {
            GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            SetRect(buttonObject.GetComponent<RectTransform>(), anchorMin, anchorMax);
            buttonObject.GetComponent<Image>().color = color;
            Button button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(() => action());
            label = CreateText("Label", buttonObject.transform, 38, TextAnchor.MiddleCenter);
            SetRect(label.rectTransform, Vector2.zero, Vector2.one);
            return button;
        }

        private static Text CreateText(string objectName, Transform parent, int fontSize, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = GetFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void SetRect(RectTransform target, Vector2 anchorMin, Vector2 anchorMax)
        {
            target.anchorMin = anchorMin;
            target.anchorMax = anchorMax;
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
        }

        private static Font GetFont()
        {
            if (_font == null)
            {
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return _font;
        }
    }
}
