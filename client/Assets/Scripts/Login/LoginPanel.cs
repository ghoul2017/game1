using UnityEngine;
using UnityEngine.UI;
using Client.Core;

namespace Client.Login
{
    public class LoginPanel : UIPanel
    {
        private InputField usernameInput;
        private InputField passwordInput;
        private Text errorText;
        private Toggle rememberToggle;

        private const string PREF_USERNAME = "login_username";
        private const string PREF_REMEMBER = "login_remember";

        public override void OnInit()
        {
            UIBuilder.CreateFullScreenBg(transform, UIBuilder.DarkBg);

            var bgTitle = UIBuilder.CreateText(transform, "BgTitle", "GAME", 72, new Color(1f, 0.85f, 0.3f, 0.06f),
                TextAnchor.MiddleCenter, FontStyle.Bold);
            var bgTitleRect = bgTitle.GetComponent<RectTransform>();
            bgTitleRect.anchoredPosition = new Vector2(0, 300);
            bgTitleRect.sizeDelta = new Vector2(1000, 100);

            var (panelRoot, content) = UIBuilder.CreateBorderedPanel(transform, "LoginCard", 420, 480,
                UIBuilder.PanelBg, UIBuilder.GoldDark, 2f);
            panelRoot.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -10);

            var titleBar = UIBuilder.CreateTitleBar(content, "TitleBar", "帐 号 登 录", 420, 50);
            var tbRect = titleBar.GetComponent<RectTransform>();
            tbRect.anchorMin = new Vector2(0, 1);
            tbRect.anchorMax = new Vector2(1, 1);
            tbRect.pivot = new Vector2(0.5f, 1);
            tbRect.anchoredPosition = Vector2.zero;
            tbRect.offsetMin = new Vector2(0, -50);
            tbRect.offsetMax = Vector2.zero;

            var gameTitle = UIBuilder.CreateText(content, "GameTitle", "~ 奇幻冒险 ~", 20,
                UIBuilder.Gold, TextAnchor.MiddleCenter, FontStyle.Italic);
            var gtRect = gameTitle.GetComponent<RectTransform>();
            gtRect.anchoredPosition = new Vector2(0, 170);
            gtRect.sizeDelta = new Vector2(300, 30);

            var div1 = UIBuilder.CreateDivider(content, "Div1", 350);
            div1.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 145);

            var userLabel = UIBuilder.CreateText(content, "UserLabel", "帐 号", 16, UIBuilder.TextDim,
                TextAnchor.MiddleLeft);
            var userLabelRect = userLabel.GetComponent<RectTransform>();
            userLabelRect.anchoredPosition = new Vector2(0, 110);
            userLabelRect.sizeDelta = new Vector2(360, 22);

            usernameInput = UIBuilder.CreateInputField(content, "UsernameInput", "请输入账号", 360, 42, 18);
            usernameInput.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 72);

            var pwdLabel = UIBuilder.CreateText(content, "PwdLabel", "密 码", 16, UIBuilder.TextDim,
                TextAnchor.MiddleLeft);
            var pwdLabelRect = pwdLabel.GetComponent<RectTransform>();
            pwdLabelRect.anchoredPosition = new Vector2(0, 35);
            pwdLabelRect.sizeDelta = new Vector2(360, 22);

            passwordInput = UIBuilder.CreateInputField(content, "PasswordInput", "请输入密码", 360, 42, 18);
            passwordInput.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -3);
            passwordInput.contentType = InputField.ContentType.Password;

            var rememberObj = new GameObject("RememberToggle");
            rememberObj.transform.SetParent(content, false);
            var rememberRect = rememberObj.AddComponent<RectTransform>();
            rememberRect.sizeDelta = new Vector2(160, 28);
            rememberRect.anchoredPosition = new Vector2(-90, -40);
            var toggleBg = rememberObj.AddComponent<Image>();
            toggleBg.color = UIBuilder.InputBg;
            rememberToggle = rememberObj.AddComponent<Toggle>();
            rememberToggle.targetGraphic = toggleBg;
            var toggleLabel = UIBuilder.CreateText(rememberObj.transform, "Label", "记住密码", 14,
                UIBuilder.TextGray, TextAnchor.MiddleLeft);
            var tlRect = toggleLabel.GetComponent<RectTransform>();
            tlRect.anchorMin = Vector2.zero;
            tlRect.anchorMax = Vector2.one;
            tlRect.offsetMin = new Vector2(22, 0);
            tlRect.offsetMax = Vector2.zero;

            errorText = UIBuilder.CreateText(content, "ErrorText", "", 15, Color.red);
            var errorRect = errorText.GetComponent<RectTransform>();
            errorRect.anchoredPosition = new Vector2(0, -72);
            errorRect.sizeDelta = new Vector2(340, 22);

            var div2 = UIBuilder.CreateDivider(content, "Div2", 350);
            div2.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -95);

            var loginBtn = UIBuilder.CreateButton(content, "LoginBtn", "进 入 游 戏", 300, 52, 24,
                UIBuilder.AccentBlue, UIBuilder.Gold);
            loginBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -140);
            loginBtn.onClick.AddListener(OnLoginClick);

            var serverInfo = UIBuilder.CreateText(content, "ServerInfo", "[ 推荐服务器 ]  亚服一区  延迟: 12ms", 13,
                UIBuilder.TextDim, TextAnchor.MiddleCenter);
            var siRect = serverInfo.GetComponent<RectTransform>();
            siRect.anchoredPosition = new Vector2(0, -185);
            siRect.sizeDelta = new Vector2(380, 20);

            var version = UIBuilder.CreateText(transform, "Version", "v0.1.0  Build 20260411", 12, UIBuilder.TextDim);
            var vRect = version.GetComponent<RectTransform>();
            vRect.anchorMin = new Vector2(1, 0);
            vRect.anchorMax = new Vector2(1, 0);
            vRect.pivot = new Vector2(1, 0);
            vRect.anchoredPosition = new Vector2(-20, 15);
            vRect.sizeDelta = new Vector2(200, 20);

            var copyright = UIBuilder.CreateText(transform, "Copyright", "Copyright 2026 Game Studio",
                12, UIBuilder.TextDim, TextAnchor.MiddleCenter);
            var crRect = copyright.GetComponent<RectTransform>();
            crRect.anchorMin = new Vector2(0, 0);
            crRect.anchorMax = new Vector2(1, 0);
            crRect.pivot = new Vector2(0.5f, 0);
            crRect.anchoredPosition = new Vector2(0, 40);
            crRect.sizeDelta = new Vector2(400, 20);

            LoadSavedCredentials();

            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        public override void OnShow()
        {
            errorText.text = "";
        }

        private void OnLoginClick()
        {
            string username = usernameInput.text.Trim();
            string password = passwordInput.text.Trim();

            if (string.IsNullOrEmpty(username))
            {
                errorText.text = "请输入账号";
                return;
            }
            if (string.IsNullOrEmpty(password))
            {
                errorText.text = "请输入密码";
                return;
            }

            errorText.text = "";
            SaveCredentials();
            Debug.Log($"[LoginPanel] 模拟登录: {username}");
            UIManager.Instance.ReplacePanel<RoleSelectPanel>();
        }

        private void LoadSavedCredentials()
        {
            if (PlayerPrefs.GetInt(PREF_REMEMBER, 0) == 1)
            {
                usernameInput.text = PlayerPrefs.GetString(PREF_USERNAME, "");
                if (rememberToggle != null) rememberToggle.isOn = true;
            }
        }

        private void SaveCredentials()
        {
            if (rememberToggle != null && rememberToggle.isOn)
            {
                PlayerPrefs.SetString(PREF_USERNAME, usernameInput.text.Trim());
                PlayerPrefs.SetInt(PREF_REMEMBER, 1);
                PlayerPrefs.Save();
            }
            else
            {
                PlayerPrefs.DeleteKey(PREF_USERNAME);
                PlayerPrefs.SetInt(PREF_REMEMBER, 0);
                PlayerPrefs.Save();
            }
        }
    }
}
