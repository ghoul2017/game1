using UnityEngine;
using UnityEngine.UI;
using Client.Logic;

namespace Client.View
{
    public class LoginPanel : UIPanel
    {
        private InputField usernameInput;
        private InputField passwordInput;
        private Text errorText;
        private Toggle rememberToggle;
        private Button loginBtn;
        private Text loginBtnText;

        private const string BACKGROUND_RESOURCE = "UI/LoginMenuBackground";
        private const string CARD_FRAME_RESOURCE = "UI/LoginCardFrame";
        private const string INPUT_FRAME_RESOURCE = "UI/LoginInputFrame";
        private const string BUTTON_FRAME_RESOURCE = "UI/LoginButtonFrame";
        private const string SERVER_PANEL_RESOURCE = "UI/LoginServerPanel";
        private const string CREST_RESOURCE = "UI/LoginCrest";
        private const string PREF_USERNAME = "login_username";
        private const string PREF_PASSWORD = "login_password";
        private const string PREF_REMEMBER = "login_remember";

        public override void OnInit()
        {
            CreateMenuBackground();
            CreateBrandArea();
            CreateLoginCard();
            CreateFooter();

            LoadSavedCredentials();

            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        private void CreateMenuBackground()
        {
            var bg = UIBuilder.CreateFullScreenBg(transform, UIBuilder.DarkBg);
            bg.raycastTarget = false;

            var backgroundTexture = Resources.Load<Texture2D>(BACKGROUND_RESOURCE);
            if (backgroundTexture != null)
            {
                bg.sprite = Sprite.Create(backgroundTexture, new Rect(0, 0, backgroundTexture.width, backgroundTexture.height),
                    new Vector2(0.5f, 0.5f));
                bg.color = Color.white;
                bg.type = Image.Type.Simple;
                bg.preserveAspect = true;

                var aspectFitter = bg.gameObject.AddComponent<AspectRatioFitter>();
                aspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                aspectFitter.aspectRatio = (float)backgroundTexture.width / backgroundTexture.height;
            }

            var globalShade = UIBuilder.CreateFullScreenBg(transform, new Color(0.01f, 0.012f, 0.03f, 0.36f));
            globalShade.raycastTarget = false;

            var bottomShade = UIBuilder.CreatePanel(transform, "BottomShade", 1800, 150,
                new Color(0.01f, 0.01f, 0.02f, 0.55f));
            var bottomShadeRect = bottomShade.GetComponent<RectTransform>();
            bottomShadeRect.anchorMin = new Vector2(0.5f, 0);
            bottomShadeRect.anchorMax = new Vector2(0.5f, 0);
            bottomShadeRect.pivot = new Vector2(0.5f, 0);
            bottomShadeRect.anchoredPosition = Vector2.zero;
            bottomShade.GetComponent<Image>().raycastTarget = false;
        }

        private void CreateBrandArea()
        {
            var brand = new GameObject("BrandArea");
            brand.transform.SetParent(transform, false);
            var brandRect = brand.AddComponent<RectTransform>();
            brandRect.anchorMin = new Vector2(0, 0.5f);
            brandRect.anchorMax = new Vector2(0, 0.5f);
            brandRect.pivot = new Vector2(0, 0.5f);
            brandRect.anchoredPosition = new Vector2(92, 26);
            brandRect.sizeDelta = new Vector2(600, 420);

            var eyebrow = UIBuilder.CreateText(brand.transform, "Eyebrow", "星门已开启", 18,
                new Color(0.77f, 0.86f, 1f, 0.92f), TextAnchor.MiddleLeft, FontStyle.Bold);
            var eyebrowRect = eyebrow.GetComponent<RectTransform>();
            eyebrowRect.anchoredPosition = new Vector2(0, 130);
            eyebrowRect.sizeDelta = new Vector2(420, 28);

            var titleShadow = UIBuilder.CreateText(brand.transform, "TitleShadow", "GAME", 74,
                new Color(0f, 0f, 0f, 0.38f), TextAnchor.MiddleLeft, FontStyle.Bold);
            var titleShadowRect = titleShadow.GetComponent<RectTransform>();
            titleShadowRect.anchoredPosition = new Vector2(4, 75);
            titleShadowRect.sizeDelta = new Vector2(520, 96);

            var title = UIBuilder.CreateText(brand.transform, "GameTitle", "GAME", 74,
                new Color(0.96f, 0.76f, 0.18f, 0.98f), TextAnchor.MiddleLeft, FontStyle.Bold);
            var titleRect = title.GetComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(0, 80);
            titleRect.sizeDelta = new Vector2(520, 96);

            var subtitle = UIBuilder.CreateText(brand.transform, "Subtitle", "奇幻冒险", 30,
                UIBuilder.TextWhite, TextAnchor.MiddleLeft, FontStyle.Bold);
            var subtitleRect = subtitle.GetComponent<RectTransform>();
            subtitleRect.anchoredPosition = new Vector2(2, 18);
            subtitleRect.sizeDelta = new Vector2(420, 42);

            var divider = UIBuilder.CreateDivider(brand.transform, "BrandDivider", 320, 2f);
            divider.GetComponent<RectTransform>().anchoredPosition = new Vector2(160, -26);

            var status = UIBuilder.CreatePanel(brand.transform, "ServerPill", 300, 46,
                new Color(0.03f, 0.04f, 0.08f, 0.68f));
            var statusRect = status.GetComponent<RectTransform>();
            statusRect.anchoredPosition = new Vector2(150, -78);
            status.GetComponent<Image>().raycastTarget = false;

            var statusText = UIBuilder.CreateText(status.transform, "Text", "亚服一区  ·  推荐线路", 16,
                new Color(0.86f, 0.9f, 1f, 0.94f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.SetStretch(statusText.GetComponent<RectTransform>());

            var buildTag = UIBuilder.CreateText(brand.transform, "BuildTag", "内测构建  v0.1.0", 14,
                new Color(0.68f, 0.72f, 0.82f, 0.82f), TextAnchor.MiddleLeft);
            var buildTagRect = buildTag.GetComponent<RectTransform>();
            buildTagRect.anchoredPosition = new Vector2(0, -128);
            buildTagRect.sizeDelta = new Vector2(300, 24);
        }

        private void CreateLoginCard()
        {
            var shadow = UIBuilder.CreatePanel(transform, "LoginCardShadow", 466, 596,
                new Color(0f, 0f, 0f, 0.34f));
            var shadowRect = shadow.GetComponent<RectTransform>();
            shadowRect.anchorMin = new Vector2(1, 0.5f);
            shadowRect.anchorMax = new Vector2(1, 0.5f);
            shadowRect.pivot = new Vector2(1, 0.5f);
            shadowRect.anchoredPosition = new Vector2(-74, -18);
            shadow.GetComponent<Image>().raycastTarget = false;

            var panelRoot = CreateSpritePanel(transform, "LoginCard", CARD_FRAME_RESOURCE, 452, 582,
                new Vector4(42, 42, 42, 42), new Color(0.018f, 0.024f, 0.052f, 0.86f));
            var content = panelRoot.transform;
            var panelRect = panelRoot.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1, 0.5f);
            panelRect.anchorMax = new Vector2(1, 0.5f);
            panelRect.pivot = new Vector2(1, 0.5f);
            panelRect.anchoredPosition = new Vector2(-82, -10);

            var crest = CreateSpritePanel(content, "Crest", CREST_RESOURCE, 96, 96,
                Vector4.zero, new Color(0.05f, 0.09f, 0.18f, 0.84f));
            crest.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 224);

            var title = UIBuilder.CreateText(content, "Title", "远征者通行证", 28, UIBuilder.Gold,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            var titleRect = title.GetComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(0, 166);
            titleRect.sizeDelta = new Vector2(380, 38);

            var subtitle = UIBuilder.CreateText(content, "Subtitle", "连接亚服一区  ·  自动注册测试账号", 14,
                new Color(0.72f, 0.76f, 0.86f, 0.9f), TextAnchor.MiddleCenter);
            var subtitleRect = subtitle.GetComponent<RectTransform>();
            subtitleRect.anchoredPosition = new Vector2(0, 137);
            subtitleRect.sizeDelta = new Vector2(390, 24);

            var serverPanel = CreateSpritePanel(content, "ServerInfoPanel", SERVER_PANEL_RESOURCE, 370, 58,
                new Vector4(12, 12, 12, 12), new Color(0.035f, 0.052f, 0.105f, 0.86f));
            serverPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 92);

            var serverLabel = UIBuilder.CreateText(serverPanel.transform, "Label", "推荐服务器", 13,
                UIBuilder.TextDim, TextAnchor.MiddleLeft);
            var serverLabelRect = serverLabel.GetComponent<RectTransform>();
            serverLabelRect.anchoredPosition = new Vector2(-88, 10);
            serverLabelRect.sizeDelta = new Vector2(120, 20);

            var serverValue = UIBuilder.CreateText(serverPanel.transform, "Value", "亚服一区    12ms", 17,
                new Color(0.92f, 0.94f, 1f, 0.96f), TextAnchor.MiddleRight, FontStyle.Bold);
            var serverValueRect = serverValue.GetComponent<RectTransform>();
            serverValueRect.anchoredPosition = new Vector2(84, -4);
            serverValueRect.sizeDelta = new Vector2(190, 28);

            var userLabel = UIBuilder.CreateText(content, "UserLabel", "账号", 15,
                new Color(0.6f, 0.64f, 0.74f, 0.92f), TextAnchor.MiddleLeft, FontStyle.Bold);
            var userLabelRect = userLabel.GetComponent<RectTransform>();
            userLabelRect.anchoredPosition = new Vector2(0, 44);
            userLabelRect.sizeDelta = new Vector2(370, 22);

            usernameInput = UIBuilder.CreateInputField(content, "UsernameInput", "输入账号", 370, 48, 18);
            usernameInput.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 4);
            StyleCredentialInput(usernameInput);
            CreateInputBadgeLabel(usernameInput.transform, "ID");

            var pwdLabel = UIBuilder.CreateText(content, "PwdLabel", "密码", 15,
                new Color(0.6f, 0.64f, 0.74f, 0.92f), TextAnchor.MiddleLeft, FontStyle.Bold);
            var pwdLabelRect = pwdLabel.GetComponent<RectTransform>();
            pwdLabelRect.anchoredPosition = new Vector2(0, -43);
            pwdLabelRect.sizeDelta = new Vector2(370, 22);

            passwordInput = UIBuilder.CreateInputField(content, "PasswordInput", "输入密码", 370, 48, 18);
            passwordInput.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -83);
            passwordInput.contentType = InputField.ContentType.Password;
            StyleCredentialInput(passwordInput);
            CreateInputBadgeLabel(passwordInput.transform, "KEY");

            CreateRememberToggle(content);

            errorText = UIBuilder.CreateText(content, "ErrorText", "", 15, new Color(1f, 0.34f, 0.32f));
            var errorRect = errorText.GetComponent<RectTransform>();
            errorRect.anchoredPosition = new Vector2(0, -158);
            errorRect.sizeDelta = new Vector2(370, 24);

            loginBtn = UIBuilder.CreateButton(content, "LoginBtn", "进入游戏", 330, 58, 24,
                new Color(0.12f, 0.31f, 0.78f, 0.96f), UIBuilder.TextWhite);
            loginBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -209);
            ApplySpriteSkin(loginBtn.GetComponent<Image>(), BUTTON_FRAME_RESOURCE,
                new Vector4(14, 14, 14, 14), new Color(0.12f, 0.31f, 0.78f, 0.96f));
            loginBtn.onClick.AddListener(OnLoginClick);
            loginBtnText = loginBtn.GetComponentInChildren<Text>();

            var hint = UIBuilder.CreateText(content, "Hint", "测试环境：首次登录自动创建账号", 13,
                new Color(0.55f, 0.6f, 0.72f, 0.86f), TextAnchor.MiddleCenter);
            var hintRect = hint.GetComponent<RectTransform>();
            hintRect.anchoredPosition = new Vector2(0, -258);
            hintRect.sizeDelta = new Vector2(370, 22);
        }

        private void CreateRememberToggle(Transform content)
        {
            var rememberObj = new GameObject("RememberToggle");
            rememberObj.transform.SetParent(content, false);
            var rememberRect = rememberObj.AddComponent<RectTransform>();
            rememberRect.sizeDelta = new Vector2(170, 28);
            rememberRect.anchoredPosition = new Vector2(-100, -122);
            var toggleHitArea = rememberObj.AddComponent<Image>();
            toggleHitArea.color = new Color(0f, 0f, 0f, 0f);

            rememberToggle = rememberObj.AddComponent<Toggle>();
            rememberToggle.targetGraphic = toggleHitArea;

            var boxObj = new GameObject("Box");
            boxObj.transform.SetParent(rememberObj.transform, false);
            var boxRect = boxObj.AddComponent<RectTransform>();
            boxRect.sizeDelta = new Vector2(18, 18);
            boxRect.anchoredPosition = new Vector2(-64, 0);
            var boxImage = boxObj.AddComponent<Image>();
            boxImage.color = new Color(0.08f, 0.09f, 0.16f, 0.95f);
            boxImage.raycastTarget = false;

            var checkObj = new GameObject("Checkmark");
            checkObj.transform.SetParent(rememberObj.transform, false);
            var checkRect = checkObj.AddComponent<RectTransform>();
            checkRect.sizeDelta = new Vector2(10, 10);
            checkRect.anchoredPosition = new Vector2(-64, 0);
            var checkImage = checkObj.AddComponent<Image>();
            checkImage.color = UIBuilder.Gold;
            checkImage.raycastTarget = false;
            rememberToggle.graphic = checkImage;

            var toggleLabel = UIBuilder.CreateText(rememberObj.transform, "Label", "记住密码", 14,
                UIBuilder.TextGray, TextAnchor.MiddleLeft);
            var tlRect = toggleLabel.GetComponent<RectTransform>();
            tlRect.anchorMin = Vector2.zero;
            tlRect.anchorMax = Vector2.one;
            tlRect.offsetMin = new Vector2(38, 0);
            tlRect.offsetMax = Vector2.zero;
        }

        private void StyleCredentialInput(InputField input)
        {
            var inputImage = input.GetComponent<Image>();
            if (inputImage != null)
                ApplySpriteSkin(inputImage, INPUT_FRAME_RESOURCE, new Vector4(10, 10, 10, 10),
                    new Color(0.04f, 0.055f, 0.11f, 0.92f));

            var textRect = input.textComponent.GetComponent<RectTransform>();
            textRect.offsetMin = new Vector2(64, 5);
            textRect.offsetMax = new Vector2(-14, -5);

            var placeholderRect = input.placeholder.GetComponent<RectTransform>();
            placeholderRect.offsetMin = new Vector2(64, 5);
            placeholderRect.offsetMax = new Vector2(-14, -5);
        }

        private void CreateInputBadgeLabel(Transform input, string badgeText)
        {
            var badgeLabel = UIBuilder.CreateText(input, "BadgeLabel", badgeText, 12,
                new Color(0.94f, 0.75f, 0.24f, 0.95f), TextAnchor.MiddleCenter, FontStyle.Bold);
            var labelRect = badgeLabel.GetComponent<RectTransform>();
            labelRect.anchoredPosition = new Vector2(-149, 0);
            labelRect.sizeDelta = new Vector2(42, 24);
        }

        private GameObject CreateSpritePanel(Transform parent, string name, string resource, float width, float height,
            Vector4 border, Color fallbackColor)
        {
            var obj = UIBuilder.CreatePanel(parent, name, width, height, fallbackColor);
            ApplySpriteSkin(obj.GetComponent<Image>(), resource, border, fallbackColor);
            obj.GetComponent<Image>().raycastTarget = false;
            return obj;
        }

        private void ApplySpriteSkin(Image image, string resource, Vector4 border, Color fallbackColor)
        {
            if (image == null)
                return;

            var texture = Resources.Load<Texture2D>(resource);
            if (texture == null)
            {
                image.color = fallbackColor;
                return;
            }

            image.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            image.color = Color.white;
            image.type = border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
        }

        private void CreateFooter()
        {
            var version = UIBuilder.CreateText(transform, "Version", "v0.1.0  Build 20260411", 12,
                new Color(0.62f, 0.66f, 0.76f, 0.78f), TextAnchor.MiddleRight);
            var vRect = version.GetComponent<RectTransform>();
            vRect.anchorMin = new Vector2(1, 0);
            vRect.anchorMax = new Vector2(1, 0);
            vRect.pivot = new Vector2(1, 0);
            vRect.anchoredPosition = new Vector2(-22, 18);
            vRect.sizeDelta = new Vector2(220, 22);

            var copyright = UIBuilder.CreateText(transform, "Copyright", "Copyright 2026 Game Studio",
                12, new Color(0.58f, 0.62f, 0.72f, 0.72f), TextAnchor.MiddleLeft);
            var crRect = copyright.GetComponent<RectTransform>();
            crRect.anchorMin = new Vector2(0, 0);
            crRect.anchorMax = new Vector2(0, 0);
            crRect.pivot = new Vector2(0, 0);
            crRect.anchoredPosition = new Vector2(24, 18);
            crRect.sizeDelta = new Vector2(320, 22);
        }

        public override void OnShow()
        {
            errorText.text = "";
            SetButtonEnabled(!LoginController.Instance.IsLoggingIn);
        }

        public override void OnHide()
        {
            CleanupConnectEvents();
        }

        internal bool ConnectEventsActive => _connectEventsActive;

        internal void SetErrorText(string message)
        {
            errorText.text = message;
        }

        internal void SetButtonEnabled(bool enabled)
        {
            if (loginBtn != null) loginBtn.interactable = enabled;
            if (loginBtnText != null)
                loginBtnText.text = enabled ? "进入游戏" : "登录中...";
        }

        private void OnLoginClick()
        {
            string username = usernameInput.text.Trim();
            string password = passwordInput.text.Trim();

            string validationError = LoginController.Instance.ValidateInput(username, password);
            if (validationError != null)
            {
                errorText.text = validationError;
                return;
            }

            errorText.text = "";
            SaveCredentials();

            if (LoginController.Instance.IsLoggingIn)
                return;
            SetButtonEnabled(false);

            bool needConnect = LoginController.Instance.BeginLogin(username, password);

            if (needConnect)
            {
                errorText.text = LoginController.Instance.ErrorMessage;

                var nc = NetClient.Instance;
                if (nc == null)
                {
                    errorText.text = "网络未初始化";
                    LoginController.Instance.FailPendingLoginConnection("网络未初始化");
                    SetButtonEnabled(true);
                    return;
                }

                _connectEventsActive = true;
                if (!nc.IsConnecting)
                    nc.Connect();
            }
        }

        private bool _connectEventsActive;

        internal void CleanupConnectEvents()
        {
            if (!_connectEventsActive)
                return;
            _connectEventsActive = false;
        }

        private void LoadSavedCredentials()
        {
            if (PlayerPrefs.GetInt(PREF_REMEMBER, 0) == 1)
            {
                usernameInput.text = PlayerPrefs.GetString(PREF_USERNAME, "");
                passwordInput.text = PlayerPrefs.GetString(PREF_PASSWORD, "");
                if (rememberToggle != null) rememberToggle.isOn = true;
            }
        }

        private void SaveCredentials()
        {
            if (rememberToggle != null && rememberToggle.isOn)
            {
                PlayerPrefs.SetString(PREF_USERNAME, usernameInput.text.Trim());
                PlayerPrefs.SetString(PREF_PASSWORD, passwordInput.text.Trim());
                PlayerPrefs.SetInt(PREF_REMEMBER, 1);
                PlayerPrefs.Save();
            }
            else
            {
                PlayerPrefs.DeleteKey(PREF_USERNAME);
                PlayerPrefs.DeleteKey(PREF_PASSWORD);
                PlayerPrefs.SetInt(PREF_REMEMBER, 0);
                PlayerPrefs.Save();
            }
        }
    }
}
