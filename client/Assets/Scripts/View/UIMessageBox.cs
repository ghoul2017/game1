using UnityEngine;
using UnityEngine.UI;
using System;
using Client.View;

namespace Client.View
{
    public class UIMessageBox : UIPanel
    {
        private Text titleText;
        private Text contentText;
        private Button confirmBtn;
        private Button cancelBtn;

        private Action onConfirm;
        private Action onCancel;

        public static void Show(string title, string content, Action onConfirm = null, Action onCancel = null,
            string confirmText = "确认", string cancelText = "取消", bool showCancel = true)
        {
            var msgBox = UIManager.Instance.ShowPanel<UIMessageBox>();
            if (msgBox != null)
            {
                msgBox.titleText.text = title;
                msgBox.contentText.text = content;
                msgBox.onConfirm = onConfirm;
                msgBox.onCancel = onCancel;
                msgBox.confirmBtn.GetComponentInChildren<Text>().text = confirmText;
                msgBox.cancelBtn.GetComponentInChildren<Text>().text = cancelText;
                msgBox.cancelBtn.gameObject.SetActive(showCancel);
            }
        }

        public override void OnInit()
        {
            var mask = new GameObject("Mask");
            mask.transform.SetParent(transform, false);
            UIBuilder.SetStretch(mask.AddComponent<RectTransform>());
            mask.AddComponent<Image>().color = new Color(0, 0, 0, 0.55f);

            var (panelRoot, content) = UIBuilder.CreateBorderedPanel(transform, "MessageBox", 450, 280,
                UIBuilder.PanelBg, UIBuilder.GoldDark, 2f);

            var titleBar = UIBuilder.CreateTitleBar(content, "TitleBar", "提 示", 450, 45);
            var tbRect = titleBar.GetComponent<RectTransform>();
            tbRect.anchorMin = new Vector2(0, 1);
            tbRect.anchorMax = new Vector2(1, 1);
            tbRect.pivot = new Vector2(0.5f, 1);
            tbRect.offsetMin = new Vector2(0, -45);
            tbRect.offsetMax = Vector2.zero;
            titleText = titleBar.transform.Find("TitleText").GetComponent<Text>();

            var div = UIBuilder.CreateDivider(content, "Div", 400);
            div.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 80);

            contentText = UIBuilder.CreateText(content, "Content", "", 19, UIBuilder.TextWhite,
                TextAnchor.MiddleCenter);
            var cRect = contentText.GetComponent<RectTransform>();
            cRect.anchoredPosition = new Vector2(0, 20);
            cRect.sizeDelta = new Vector2(400, 100);

            confirmBtn = UIBuilder.CreateButton(content, "ConfirmBtn", "确认", 150, 42, 18,
                UIBuilder.AccentBlue, UIBuilder.TextWhite);
            confirmBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-90, -90);
            confirmBtn.onClick.AddListener(OnConfirmClick);

            cancelBtn = UIBuilder.CreateButton(content, "CancelBtn", "取消", 150, 42, 18,
                UIBuilder.ButtonNormal, UIBuilder.TextGray);
            cancelBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(90, -90);
            cancelBtn.onClick.AddListener(OnCancelClick);

            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        private void OnConfirmClick()
        {
            UIManager.Instance.HidePanel<UIMessageBox>();
            onConfirm?.Invoke();
        }

        private void OnCancelClick()
        {
            UIManager.Instance.HidePanel<UIMessageBox>();
            onCancel?.Invoke();
        }
    }
}
