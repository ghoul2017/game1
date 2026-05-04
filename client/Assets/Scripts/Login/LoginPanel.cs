using UnityEngine;
using UnityEngine.UI;
using Client.Core;
using Client.Network;
using Client.Game;
using Shared.Common;
using Shared.Common.Protos;

namespace Client.Login
{
    public class LoginPanel : UIPanel
    {
        private InputField usernameInput;
        private InputField passwordInput;
        private Text errorText;
        private Toggle rememberToggle;
        private Button loginBtn;
        private Text loginBtnText;
        private bool isLoggingIn;

        private const string PREF_USERNAME = "login_username";
        private const string PREF_PASSWORD = "login_password";
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

            // 勾选标记
            var checkObj = new GameObject("Checkmark");
            checkObj.transform.SetParent(rememberObj.transform, false);
            var checkRect = checkObj.AddComponent<RectTransform>();
            checkRect.sizeDelta = new Vector2(18, 18);
            checkRect.anchoredPosition = new Vector2(-60, 0);
            var checkImage = checkObj.AddComponent<Image>();
            checkImage.color = UIBuilder.Gold;
            checkImage.raycastTarget = false;
            rememberToggle.graphic = checkImage;

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

            loginBtn = UIBuilder.CreateButton(content, "LoginBtn", "进 入 游 戏", 300, 52, 24,
                UIBuilder.AccentBlue, UIBuilder.Gold);
            loginBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -140);
            loginBtn.onClick.AddListener(OnLoginClick);
            loginBtnText = loginBtn.GetComponentInChildren<Text>();

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
            isLoggingIn = false;
            SetButtonEnabled(true);
            RegisterHandlers();
        }

        public override void OnHide()
        {
            CleanupConnectEvents();
            UnregisterHandlers();
        }

        private void RegisterHandlers()
        {
            var nc = NetClient.Instance;
            if (nc != null && nc.Dispatcher != null)
                nc.Dispatcher.Register<LoginRes>(MsgIdConst.LoginRes, HandleLoginRes);
        }

        private void UnregisterHandlers()
        {
            var nc = NetClient.Instance;
            if (nc != null && nc.Dispatcher != null)
                nc.Dispatcher.Unregister(MsgIdConst.LoginRes);
        }

        private void SetButtonEnabled(bool enabled)
        {
            if (loginBtn != null) loginBtn.interactable = enabled;
            if (loginBtnText != null)
                loginBtnText.text = enabled ? "进 入 游 戏" : "登录中...";
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

            if (isLoggingIn) return;
            isLoggingIn = true;
            SetButtonEnabled(false);

            var nc = NetClient.Instance;
            if (nc == null || nc.Dispatcher == null)
            {
                errorText.text = "网络未初始化";
                isLoggingIn = false;
                SetButtonEnabled(true);
                return;
            }

            RegisterHandlers();

            if (nc.IsConnected)
            {
                DoLogin(username, password);
            }
            else
            {
                errorText.text = "正在连接服务器...";
                _pendingUser = username;
                _pendingPass = password;
                nc.OnConnected += OnServerConnected;
                nc.OnError += OnServerError;
                if (!nc.IsConnecting)
                    nc.Connect();
            }
        }

        private string _pendingUser;
        private string _pendingPass;

        private void OnServerConnected()
        {
            if (_pendingUser != null)
                DoLogin(_pendingUser, _pendingPass);
            CleanupConnectEvents();
        }

        private void OnServerError(string reason)
        {
            errorText.text = "连接服务器失败";
            isLoggingIn = false;
            SetButtonEnabled(true);
            CleanupConnectEvents();
        }

        private void CleanupConnectEvents()
        {
            var nc = NetClient.Instance;
            if (nc != null)
            {
                nc.OnConnected -= OnServerConnected;
                nc.OnError -= OnServerError;
            }
            _pendingUser = null;
            _pendingPass = null;
        }

        private void DoLogin(string username, string password)
        {
            errorText.text = "正在登录...";
            NetClient.Instance.Send(new LoginReq { Username = username, Password = password });
        }

        private void HandleLoginRes(LoginRes res)
        {
            if (res.Code == 0)
            {
                GameData.PlayerId = res.PlayerId;
                GameData.Token = res.Token;
                Debug.Log("[LoginPanel] 登录成功 PlayerId=" + res.PlayerId);
                UIManager.Instance.ReplacePanel<RoleSelectPanel>();
            }
            else
            {
                errorText.text = res.Message;
                isLoggingIn = false;
                SetButtonEnabled(true);
            }
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
