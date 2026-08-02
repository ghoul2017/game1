using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Shared.Common.Enums;
using Shared.Core.Log;

#nullable enable
namespace Shared.Core.Network
{
    /// <summary>
    /// 为 Server、Robot 和原生客户端提供有界异步发送的 Socket 字节传输。
    /// </summary>
    public sealed class SocketByteTransport : IByteTransport
    {
        private const int MaximumReliableQueuedBytes = 1024 * 1024;
        private const int ReceiveBufferSize = 8192;

        private static readonly ILog Log = LogManager.GetLogger<SocketByteTransport>();
        private readonly object _queueLock = new object();
        private readonly Queue<byte[]> _reliableQueue = new Queue<byte[]>();
        private readonly byte[] _receiveBuffer = new byte[ReceiveBufferSize];
        private readonly Socket _socket;
        private long _bytesReceived;
        private long _bytesSent;
        private int _disconnected;
        private byte[]? _latestSnapshot;
        private int _peakQueuedBytes;
        private int _queuedBytes;
        private int _receiveStarted;
        private long _replacedSnapshotCount;
        private bool _sendLoopRunning;

        public SocketByteTransport(Socket socket)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            RemoteIp = (socket.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? string.Empty;
        }

        public bool IsConnected => Volatile.Read(ref _disconnected) == 0;

        public string RemoteIp { get; }

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

        public event Action<byte[]>? BytesReceived;

        public event Action<LogoutReason>? Disconnected;

        public void StartReceive()
        {
            if (Interlocked.Exchange(ref _receiveStarted, 1) != 0)
            {
                return;
            }

            _ = ReceiveLoopAsync();
        }

        public void Send(byte[] bytes, TransportSendMode mode)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (!IsConnected)
            {
                throw new InvalidOperationException("连接已断开");
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
                    if (_queuedBytes + bytes.Length > MaximumReliableQueuedBytes)
                    {
                        throw new InvalidOperationException("可靠发送队列超过 1 MB 上限");
                    }

                    _reliableQueue.Enqueue(bytes);
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
            if (Interlocked.Exchange(ref _disconnected, 1) != 0)
            {
                return;
            }

            try
            {
                _socket.Shutdown(SocketShutdown.Both);
            }
            catch
            {
            }

            try
            {
                _socket.Close();
            }
            catch
            {
            }

            Disconnected?.Invoke(reason);
        }

        public void Dispose()
        {
            Disconnect(LogoutReason.NetDisconnected);
            _socket.Dispose();
        }

        private async Task ReceiveLoopAsync()
        {
            try
            {
                while (IsConnected)
                {
                    int bytesRead = await _socket.ReceiveAsync(
                        new ArraySegment<byte>(_receiveBuffer),
                        SocketFlags.None);
                    if (bytesRead == 0)
                    {
                        Disconnect(LogoutReason.NetDisconnected);
                        return;
                    }

                    byte[] received = new byte[bytesRead];
                    Buffer.BlockCopy(_receiveBuffer, 0, received, 0, bytesRead);
                    Interlocked.Add(ref _bytesReceived, bytesRead);
                    BytesReceived?.Invoke(received);
                }
            }
            catch (SocketException)
            {
                Disconnect(LogoutReason.NetDisconnected);
            }
            catch (ObjectDisposedException)
            {
                Disconnect(LogoutReason.NetDisconnected);
            }
            catch (Exception exception)
            {
                Log.Error($"[SocketByteTransport] 接收异常: {exception.Message}");
                Disconnect(LogoutReason.NetDisconnected);
            }
        }

        private async Task SendLoopAsync()
        {
            try
            {
                while (IsConnected)
                {
                    byte[]? packet = TakeNextPacket();
                    if (packet == null)
                    {
                        return;
                    }

                    int offset = 0;
                    while (offset < packet.Length)
                    {
                        int sent = await _socket.SendAsync(
                            new ArraySegment<byte>(packet, offset, packet.Length - offset),
                            SocketFlags.None);
                        if (sent <= 0)
                        {
                            throw new SocketException((int)SocketError.ConnectionReset);
                        }

                        offset += sent;
                        Interlocked.Add(ref _bytesSent, sent);
                    }
                }
            }
            catch (Exception exception)
            {
                if (IsConnected)
                {
                    Log.Error($"[SocketByteTransport] 发送异常: {exception.Message}");
                }

                Disconnect(LogoutReason.NetDisconnected);
            }
        }

        private byte[]? TakeNextPacket()
        {
            lock (_queueLock)
            {
                byte[]? packet = null;
                if (_reliableQueue.Count > 0)
                {
                    packet = _reliableQueue.Dequeue();
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
    }
}
