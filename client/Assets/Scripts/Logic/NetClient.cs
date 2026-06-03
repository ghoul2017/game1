using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Shared.Common;
using Shared.Common.Enums;
using Shared.Common.Protos;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Client.Logic
{
    /// <summary>
    /// 客户端网络管理器。基于 NetworkComponent + TcpSession，
    /// 负责连接、心跳、断线重连。消息处理由 NetworkComponent 的 MessageRouter 统一分发。
    /// </summary>
    public class NetClient
    {
        public static NetClient Instance { get; private set; }

        public string host = "127.0.0.1";
        public int port = 9000;

        public float heartbeatInterval = 30f;
        public float recvTimeout = 60f;

        public bool autoReconnect = true;
        public int maxReconnectAttempts = 5;
        public float baseReconnectDelay = 1f;
        public float maxReconnectDelay = 30f;

        private static readonly ILog Log = LogManager.GetLogger<NetClient>();

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
            _state == ConnState.Reconnecting ? Math.Max(0f, _nextReconnectTime - _currentTime) : 0f;

        private ConnState _state = ConnState.Disconnected;

        // --- Internals ---
        private TcpSession _session;
        private readonly ConcurrentQueue<Action> _mainQueue = new ConcurrentQueue<Action>();

        // Timing
        private float _currentTime;
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

        public void Initialize()
        {
            if (Instance != null && Instance != this)
            {
                Log.Warn("[NetClient] Duplicate instance, ignoring");
                return;
            }

            Instance = this;

            Context.Client.AddComponent<NetworkComponent>();
        }

        /// <summary>
        /// 每帧由 GameEntry 调用，deltaTime 为 Time.deltaTime
        /// </summary>
        public void Update(float deltaTime)
        {
            _currentTime += deltaTime;

            // Drain main-thread queue
            while (_mainQueue.TryDequeue(out Action action))
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Log.Error("[NetClient] Main queue action error: " + e);
                }
            }

            // Reconnect timer
            if (_state == ConnState.Reconnecting && _currentTime >= _nextReconnectTime)
            {
                Log.Info(string.Format("[NetClient] Reconnecting ({0}/{1})...", _reconnectAttempts, maxReconnectAttempts));
                _ = StartConnectAsync();
            }

            // Handshake timeout
            if (_state == ConnState.Handshaking)
            {
                if (_currentTime - _lastRecvTime > recvTimeout)
                {
                    Log.Warn("[NetClient] Handshake timeout");
                    DisposeSession(LogoutReason.NetDisconnected, "Handshake timeout");
                }
            }

            // Heartbeat
            if (_state == ConnState.Connected)
            {
                float now = _currentTime;
                if (now - _lastSendTime >= heartbeatInterval)
                {
                    _ = SendHeartbeat();
                }

                if (now - _lastRecvTime > recvTimeout)
                {
                    Log.Warn("[NetClient] Receive timeout, disconnecting");
                    DisposeSession(LogoutReason.NetDisconnected, "Receive timeout");
                }
            }
        }

        public void Dispose()
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
                Log.Warn("[NetClient] Cannot send: not connected");
                return;
            }

            _session?.Send(msg);
            _lastSendTime = _currentTime;
        }

        public Task<TRes> CallAsync<TRes>(IMessage req, int timeoutMs = 5000) where TRes : IMessage
        {
            if (_state != ConnState.Connected && _state != ConnState.Handshaking)
            {
                Log.Warn("[NetClient] Cannot call: not connected");
                var tcs = new TaskCompletionSource<TRes>();
                tcs.SetException(new InvalidOperationException("网络未连接"));
                return tcs.Task;
            }

            if (_session == null)
            {
                var tcs = new TaskCompletionSource<TRes>();
                tcs.SetException(new InvalidOperationException("网络会话不存在"));
                return tcs.Task;
            }

            _lastSendTime = _currentTime;
            return _session.CallAsync<TRes>(req, timeoutMs);
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
                _lastRecvTime = _currentTime;
                action();
            });
            _session.OnDisconnected += OnSessionDisconnected;

            _state = ConnState.Handshaking;
            _reconnectAttempts = 0;
            _lastRecvTime = _currentTime;
            _lastSendTime = _currentTime;
            Log.Info("[NetClient] TCP connected, sending handshake...");

            _ = StartHandshakeAsync();
        }

        private void HandleConnectFailed(string reason)
        {
            Log.Warn("[NetClient] Connect failed: " + reason);

            if (autoReconnect && !_userDisconnect && _reconnectAttempts < maxReconnectAttempts)
            {
                _reconnectAttempts++;
                float delay = Math.Min(
                    baseReconnectDelay * (1 << (_reconnectAttempts - 1)),
                    maxReconnectDelay);
                _nextReconnectTime = _currentTime + delay;
                _state = ConnState.Reconnecting;
                Log.Info(string.Format("[NetClient] Retry in {0}s ({1}/{2})", delay, _reconnectAttempts, maxReconnectAttempts));
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
                Log.Info("[NetClient] DisposeSession: " + debugMsg);
            }

            HandlePostDisconnect();
        }

        /// <summary>断开后的状态处理：重连或通知</summary>
        private void HandlePostDisconnect()
        {
            if (_userDisconnect)
            {
                _state = ConnState.Disconnected;
                Log.Info("[NetClient] Disconnected by user");
                OnDisconnected?.Invoke();
                return;
            }

            if (autoReconnect && _reconnectAttempts < maxReconnectAttempts)
            {
                _reconnectAttempts++;
                float delay = Math.Min(
                    baseReconnectDelay * (1 << (_reconnectAttempts - 1)),
                    maxReconnectDelay);
                _nextReconnectTime = _currentTime + delay;
                _state = ConnState.Reconnecting;
                Log.Info(string.Format("[NetClient] Disconnected, reconnect in {0}s ({1}/{2})", delay, _reconnectAttempts, maxReconnectAttempts));
            }
            else
            {
                _state = ConnState.Disconnected;
                Log.Info("[NetClient] Disconnected");
                OnDisconnected?.Invoke();
            }
        }

        // ==================== Heartbeat ====================
        private async Task SendHeartbeat()
        {
            try
            {
                var req = new HeartbeatReq();
                req.ClientTime = DateTime.UtcNow.Ticks;
                HeartbeatRes res = await CallAsync<HeartbeatRes>(req);
                long nowTicks = DateTime.UtcNow.Ticks;
                long rttTicks = nowTicks - res.ClientTime;
                PingMs = (int)(rttTicks / TimeSpan.TicksPerMillisecond);
            }
            catch (Exception ex)
            {
                Log.Warn("[NetClient] 心跳失败: " + ex.Message);
            }
        }

        // ==================== Handshake ====================

        private async Task StartHandshakeAsync()
        {
            try
            {
                HandshakeRes res = await CallAsync<HandshakeRes>(new HandshakeReq());

                if (!string.IsNullOrEmpty(res.ErrorMessage))
                {
                    Log.Warn("[NetClient] 握手失败: " + res.ErrorMessage);
                    _hadSession = false;
                    DisposeSession(LogoutReason.NetDisconnected, "握手被拒绝: " + res.ErrorMessage);
                    return;
                }

                if (_hadSession && !string.IsNullOrEmpty(GameData.Token) && GameData.CurrentRole != null)
                {
                    await ReconnectAsync();
                }
                else
                {
                    Log.Info(string.Format("[NetClient] Handshake OK, connected to {0}:{1}", host, port));
                    _state = ConnState.Connected;
                    OnConnected?.Invoke();
                }
            }
            catch (Exception ex)
            {
                Log.Warn("[NetClient] 握手失败: " + ex.Message);
                _hadSession = false;
                DisposeSession(LogoutReason.NetDisconnected, "握手失败: " + ex.Message);
            }
        }

        // ==================== Reconnect ====================

        private async Task ReconnectAsync()
        {
            Log.Info("[NetClient] Handshake OK, sending ReconnectReq...");
            var req = new ReconnectReq();
            req.Token = GameData.Token;
            req.RoleId = GameData.CurrentRole.RoleId;
            ReconnectRes res = await CallAsync<ReconnectRes>(req);
            _hadSession = false;

            if (string.IsNullOrEmpty(res.ErrorMessage))
            {
                Log.Info("[NetClient] Reconnect OK, sending EnterGameReq...");
                GameData.Token = res.Token;
                _state = ConnState.Connected;

                if (GameData.CurrentRole != null)
                {
                    EnterGameRes enterGameRes = await CallAsync<EnterGameRes>(
                        new EnterGameReq { RoleId = GameData.CurrentRole.RoleId });
                    if (!string.IsNullOrEmpty(enterGameRes.ErrorMessage))
                    {
                        OnReconnectFailed?.Invoke(enterGameRes.ErrorMessage);
                        return;
                    }
                }

                OnReconnected?.Invoke();
            }
            else
            {
                Log.Warn("[NetClient] Reconnect failed: " + res.ErrorMessage);
                GameData.Token = "";
                GameData.CurrentRole = null;
                GameData.CurrentMap = null;

                _state = ConnState.Connected;
                OnReconnectFailed?.Invoke(res.ErrorMessage);
            }
        }

        // ==================== Kick ====================

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleKickNtf(TcpSession session, KickNtf ntf)
        {
            Log.Info(string.Format("[NetClient] Kicked, reason={0}", ntf.Reason));
            if (Instance == null) return;

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
