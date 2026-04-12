using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Client.Core;

namespace Client.Game
{
    public class UIToast : UIPanel
    {
        private Text msgText;
        private float displayDuration = 2f;

        public static void Show(string message, float duration = 2f)
        {
            var toast = UIManager.Instance.ShowPanel<UIToast>();
            if (toast != null)
            {
                toast.displayDuration = duration;
                toast.msgText.text = message;
                toast.StartCoroutine(toast.AutoHide());
            }
        }

        public override void OnInit()
        {
            var bg = new GameObject("Bg");
            bg.transform.SetParent(transform, false);
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(400, 60);
            bg.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.12f, 0.9f);
            bg.AddComponent<CanvasGroup>();

            msgText = UIBuilder.CreateText(bg.transform, "Text", "", 20, UIBuilder.TextWhite,
                TextAnchor.MiddleCenter);
            UIBuilder.SetStretch(msgText.GetComponent<RectTransform>());

            var selfRect = GetComponent<RectTransform>();
            selfRect.anchorMin = new Vector2(0.5f, 1);
            selfRect.anchorMax = new Vector2(0.5f, 1);
            selfRect.pivot = new Vector2(0.5f, 1);
            selfRect.anchoredPosition = new Vector2(0, -200);
            selfRect.sizeDelta = new Vector2(400, 60);

            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        private IEnumerator AutoHide()
        {
            yield return new WaitForSeconds(displayDuration);
            UIManager.Instance.HidePanel<UIToast>();
        }

        public override void OnHide()
        {
            StopAllCoroutines();
        }
    }
}
