using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ProtoBuf;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Robot
{
    /// <summary>
    /// Robot 客户端会话。管理 TCP 连接、消息收发，提供请求/响应等待。
    /// </summary>
    public class RobotSession : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger<RobotSession>();

        private Socket? _socket;
        private NetworkStream? _stream;
        private CancellationTokenSource? _cts;
        private readonly byte[] _recvBuffer = new byte[8192];
        private readonly MemoryStream _packetBuffer = new();

        /// <summary>消息处理器：msgId → 处理 payload 的回调</summary>
        private readonly ConcurrentDictionary<ushort, Action<byte[]>> _handlers = new();

        public bool IsConnected { get; private set; }
        public long UserId { get; set; }
        public long RoleId { get; set; }

        /// <summary>连接服务器</summary>
        public async Task ConnectAsync(string host, int port)
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await Task.Factory.FromAsync(
                (callback, state) => _socket.BeginConnect(host, port, callback, state),
                _socket.EndConnect,
                null);
            _stream = new NetworkStream(_socket);
            _cts = new CancellationTokenSource();
            IsConnected = true;

            Log.Info($"Robot 连接 {host}:{port}");

            _ = ReceiveLoopAsync(_cts.Token);
        }

        /// <summary>发送消息</summary>
        public async Task SendAsync(IMessage msg)
        {
            if (!IsConnected || _stream == null)
                throw new InvalidOperationException("未连接");

            var packet = PacketCodec.Encode(msg.MsgId, msg.Serialize());
            await _stream.WriteAsync(packet, 0, packet.Length);
        }

        /// <summary>发送请求并等待指定类型的响应</summary>
        public async Task<T> SendAndWaitAsync<T>(IMessage req, int timeoutMs = 5000) where T : IMessage
        {
            var waitTask = WaitForMsgAsync<T>(timeoutMs);
            await SendAsync(req);
            return await waitTask;
        }

        /// <summary>等待指定类型的消息（请求响应或服务端推送）</summary>
        public async Task<T> WaitForMsgAsync<T>(int timeoutMs = 5000) where T : IMessage
        {
            if (!IsConnected)
                throw new InvalidOperationException("未连接");

            var msgId = Activator.CreateInstance<T>().MsgId;
            var tcs = new TaskCompletionSource<T>();

            _handlers[msgId] = payload =>
            {
                try
                {
                    using var ms = new MemoryStream(payload);
                    var msg = Serializer.Deserialize<T>(ms);
                    tcs.TrySetResult(msg);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            };

            using var cts = new CancellationTokenSource(timeoutMs);
            cts.Token.Register(() =>
            {
                _handlers.TryRemove(msgId, out _);
                tcs.TrySetCanceled();
            });

            try
            {
                return await tcs.Task;
            }
            catch (TaskCanceledException)
            {
                throw new TimeoutException($"等待消息 {typeof(T).Name} 超时 ({timeoutMs}ms)");
            }
        }

        /// <summary>断开连接</summary>
        public void Disconnect()
        {
            IsConnected = false;
            _cts?.Cancel();

            try { _stream?.Close(); } catch { }
            try { _socket?.Close(); } catch { }

            Log.Info("Robot 断开连接");
        }

        public void Dispose()
        {
            Disconnect();
            _cts?.Dispose();
            _stream?.Dispose();
            _packetBuffer.Dispose();
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && IsConnected)
                {
                    var bytesRead = await _socket!.ReceiveAsync(new ArraySegment<byte>(_recvBuffer), SocketFlags.None);
                    if (bytesRead == 0)
                    {
                        Disconnect();
                        return;
                    }

                    _packetBuffer.Write(_recvBuffer, 0, bytesRead);
                    ProcessBuffer();
                }
            }
            catch (SocketException)
            {
                Disconnect();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                Log.Error($"Robot 接收异常: {ex.Message}");
                Disconnect();
            }
        }

        private void ProcessBuffer()
        {
            var data = _packetBuffer.ToArray();
            var offset = 0;

            while (offset < data.Length)
            {
                var result = PacketCodec.TryDecode(data, offset, data.Length - offset);
                if (result == null)
                    break;

                offset += result.Consumed;

                if (_handlers.TryGetValue(result.MsgId, out var handler))
                {
                    handler(result.Payload);
                }
                else
                {
                    Log.Warn($"Robot 未处理的消息 MsgId:{result.MsgId}");
                }
            }

            if (offset > 0)
            {
                _packetBuffer.SetLength(0);
                if (offset < data.Length)
                    _packetBuffer.Write(data, offset, data.Length - offset);
            }
        }
    }
}
