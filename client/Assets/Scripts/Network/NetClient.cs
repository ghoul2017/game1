using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using GameShared.Network;
using GameShared.Protos;
using UnityEngine;

namespace Client.Network
{
    /// <summary>
    /// 客户端TCP网络管理器
    /// 负责：连接管理、消息收发、心跳保活、断线重连
    /// </summary>
    public class NetClient : MonoBehaviour
    {
        public static NetClient Instance { get; private set; }

        [Header("Server")]
        public string host = "127.0.0.1";
        public int port = 9000;

        [Header("Heartbeat")]
        public float heartbeatInterval = 30f;
        public float recvTimeout = 60f;

        [Header("Reconnect")]
        public bool autoReconnect = true;
        public int maxReconnectAttempts = 5;
        public float baseReconnectDelay = 1f;
        public float maxReconnectDelay = 30f;

        // --- State ---
        private enum ConnState { Disconnected, Connecting, Connected, Reconnecting }

        public bool IsConnected { get { return _state == ConnState.Connected && IsSocketAlive(); } }
        public bool IsConnecting { get { return _state == ConnState.Connecting || _state == ConnState.Reconnecting; } }

        private ConnState _state = ConnState.Disconnected;

        // --- Internals ---
        private TcpClient _tcp;
        private NetworkStream _stream;
        private Thread _recvThread;
        private volatile bool _running;
        private readonly object _sendLock = new object();

        // Receive
        private byte[] _recvBuffer = new byte[8192];
        private readonly List<byte> _pendingData = new List<byte>();
        private readonly ConcurrentQueue<Action> _mainQueue = new ConcurrentQueue<Action>();

        // Timing
        private float _lastSendTime;
        private float _lastRecvTime;

        // Reconnect
        private int _reconnectAttempts;
        private float _nextReconnectTime;
        private bool _userDisconnect;

        // --- Events ---
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnError;

        // --- External ---
        private MessageDispatcher _dispatcher;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            // Drain main-thread queue
            while (_mainQueue.TryDequeue(out Action action))
            {
                try { action(); }
                catch (Exception e) { Debug.LogException(e); }
            }

            // Reconnect timer
            if (_state == ConnState.Reconnecting && Time.time >= _nextReconnectTime)
            {
                Debug.Log("[NetClient] Reconnecting (" + _reconnectAttempts + "/" + maxReconnectAttempts + ")...");
                DoConnect();
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
                    HandleDisconnect("Receive timeout");
                }
            }
        }

        void OnDestroy()
        {
            DoDisconnect();
            if (Instance == this) Instance = null;
        }

        // ==================== Public API ====================

        public MessageDispatcher Dispatcher { get { return _dispatcher; } }

        public void SetDispatcher(MessageDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public void Connect(string host = null, int? port = null)
        {
            if (_state == ConnState.Connecting || _state == ConnState.Connected)
                return;

            if (host != null) this.host = host;
            if (port != null) this.port = port.Value;

            _userDisconnect = false;
            _reconnectAttempts = 0;
            DoConnect();
        }

        public void Disconnect()
        {
            _userDisconnect = true;
            HandleDisconnect("User disconnect");
        }

        public void Send(IMessage msg)
        {
            if (_state != ConnState.Connected || _stream == null)
            {
                Debug.LogWarning("[NetClient] Cannot send: not connected");
                return;
            }

            byte[] payload = msg.Serialize();
            byte[] packet = PacketCodec.Encode(msg.MsgId, payload);

            try
            {
                lock (_sendLock)
                {
                    _stream.Write(packet, 0, packet.Length);
                    _stream.Flush();
                }
                _lastSendTime = Time.time;
            }
            catch (Exception ex)
            {
                Debug.LogError("[NetClient] Send error: " + ex.Message);
                HandleDisconnect("Send error");
            }
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

        private void DoConnect()
        {
            _state = ConnState.Connecting;
            _running = true;
            _pendingData.Clear();

            try
            {
                _tcp = new TcpClient();
                _tcp.NoDelay = true;
                _tcp.BeginConnect(host, port, OnConnectResult, null);
            }
            catch (Exception ex)
            {
                Debug.LogError("[NetClient] Connect error: " + ex.Message);
                HandleConnectFailed(ex.Message);
            }
        }

        private void OnConnectResult(IAsyncResult ar)
        {
            try
            {
                _tcp.EndConnect(ar);
                _stream = _tcp.GetStream();

                _recvThread = new Thread(ReceiveLoop) { IsBackground = true };
                _recvThread.Start();

                _mainQueue.Enqueue(() =>
                {
                    if (!_running) return;
                    _state = ConnState.Connected;
                    _reconnectAttempts = 0;
                    _lastSendTime = Time.time;
                    _lastRecvTime = Time.time;
                    Debug.Log("[NetClient] Connected to " + host + ":" + port);
                    OnConnected?.Invoke();
                });
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                _mainQueue.Enqueue(() => HandleConnectFailed(msg));
            }
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
                Debug.Log("[NetClient] Retry in " + delay + "s (" + _reconnectAttempts + "/" + maxReconnectAttempts + ")");
            }
            else
            {
                _state = ConnState.Disconnected;
                OnError?.Invoke(reason);
            }
        }

        private void HandleDisconnect(string reason)
        {
            if (_state != ConnState.Connected) return;

            DoDisconnect();

            if (!_userDisconnect && autoReconnect && _reconnectAttempts < maxReconnectAttempts)
            {
                _reconnectAttempts++;
                float delay = Math.Min(
                    baseReconnectDelay * (1 << (_reconnectAttempts - 1)),
                    maxReconnectDelay);
                _nextReconnectTime = Time.time + delay;
                _state = ConnState.Reconnecting;
                Debug.Log("[NetClient] Disconnected (" + reason + "), reconnect in " + delay + "s");
            }
            else
            {
                _state = ConnState.Disconnected;
                Debug.Log("[NetClient] Disconnected: " + reason);
                OnDisconnected?.Invoke();
            }
        }

        private void DoDisconnect()
        {
            _running = false;
            _state = ConnState.Disconnected;

            try { _stream?.Close(); } catch { }
            try { _tcp?.Close(); } catch { }
            try { if (_recvThread != null && _recvThread.IsAlive) _recvThread.Join(1000); } catch { }

            _stream = null;
            _tcp = null;
            _recvThread = null;
            _pendingData.Clear();
        }

        // ==================== Receive ====================

        private void ReceiveLoop()
        {
            while (_running)
            {
                try
                {
                    int bytesRead = _stream.Read(_recvBuffer, 0, _recvBuffer.Length);
                    if (bytesRead == 0)
                    {
                        _mainQueue.Enqueue(() => HandleDisconnect("Server closed"));
                        break;
                    }

                    byte[] data = new byte[bytesRead];
                    Buffer.BlockCopy(_recvBuffer, 0, data, 0, bytesRead);

                    _mainQueue.Enqueue(() =>
                    {
                        ProcessReceivedBytes(data);
                        _lastRecvTime = Time.time;
                    });
                }
                catch (IOException)
                {
                    if (_running)
                        _mainQueue.Enqueue(() => HandleDisconnect("Connection lost"));
                    break;
                }
                catch (Exception ex)
                {
                    if (_running)
                        _mainQueue.Enqueue(() => HandleDisconnect("Recv error: " + ex.Message));
                    break;
                }
            }
        }

        private void ProcessReceivedBytes(byte[] data)
        {
            _pendingData.AddRange(data);

            int offset = 0;
            byte[] buf = _pendingData.ToArray();
            int count = buf.Length;

            while (count - offset >= PacketCodec.HeaderSize)
            {
                PacketCodec.DecodeResult result = PacketCodec.TryDecode(buf, offset, count - offset);
                if (result == null) break;

                if (_dispatcher != null)
                    _dispatcher.Dispatch(result.MsgId, result.Payload);

                offset += result.Consumed;
            }

            if (offset > 0)
                _pendingData.RemoveRange(0, offset);
        }

        // ==================== Heartbeat ====================

        private void SendHeartbeat()
        {
            var req = new HeartbeatReq();
            req.ClientTime = DateTime.UtcNow.Ticks;
            Send(req);
        }

        // ==================== Helpers ====================

        private bool IsSocketAlive()
        {
            try { return _tcp != null && _tcp.Connected; }
            catch { return false; }
        }
    }
}
