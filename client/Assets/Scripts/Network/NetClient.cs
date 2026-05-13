using System;
using System.Collections.Concurrent;
using Client.Game;
using Shared.Common;
using Client;
using Shared.Common.Enums;
using Shared.Core.Event;
using Shared.Core.Network;
using Shared.Common.Protos;
using UnityEngine;

namespace Client.Network
{
    /// <summary>
    /// 客户端网络管理器。基于 NetworkComponent + TcpSession，
    /// 负责连接、心跳、断线重连。消息处理由 NetworkComponent 的 MessageRouter 统一分发。
    /// </summary>
    public class NetClient : MonoBehaviour
    {
        public static NetClient Instance { get; private set; }

        [Header("Server")] public string host = "127.0.0.1";
        public int port = 9000;

        [Header("Heartbeat")] public float heartbeatInterval = 30f;
        public float recvTimeout = 60f;

        [Header("Reconnect")] public bool autoReconnect = true;
        public int maxReconnectAttempts = 5;
        public float baseReconnectDelay = 1f;
        public float maxReconnectDelay = 30f;

        // --- State ---
        public enum ConnState
        {
            Disconnected,
            Connecting,
            Handshaking,
            Connected,
            Reconnecting
        }

        public bool IsConnected
        {
            get { return _state == ConnState.Connected && _session != null && _session.IsConnected; }
        }

        public bool IsConnecting
        {
            get
            {
                return _state == ConnState.Connecting || _state == ConnState.Reconnecting ||
                       _state == ConnState.Handshaking;
            }
        }

        public ConnState State => _state;
        public int PingMs { get; private set; }
        public int CurrentReconnectAttempt => _reconnectAttempts;
        public int MaxReconnectAttempt => maxReconnectAttempts;

        public float ReconnectCountdown =>
            _state == ConnState.Reconnecting ? Mathf.Max(0f, _nextReconnectTime - Time.time) : 0f;

        private ConnState _state = ConnState.Disconnected;

        // --- Internals ---
        private TcpSession _session;
        private readonly ConcurrentQueue<Action> _mainQueue = new ConcurrentQueue<Action>();

        // Timing
        private float _lastSendTime;
        private float _lastRecvTime;
        private volatile bool _running;

        // Reconnect
        private int _reconnectAttempts;
        private float _nextReconnectTime;
        private bool _userDisconnect;
        private bool _hadSession;

        // --- Events ---
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnError;
        public event Action OnReconnected;
        public event Action<string> OnReconnectFailed;
        public event Action<int> OnKicked;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Context.Client.AddComponent<NetworkComponent>();
            EventBus.Instance.AutoRegister();
        }

        void Update()
        {
            // Drain main-thread queue
            while (_mainQueue.TryDequeue(out Action action))
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            // Reconnect timer
            if (_state == ConnState.Reconnecting && Time.time >= _nextReconnectTime)
            {
                Debug.Log("[NetClient] Reconnecting (" + _reconnectAttempts + "/" + maxReconnectAttempts + ")...");
                _ = StartConnectAsync();
            }

            // Handshake timeout
            if (_state == ConnState.Handshaking)
            {
                if (Time.time - _lastRecvTime > recvTimeout)
                {
                    Debug.LogWarning("[NetClient] Handshake timeout");
                    DisposeSession(LogoutReason.NetDisconnected, "Handshake timeout");
                }
            }

            // Heartbeat
            if (_state == ConnState.Connected)
            {
                float now = Time.time;
                if (now - _lastSendTime >= heartbeatInterval)
                {
                    SendHeartbeat();
                }

                if (now - _lastRecvTime > recvTimeout)
                {
                    Debug.LogWarning("[NetClient] Receive timeout, disconnecting");
                    DisposeSession(LogoutReason.NetDisconnected, "Receive timeout");
                }
            }
        }

        void OnDestroy()
        {
            _running = false;
            DisposeSession(LogoutReason.NetDisconnected, "Destroyed");
            if (Instance == this) Instance = null;
        }

        // ==================== Public API ====================

        public void Connect(string host = null, int? port = null)
        {
            if (_state == ConnState.Connecting || _state == ConnState.Connected ||
                _state == ConnState.Handshaking || _state == ConnState.Reconnecting)
                return;

            if (host != null) this.host = host;
            if (port != null) this.port = port.Value;

            _userDisconnect = false;
            _hadSession = false;
            _reconnectAttempts = 0;
            _state = ConnState.Connecting;
            _ = StartConnectAsync();
        }

        public void Disconnect()
        {
            _userDisconnect = true;
            DisposeSession(LogoutReason.NetDisconnected, "User disconnect");
        }

        public void Send(IMessage msg)
        {
            if (_state != ConnState.Connected && _state != ConnState.Handshaking)
            {
                Debug.LogWarning("[NetClient] Cannot send: not connected");
                return;
            }

            _session?.Send(msg);
            _lastSendTime = Time.time;
        }

        /// <summary>
        /// 创建 NetClient 单例（如果不存在）
        /// </summary>
        public static void EnsureCreated()
        {
            if (Instance != null) return;
            var go = new GameObject("_NetClient");
            go.AddComponent<NetClient>();
        }

        // ==================== Connect / Disconnect ====================

        private async System.Threading.Tasks.Task StartConnectAsync()
        {
            _state = ConnState.Connecting;
            _running = true;
            try
            {
                var session = await Context.Network.ConnectAsync(host, port);
                _mainQueue.Enqueue(() => OnConnectSuccess(session));
            }
            catch (Exception ex)
            {
                _mainQueue.Enqueue(() => HandleConnectFailed(ex.Message));
            }
        }

        private void OnConnectSuccess(TcpSession session)
        {
            if (!_running) return;

            _session = session;
            _session.PostToMainThread = action => _mainQueue.Enqueue(() =>
            {
                _lastRecvTime = Time.time;
                action();
            });
            _session.OnDisconnected += OnSessionDisconnected;

            _state = ConnState.Handshaking;
            _reconnectAttempts = 0;
            _lastRecvTime = Time.time;
            _lastSendTime = Time.time;
            Debug.Log("[NetClient] TCP connected, sending handshake...");

            Send(new HandshakeReq());
        }

        private void HandleConnectFailed(string reason)
        {
            Debug.LogWarning("[NetClient] Connect failed: " + reason);

            if (autoReconnect && !_userDisconnect && _reconnectAttempts < maxReconnectAttempts)
            {
                _reconnectAttempts++;
                float delay = Math.Min(
                    baseReconnectDelay * (1 << (_reconnectAttempts - 1)),
                    maxReconnectDelay);
                _nextReconnectTime = Time.time + delay;
                _state = ConnState.Reconnecting;
                Debug.Log("[NetClient] Retry in " + delay + "s (" + _reconnectAttempts + "/" + maxReconnectAttempts +
                          ")");
            }
            else
            {
                _state = ConnState.Disconnected;
                OnError?.Invoke(reason);
            }
        }

        private void OnSessionDisconnected(TcpSession session, LogoutReason reason)
        {
            _mainQueue.Enqueue(() =>
            {
                // 非主动断开且有会话信息时标记，下次握手成功后自动重连
                if (!_userDisconnect && !string.IsNullOrEmpty(GameData.Token) && GameData.CurrentRole != null)
                    _hadSession = true;

                _session = null;
                HandlePostDisconnect();
            });
        }

        private void DisposeSession(LogoutReason reason, string debugMsg)
        {
            if (_session != null)
            {
                _session.OnDisconnected -= OnSessionDisconnected;
                _session.PostToMainThread = null;
                _session.Disconnect(reason);
                _session = null;
            }

            if (reason != LogoutReason.NetDisconnected || debugMsg != "Destroyed")
            {
                Debug.Log("[NetClient] DisposeSession: " + debugMsg);
            }

            HandlePostDisconnect();
        }

        /// <summary>断开后的状态处理：重连或通知</summary>
        private void HandlePostDisconnect()
        {
            if (_userDisconnect)
            {
                _state = ConnState.Disconnected;
                Debug.Log("[NetClient] Disconnected by user");
                OnDisconnected?.Invoke();
                return;
            }

            if (autoReconnect && _reconnectAttempts < maxReconnectAttempts)
            {
                _reconnectAttempts++;
                float delay = Math.Min(
                    baseReconnectDelay * (1 << (_reconnectAttempts - 1)),
                    maxReconnectDelay);
                _nextReconnectTime = Time.time + delay;
                _state = ConnState.Reconnecting;
                Debug.Log("[NetClient] Disconnected, reconnect in " + delay + "s (" + _reconnectAttempts + "/" +
                          maxReconnectAttempts + ")");
            }
            else
            {
                _state = ConnState.Disconnected;
                Debug.Log("[NetClient] Disconnected");
                OnDisconnected?.Invoke();
            }
        }

        // ==================== Heartbeat ====================

        private void SendHeartbeat()
        {
            var req = new HeartbeatReq();
            req.ClientTime = DateTime.UtcNow.Ticks;
            Send(req);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleHeartbeatRes(TcpSession session, HeartbeatRes res)
        {
            long nowTicks = DateTime.UtcNow.Ticks;
            long rttTicks = nowTicks - res.ClientTime;
            Instance.PingMs = (int)(rttTicks / TimeSpan.TicksPerMillisecond);
        }

        // ==================== Handshake ====================

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleHandshakeRes(TcpSession session, HandshakeRes res)
        {
            if (!string.IsNullOrEmpty(res.ErrorMessage))
            {
                Debug.LogWarning("[NetClient] Handshake failed: " + res.ErrorMessage);
                Instance._hadSession = false;
                Instance.DisposeSession(LogoutReason.NetDisconnected, "Handshake rejected: " + res.ErrorMessage);
                return;
            }

            if (Instance._hadSession && !string.IsNullOrEmpty(GameData.Token) && GameData.CurrentRole != null)
            {
                Debug.Log("[NetClient] Handshake OK, sending ReconnectReq...");
                var req = new ReconnectReq();
                req.Token = GameData.Token;
                req.RoleId = GameData.CurrentRole.RoleId;
                Instance.Send(req);
            }
            else
            {
                Debug.Log("[NetClient] Handshake OK, connected to " + Instance.host + ":" + Instance.port);
                Instance._state = ConnState.Connected;
                Instance.OnConnected?.Invoke();
            }
        }

        // ==================== Reconnect ====================

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleReconnectRes(TcpSession session, ReconnectRes res)
        {
            Instance._hadSession = false;

            if (string.IsNullOrEmpty(res.ErrorMessage))
            {
                Debug.Log("[NetClient] Reconnect OK, sending EnterGameReq...");
                GameData.Token = res.Token;
                Instance._state = ConnState.Connected;

                if (GameData.CurrentRole != null)
                {
                    Instance.Send(new EnterGameReq { RoleId = GameData.CurrentRole.RoleId });
                }

                Instance.OnReconnected?.Invoke();
            }
            else
            {
                Debug.LogWarning("[NetClient] Reconnect failed: " + res.ErrorMessage);
                GameData.Token = "";
                GameData.CurrentRole = null;
                GameData.CurrentMap = null;

                Instance._state = ConnState.Connected;
                Instance.OnReconnectFailed?.Invoke(res.ErrorMessage);
            }
        }

        // ==================== Kick ====================

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleKickNtf(TcpSession session, KickNtf ntf)
        {
            Debug.Log("[NetClient] Kicked, reason=" + ntf.Reason);
            Instance._userDisconnect = true;
            Instance._hadSession = false;
            GameData.Token = "";
            GameData.CurrentRole = null;
            GameData.CurrentMap = null;
            Instance.OnKicked?.Invoke(ntf.Reason);
            Instance.DisposeSession(LogoutReason.NetDisconnected, "Kicked by server");
        }
    }
}
