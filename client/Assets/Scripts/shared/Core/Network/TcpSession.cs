using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Shared.Common.Enums;
using Shared.Core.Network;
using Shared.Core.Log;

#nullable enable
namespace Shared.Core.Network
{
    public class TcpSession : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger<TcpSession>();
        private readonly Socket _socket;
        private readonly MessageRouter _router;
        private readonly byte[] _recvBuffer = new byte[8192];
        private readonly MemoryStream _packetBuffer = new();
        private readonly object _sendLock = new();

        public long SessionId { get; }
        public bool IsConnected { get; private set; }

        /// <summary>绑定的用户ID（登录后设置）</summary>
        public long UserId { get; set; }

        /// <summary>绑定的角色ID（选角后设置）</summary>
        public long RoleId { get; set; }

        /// <summary>重连令牌（登录时生成）</summary>
        public string ReconnectToken { get; set; } = string.Empty;

        /// <summary>最后活跃时间（收到任何消息时更新）</summary>
        public DateTime LastActiveTime { get; private set; } = DateTime.UtcNow;

        /// <summary>会话断开时的回调</summary>
        public event Action<TcpSession, LogoutReason>? OnDisconnected;

        /// <summary>断开原因（Disconnect 后可读）</summary>
        public LogoutReason DisconnectReason { get; private set; } = LogoutReason.NetDisconnected;

        /// <summary>可选的调度器，用于将消息处理投递到主线程（客户端设置，服务端为 null）</summary>
        public Action<Action>? PostToMainThread { get; set; }

        /// <summary>收到消息时的回调（在 RouteAsync 之前调用）</summary>
        public Action<TcpSession, ushort, byte[]>? OnMessageReceived { get; set; }

        public TcpSession(Socket socket, MessageRouter router)
        {
            _socket = socket;
            _router = router;
            SessionId = IdGenerator.Instance.NextId();
            IsConnected = true;
        }

        public void StartReceive()
        {
            _ = ReceiveLoopAsync();
        }

        public void Send(IMessage msg)
        {
            Send(msg.MsgId, msg.Serialize());
        }

        public void Send(ushort msgId, byte[] payload)
        {
            if (!IsConnected) return;

            var packet = PacketCodec.Encode(msgId, payload);
            try
            {
                lock (_sendLock)
                {
                    _socket.Send(packet);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"发送失败: {ex.Message}");
                Disconnect();
            }
        }

        public void Disconnect(LogoutReason reason = LogoutReason.NetDisconnected)
        {
            if (!IsConnected) return;
            IsConnected = false;
            DisconnectReason = reason;
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

            Log.Info($"断开连接 SessionId:{SessionId} UserId:{UserId} Reason:{reason}");
            OnDisconnected?.Invoke(this, reason);
        }

        /// <summary>更新最后活跃时间（收到消息时调用）</summary>
        public void UpdateActiveTime()
        {
            LastActiveTime = DateTime.UtcNow;
        }

        public void Dispose()
        {
            Disconnect();
            _packetBuffer.Dispose();
        }

        private async Task ReceiveLoopAsync()
        {
            try
            {
                while (IsConnected)
                {
                    var bytesRead = await _socket.ReceiveAsync(new ArraySegment<byte>(_recvBuffer), SocketFlags.None);
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
                Log.Error($"接收异常: {ex.Message}");
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

                // 收到任何消息都更新活跃时间
                UpdateActiveTime();

                if (PostToMainThread != null)
                {
                    var msgId = result.MsgId;
                    var payload = result.Payload;
                    var self = this;
                    PostToMainThread(() =>
                    {
                        OnMessageReceived?.Invoke(self, msgId, payload);
                        _ = _router.RouteAsync(self, msgId, payload);
                    });
                }
                else
                {
                    OnMessageReceived?.Invoke(this, result.MsgId, result.Payload);
                    _ = _router.RouteAsync(this, result.MsgId, result.Payload);
                }
            }

            // 保留未消费的数据
            if (offset > 0)
            {
                _packetBuffer.SetLength(0);
                if (offset < data.Length)
                    _packetBuffer.Write(data, offset, data.Length - offset);
            }
        }
    }
}