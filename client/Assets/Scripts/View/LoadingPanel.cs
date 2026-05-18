using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Client.View;

namespace Client.View
{
    public class LoadingPanel : UIPanel
    {
        private Slider progressBar;
        private Text progressText;
        private Text tipText;

        private string[] tips = {
            "正在加载资源...",
            "正在初始化系统...",
            "正在连接服务器...",
            "正在准备冒险..."
        };

        public override void OnInit()
        {
            UIBuilder.CreateFullScreenBg(transform, UIBuilder.DarkBg);

            var centerMask = UIBuilder.CreatePanel(transform, "CenterMask", 600, 300, new Color(0.04f, 0.04f, 0.1f, 0.6f));
            var cmRect = centerMask.GetComponent<RectTransform>();
            cmRect.anchoredPosition = new Vector2(0, 20);

            var topDiv = UIBuilder.CreateDivider(centerMask.transform, "TopDiv", 500);
            topDiv.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 120);

            var logo = UIBuilder.CreateText(centerMask.transform, "Logo", "G A M E", 48, UIBuilder.Gold,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            var logoRect = logo.GetComponent<RectTransform>();
            logoRect.anchoredPosition = new Vector2(0, 60);
            logoRect.sizeDelta = new Vector2(600, 80);

            var subtitle = UIBuilder.CreateText(centerMask.transform, "Subtitle", "~奇幻冒险之旅~", 22,
                UIBuilder.TextGray, TextAnchor.MiddleCenter, FontStyle.Italic);
            var subRect = subtitle.GetComponent<RectTransform>();
            subRect.anchoredPosition = new Vector2(0, 0);
            subRect.sizeDelta = new Vector2(400, 30);

            var botDiv = UIBuilder.CreateDivider(centerMask.transform, "BotDiv", 500);
            botDiv.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -80);

            progressBar = UIBuilder.CreateSlider(centerMask.transform, "ProgressBar", 480, 14);
            progressBar.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -110);

            progressText = UIBuilder.CreateText(centerMask.transform, "ProgressText", "0%", 18, UIBuilder.TextGray);
            var ptRect = progressText.GetComponent<RectTransform>();
            ptRect.anchoredPosition = new Vector2(0, -140);
            ptRect.sizeDelta = new Vector2(200, 25);

            tipText = UIBuilder.CreateText(centerMask.transform, "TipText", tips[0], 15, UIBuilder.TextDim);
            var tipRect = tipText.GetComponent<RectTransform>();
            tipRect.anchoredPosition = new Vector2(0, -170);
            tipRect.sizeDelta = new Vector2(400, 25);

            var copyright = UIBuilder.CreateText(transform, "Copyright", "Copyright 2026 Game Studio",
                13, UIBuilder.TextDim, TextAnchor.MiddleCenter);
            var crRect = copyright.GetComponent<RectTransform>();
            crRect.anchorMin = new Vector2(0, 0);
            crRect.anchorMax = new Vector2(1, 0);
            crRect.pivot = new Vector2(0.5f, 0);
            crRect.anchoredPosition = new Vector2(0, 20);
            crRect.sizeDelta = new Vector2(600, 20);

            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        public override void OnShow()
        {
            StartCoroutine(LoadingCoroutine());
        }

        private IEnumerator LoadingCoroutine()
        {
            tipText.text = tips[0];
            progressBar.value = 0f;
            progressText.text = "0%";

            yield return null;

            progressBar.value = 1f;
            progressText.text = "100%";
            tipText.text = "加载完成！";

            yield return new WaitForSeconds(0.3f);
            UIManager.Instance.ReplacePanel<Login.LoginPanel>();
        }
    }
}
