using Client.View;
using Shared.Core.Network;
using UnityEngine;
using UnityEngine.UI;

namespace Client.View
{
    /// <summary>
    /// 轻量常驻网络状态面板（右上角）
    /// 显示：连接状态指示灯 + 状态文字 + 延迟
    /// </summary>
    public class NetworkStatusPanel : MonoBehaviour
    {
        private static readonly Color ColorConnected = new Color(0.3f, 0.69f, 0.31f);
        private static readonly Color ColorConnecting = new Color(1f, 0.76f, 0.03f);
        private static readonly Color ColorDisconnected = new Color(0.96f, 0.26f, 0.21f);
        private static readonly Color ColorReconnecting = new Color(1f, 0.6f, 0f);

        private Image _indicatorImage;
        private Text _statusText;
        private Text _pingText;

        public static NetworkStatusPanel Instance { get; private set; }

        public static void EnsureCreated()
        {
            if (Instance != null) return;
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;
            var go = new GameObject("_NetworkStatus");
            go.transform.SetParent(canvas.transform, false);
            go.AddComponent<NetworkStatusPanel>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildUI();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            RefreshDisplay();
        }

        private void BuildUI()
        {
            // Root panel anchored to top-right
            var panel = UIBuilder.CreatePanel(transform, "StatusPanel", 220f, 36f,
                new Color(0.08f, 0.08f, 0.12f, 0.75f));
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.anchoredPosition = new Vector2(-10f, -10f);

            // Status indicator dot
            var dot = new GameObject("Dot");
            dot.transform.SetParent(panel.transform, false);
            var dotRect = dot.AddComponent<RectTransform>();
            dotRect.anchorMin = new Vector2(0f, 0.5f);
            dotRect.anchorMax = new Vector2(0f, 0.5f);
            dotRect.pivot = new Vector2(0.5f, 0.5f);
            dotRect.anchoredPosition = new Vector2(16f, 0f);
            dotRect.sizeDelta = new Vector2(12f, 12f);
            _indicatorImage = dot.AddComponent<Image>();
            _indicatorImage.color = ColorDisconnected;

            // Status text
            _statusText = UIBuilder.CreateText(panel.transform, "StatusText", "Disconnected",
                13, UIBuilder.TextGray, TextAnchor.MiddleLeft);
            var statusRect = _statusText.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 1f);
            statusRect.offsetMin = new Vector2(28f, 0f);
            statusRect.offsetMax = new Vector2(-70f, 0f);

            // Ping text (right-aligned)
            _pingText = UIBuilder.CreateText(panel.transform, "PingText", "",
                12, UIBuilder.TextDim, TextAnchor.MiddleRight);
            var pingRect = _pingText.GetComponent<RectTransform>();
            pingRect.anchorMin = new Vector2(1f, 0f);
            pingRect.anchorMax = new Vector2(1f, 1f);
            pingRect.pivot = new Vector2(1f, 0.5f);
            pingRect.sizeDelta = new Vector2(65f, 0f);
            pingRect.anchoredPosition = new Vector2(-8f, 0f);
        }

        private void RefreshDisplay()
        {
            var nc = NetClient.Instance;
            if (nc == null)
            {
                _indicatorImage.color = ColorDisconnected;
                _statusText.text = "No Network";
                _pingText.text = "";
                return;
            }

            Color color;
            string status;
            string ping;

            switch (nc.State)
            {
                case NetClient.ConnState.Connected:
                    color = ColorConnected;
                    status = "Connected";
                    ping = nc.PingMs > 0 ? nc.PingMs + "ms" : "";
                    break;
                case NetClient.ConnState.Connecting:
                    color = ColorConnecting;
                    status = "Connecting...";
                    ping = "";
                    break;
                case NetClient.ConnState.Handshaking:
                    color = ColorConnecting;
                    status = "Handshaking...";
                    ping = "";
                    break;
                case NetClient.ConnState.Reconnecting:
                    color = ColorReconnecting;
                    float cd = nc.ReconnectCountdown;
                    status = "Retry " + nc.CurrentReconnectAttempt + "/" + nc.MaxReconnectAttempt + " " + cd.ToString("F0") + "s";
                    ping = "";
                    break;
                default:
                    color = ColorDisconnected;
                    status = "Disconnected";
                    ping = "";
                    break;
            }

            _indicatorImage.color = color;
            _statusText.text = status;
            _pingText.text = ping;
        }
    }
}
