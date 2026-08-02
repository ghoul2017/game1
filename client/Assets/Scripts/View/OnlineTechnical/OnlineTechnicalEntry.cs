using Client.Logic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Client.View.OnlineTechnical
{
    /// <summary>
    /// 在线技术切片的独立 Unity 入口。登录前提供诊断抽屉，角色就绪后留在空场景挂载完整联机玩法。
    /// </summary>
    public sealed class OnlineTechnicalEntry : MonoBehaviour
    {
        private static OnlineTechnicalEntry _instance;

        private GameObject _contentObject;
        private GameObject _gameplayObject;
        private Button _toggleButton;

        public OnlineArenaPanel ArenaPanel { get; private set; }

        public bool ContentVisible => _contentObject != null && _contentObject.activeSelf;

        public OnlineExpeditionPanel ExpeditionPanel { get; private set; }

        public OnlineExpeditionGameplayView GameplayView { get; private set; }

        public static bool TryEnterGameplay(long roleId)
        {
            if (_instance == null || roleId <= 0L)
            {
                return false;
            }

            _instance.EnterGameplay(roleId);
            return true;
        }

        public static bool TryReturnToLogin(string message)
        {
            if (_instance == null || _instance.GameplayView == null ||
                !_instance.GameplayView.gameObject.activeSelf || _instance.GameplayView.RoleId <= 0L)
            {
                return false;
            }

            _instance.ReturnToLogin(message);
            return true;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            ClientRuntimeMode.SetOnlineExpeditionEntry(true);
            DontDestroyOnLoad(gameObject);
            EnsureEventSystem();
            BuildView();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                ClientRuntimeMode.SetOnlineExpeditionEntry(false);
                _instance = null;
            }
        }

        public void ToggleContent()
        {
            bool visible = !ContentVisible;
            _contentObject.SetActive(visible);
            Text label = _toggleButton.GetComponentInChildren<Text>();
            bool gameplayActive = GameplayView != null;
            label.text = visible
                ? gameplayActive ? "收起在线诊断面板" : "收起在线技术面板"
                : gameplayActive ? "展开在线诊断面板" : "展开在线技术面板";
        }

        private void EnterGameplay(long roleId)
        {
            Screen.orientation = ScreenOrientation.Portrait;
            if (GameplayView == null)
            {
                _gameplayObject = new GameObject("OnlineExpeditionGameplay");
                _gameplayObject.transform.SetParent(transform, false);
                GameplayView = _gameplayObject.AddComponent<OnlineExpeditionGameplayView>();
                GameplayView.Initialize();
            }

            _contentObject.SetActive(false);
            Text label = _toggleButton.GetComponentInChildren<Text>();
            label.text = "展开在线诊断面板";
            GameplayView.Enter(roleId);
        }

        private void ReturnToLogin(string message)
        {
            GameplayView.ExitToLogin();
            _contentObject.SetActive(false);
            _toggleButton.GetComponentInChildren<Text>().text = "展开在线诊断面板";
            GameData.Token = string.Empty;
            GameData.CurrentRole = null;
            GameData.CurrentMap = null;
            BagController.Instance.ResetForRole(0L);

            UIManager manager = UIManager.EnsureCreated();
            manager.ClearAll();
            LoginPanel loginPanel = manager.ShowPanel<LoginPanel>();
            loginPanel?.SetErrorText(message);
        }

        private void BuildView()
        {
            GameObject canvasObject = new GameObject(
                "OnlineTechnicalCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920.0f, 1080.0f);
            scaler.matchWidthOrHeight = 0.5f;

            _toggleButton = OnlineTechnicalUiFactory.CreateButton(
                "OnlineTechnicalToggleButton",
                canvasObject.transform,
                "收起在线技术面板",
                new Color(0.08f, 0.12f, 0.18f, 0.96f));
            OnlineTechnicalUiFactory.SetAnchoredRect(
                _toggleButton.GetComponent<RectTransform>(),
                new Vector2(1.0f, 1.0f),
                new Vector2(1.0f, 1.0f),
                new Vector2(-24.0f, -20.0f),
                new Vector2(310.0f, 58.0f));
            _toggleButton.onClick.AddListener(ToggleContent);

            _contentObject = OnlineTechnicalUiFactory.CreatePanel(
                "OnlineTechnicalContent",
                canvasObject.transform,
                new Color(0.025f, 0.035f, 0.055f, 0.96f));
            RectTransform contentRect = _contentObject.GetComponent<RectTransform>();
            OnlineTechnicalUiFactory.SetAnchoredRect(
                contentRect,
                new Vector2(1.0f, 1.0f),
                new Vector2(1.0f, 1.0f),
                new Vector2(-24.0f, -88.0f),
                new Vector2(720.0f, 930.0f));

            GameObject expeditionObject = OnlineTechnicalUiFactory.CreatePanel(
                "OnlineExpeditionPanel",
                _contentObject.transform,
                new Color(0.04f, 0.08f, 0.12f, 0.98f));
            OnlineTechnicalUiFactory.SetRect(
                expeditionObject.GetComponent<RectTransform>(),
                new Vector2(0.02f, 0.47f),
                new Vector2(0.98f, 0.98f),
                Vector2.zero,
                Vector2.zero);
            ExpeditionPanel = expeditionObject.AddComponent<OnlineExpeditionPanel>();
            ExpeditionPanel.Initialize();

            GameObject arenaObject = OnlineTechnicalUiFactory.CreatePanel(
                "OnlineArenaPanel",
                _contentObject.transform,
                new Color(0.10f, 0.055f, 0.035f, 0.98f));
            OnlineTechnicalUiFactory.SetRect(
                arenaObject.GetComponent<RectTransform>(),
                new Vector2(0.02f, 0.02f),
                new Vector2(0.98f, 0.45f),
                Vector2.zero,
                Vector2.zero);
            ArenaPanel = arenaObject.AddComponent<OnlineArenaPanel>();
            ArenaPanel.Initialize();
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject(
                "OnlineTechnicalEventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
            eventSystemObject.transform.SetParent(transform, false);
        }
    }
}
