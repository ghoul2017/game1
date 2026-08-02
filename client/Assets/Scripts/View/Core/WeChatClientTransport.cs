#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Client.Logic;
using Shared.Common.Enums;
using Shared.Core.Network;
using WeChatWASM;

namespace Client.View
{
    /// <summary>
    /// 把微信小游戏平台 TCPSocket 接入正式 TcpSession，业务协议不感知平台 API。
    /// </summary>
    internal static class WeChatClientTransport
    {
        private static readonly object InitializationLock = new object();
        private static Task _initializationTask;

        [DllImport("__Internal")]
        private static extern void ExposeWechatSdkUtf8RuntimeMethods();

        internal static Task InitializeSdkAsync()
        {
            lock (InitializationLock)
            {
                if (_initializationTask == null)
                {
                    _initializationTask = InitializeSdkCoreAsync();
                }

                return _initializationTask;
            }
        }

        internal static async Task<TcpSession> ConnectSessionAsync(string host, int port)
        {
            await InitializeSdkAsync();
            WeChatByteTransport transport = new WeChatByteTransport();
            await transport.ConnectAsync(host, port);
            return Context.Network.AttachClientTransport(transport);
        }

        private static async Task InitializeSdkCoreAsync()
        {
            TaskCompletionSource<int> completion = new TaskCompletionSource<int>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            WX.InitSDK(code => completion.TrySetResult(code));

            Task finished = await Task.WhenAny(completion.Task, Task.Delay(10000));
            if (finished != completion.Task)
            {
                throw new TimeoutException("WX.InitSDK 回调超时");
            }

            ExposeWechatSdkUtf8RuntimeMethods();
            object systemInfo = WX.GetSystemInfoSync();
            if (systemInfo == null)
            {
                throw new InvalidOperationException("微信 SDK 未返回系统信息");
            }
        }
    }

    internal sealed class WeChatByteTransport : IByteTransport
    {
        private const int MaximumReliableQueuedBytes = 1024 * 1024;

        private readonly object _queueLock = new object();
        private readonly Queue<byte[]> _reliableQueue = new Queue<byte[]>();
        private WXTCPSocket _socket;
        private TaskCompletionSource<bool> _connectCompletion;
        private long _bytesReceived;
        private long _bytesSent;
        private int _disconnected = 1;
        private LogoutReason _disconnectReason = LogoutReason.NetDisconnected;
        private byte[] _latestSnapshot;
        private int _peakQueuedBytes;
        private int _queuedBytes;
        private int _reliableQueuedBytes;
        private long _replacedSnapshotCount;
        private bool _sendLoopRunning;

        public bool IsConnected => Volatile.Read(ref _disconnected) == 0;

        public string RemoteIp { get; private set; } = string.Empty;

        public long BytesSent => Interlocked.Read(ref _bytesSent);

        public long BytesReceivedCount => Interlocked.Read(ref _bytesReceived);

        public int QueuedBytes
        {
            get
            {
                lock (_queueLock)
                {
                    return _queuedBytes;
                }
            }
        }

        public int PeakQueuedBytes => Volatile.Read(ref _peakQueuedBytes);

        public long ReplacedSnapshotCount => Interlocked.Read(ref _replacedSnapshotCount);

        public event Action<byte[]> BytesReceived;

        public event Action<LogoutReason> Disconnected;

        internal async Task ConnectAsync(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                throw new ArgumentException("微信 TCP 地址不能为空", nameof(host));
            }

            if (port <= 0 || port > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(port), "微信 TCP 端口无效");
            }

            RemoteIp = host;
            _connectCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _socket = WX.CreateTCPSocket();
            if (_socket == null)
            {
                throw new InvalidOperationException("WX.CreateTCPSocket 未返回实例");
            }

            _socket.OnConnect(OnConnect);
            _socket.OnMessage(OnMessage, false);
            _socket.OnError(OnError);
            _socket.OnClose(OnClose);
            Volatile.Write(ref _disconnected, 0);
            _socket.Connect(new TCPSocketConnectOption
            {
                address = host,
                port = port,
                timeout = 10000.0
            });

            Task finished = await Task.WhenAny(_connectCompletion.Task, Task.Delay(10000));
            if (finished != _connectCompletion.Task)
            {
                Disconnect(LogoutReason.NetDisconnected);
                throw new TimeoutException("微信 TCP 连接超时");
            }

            await _connectCompletion.Task;
        }

        public void StartReceive()
        {
            // 微信 SDK 在 Connect 前已经安装消息回调，不需要额外接收循环。
        }

        public void Send(byte[] bytes, TransportSendMode mode)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (!IsConnected || _socket == null)
            {
                throw new InvalidOperationException("微信 TCP 连接已断开");
            }

            bool startSendLoop = false;
            lock (_queueLock)
            {
                if (mode == TransportSendMode.LatestSnapshot)
                {
                    if (_latestSnapshot != null)
                    {
                        _queuedBytes -= _latestSnapshot.Length;
                        Interlocked.Increment(ref _replacedSnapshotCount);
                    }

                    _latestSnapshot = bytes;
                    _queuedBytes += bytes.Length;
                }
                else
                {
                    if (_reliableQueuedBytes + bytes.Length > MaximumReliableQueuedBytes)
                    {
                        throw new InvalidOperationException("微信可靠发送队列超过 1 MB 上限");
                    }

                    _reliableQueue.Enqueue(bytes);
                    _reliableQueuedBytes += bytes.Length;
                    _queuedBytes += bytes.Length;
                }

                if (_queuedBytes > _peakQueuedBytes)
                {
                    _peakQueuedBytes = _queuedBytes;
                }

                if (!_sendLoopRunning)
                {
                    _sendLoopRunning = true;
                    startSendLoop = true;
                }
            }

            if (startSendLoop)
            {
                _ = SendLoopAsync();
            }
        }

        public void Disconnect(LogoutReason reason)
        {
            _disconnectReason = reason;
            if (Interlocked.Exchange(ref _disconnected, 1) != 0)
            {
                return;
            }

            try
            {
                _socket?.Close();
            }
            finally
            {
                ClearSendQueue();
                Disconnected?.Invoke(reason);
            }
        }

        public void Dispose()
        {
            Disconnect(LogoutReason.NetDisconnected);
            _socket = null;
        }

        private void OnConnect(GeneralCallbackResult result)
        {
            if (Volatile.Read(ref _disconnected) != 0)
            {
                return;
            }

            _connectCompletion.TrySetResult(true);
        }

        private void OnMessage(TCPSocketOnMessageListenerResult result)
        {
            if (result == null || result.message == null)
            {
                return;
            }

            byte[] message = result.message;
            Interlocked.Add(ref _bytesReceived, message.Length);
            BytesReceived?.Invoke(message);
        }

        private void OnError(GeneralCallbackResult result)
        {
            string error = result == null || string.IsNullOrWhiteSpace(result.errMsg)
                ? "微信 TCP 未知错误"
                : result.errMsg;
            _connectCompletion?.TrySetException(new InvalidOperationException(error));
            Disconnect(LogoutReason.NetDisconnected);
        }

        private void OnClose(GeneralCallbackResult result)
        {
            _connectCompletion?.TrySetException(new InvalidOperationException("微信 TCP 在连接前关闭"));
            if (Interlocked.Exchange(ref _disconnected, 1) == 0)
            {
                ClearSendQueue();
                Disconnected?.Invoke(_disconnectReason);
            }
        }

        private async Task SendLoopAsync()
        {
            try
            {
                while (IsConnected)
                {
                    await Task.Yield();
                    byte[] packet = TakeNextPacket();
                    if (packet == null)
                    {
                        return;
                    }

                    if (!IsConnected || _socket == null)
                    {
                        return;
                    }

                    _socket.Write(packet);
                    Interlocked.Add(ref _bytesSent, packet.Length);
                }
            }
            catch (Exception exception)
            {
                _connectCompletion?.TrySetException(exception);
                Disconnect(LogoutReason.NetDisconnected);
            }
        }

        private byte[] TakeNextPacket()
        {
            lock (_queueLock)
            {
                byte[] packet = null;
                if (_reliableQueue.Count > 0)
                {
                    packet = _reliableQueue.Dequeue();
                    _reliableQueuedBytes -= packet.Length;
                }
                else if (_latestSnapshot != null)
                {
                    packet = _latestSnapshot;
                    _latestSnapshot = null;
                }

                if (packet != null)
                {
                    _queuedBytes -= packet.Length;
                    return packet;
                }

                _sendLoopRunning = false;
                return null;
            }
        }

        private void ClearSendQueue()
        {
            lock (_queueLock)
            {
                _reliableQueue.Clear();
                _latestSnapshot = null;
                _reliableQueuedBytes = 0;
                _queuedBytes = 0;
                _sendLoopRunning = false;
            }
        }
    }
}
#endif
