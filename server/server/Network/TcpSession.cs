using System.Net.Sockets;
using GameShared.Network;
using Server.Core.Log;

namespace Server.Network;

public class TcpSession : IDisposable
{
    private static readonly ILog Log = LogManager.GetLogger<TcpSession>();
    private readonly Socket _socket;
    private readonly MessageRouter _router;
    private readonly byte[] _recvBuffer = new byte[8192];
    private readonly MemoryStream _packetBuffer = new();
    private readonly object _sendLock = new();

    public ulong SessionId { get; }
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
    public event Action<TcpSession>? OnDisconnected;

    private static ulong _nextSessionId;

    public TcpSession(Socket socket, MessageRouter router)
    {
        _socket = socket;
        _router = router;
        SessionId = Interlocked.Increment(ref _nextSessionId);
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

    public void Disconnect()
    {
        if (!IsConnected) return;
        IsConnected = false;
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

        Log.Info($"断开连接 SessionId:{SessionId} UserId:{UserId}");
        OnDisconnected?.Invoke(this);
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

            // 分发给路由器处理（不 await，避免阻塞接收循环）
            _ = _router.RouteAsync(this, result.MsgId, result.Payload);
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