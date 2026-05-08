using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Shared.Common.Enums;
using Shared.Core.ECS;
using Shared.Core.Log;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Core.Network;

/// <summary>
/// 网络组件。管理 TCP 连接和心跳检测，不持有业务级会话映射。
/// session 引用由各业务对象（User、PlayerOnlineComponent）自行持有。
/// 通过 SessionDisconnected 回调通知上层清理 session 引用。
/// </summary>
[ComponentOf(typeof(Entities.Module))]
public class NetworkComponent : Component
{
    private static readonly ILog Log = LogManager.GetLogger<NetworkComponent>();

    private readonly MessageRouter _router;
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 9000;
    public int MaxConnections { get; set; } = 5000;

    private Socket? _listenSocket;
    private readonly Dictionary<long, TcpSession> _sessionMap = new(); // sessionId -> session

    private const int HeartbeatTimeoutSeconds = 60;
    private const int CheckIntervalSeconds = 20;

    private GameTimerHandle _heartbeatTimer;

    /// <summary>会话断开回调。上层注册此回调清理业务对象上的 session 引用。</summary>
    public Action<TcpSession>? SessionDisconnected { get; set; }

    public NetworkComponent()
    {
        _router = new MessageRouter();
        _router.AutoRegister();
        _heartbeatTimer = GameTimer.Start(CheckIntervalSeconds * 1000, DoHeartbeatCheck);
    }

    public async Task Listen(string host, int port, int maxConnections = 5000)
    {
        Host = host;
        Port = port;
        MaxConnections = maxConnections;

        var endpoint = new IPEndPoint(IPAddress.Parse(Host), Port);
        _listenSocket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        _listenSocket.Bind(endpoint);
        _listenSocket.Listen(MaxConnections);

        Log.Info($"监听 {Host}:{Port}");

        await AcceptAsync(_listenSocket);
    }

    private async Task AcceptAsync(Socket listenSocket)
    {
        try
        {
            while (!ShutdownToken.Instance.IsCancellationRequested)
            {
                var clientSocket = await listenSocket.AcceptAsync(ShutdownToken.Instance.Token);
                var session = new TcpSession(clientSocket, _router);

                if (_sessionMap.Count >= MaxConnections)
                {
                    session.Disconnect();
                    Log.Warn("达到最大连接数，拒绝新连接");
                    continue;
                }

                _sessionMap[session.SessionId] = session;
                session.OnDisconnected += OnSessionDisconnected;
                session.StartReceive();

                Log.Info($"会话添加 SessionId:{session.SessionId}，当前连接数: {_sessionMap.Count}");
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            Log.Info("正在关闭...");
            KickAll(LogoutReason.ServerClose);
            _listenSocket?.Close();
        }
    }

    private void OnSessionDisconnected(TcpSession session)
    {
        // 通知上层清理业务对象上的 session 引用
        SessionDisconnected?.Invoke(session);

        _sessionMap.Remove(session.SessionId);
        session.OnDisconnected -= OnSessionDisconnected;
        session.Dispose();

        Log.Info($"会话断开 SessionId:{session.SessionId}，当前连接数: {_sessionMap.Count}");
    }

    /// <summary>按 sessionId 获取</summary>
    public TcpSession? Get(long sessionId)
    {
        return _sessionMap.GetValueOrDefault(sessionId);
    }

    /// <summary>踢人</summary>
    public void Kick(TcpSession session, LogoutReason reason)
    {
        Log.Info($"踢人 SessionId:{session.SessionId} UserId:{session.UserId} Reason:{reason}");
        try
        {
            session.Send(new KickNtf { Reason = (int)reason });
        }
        catch (Exception e)
        {
            Log.Error("踢人失败", e);
        }
        session.Disconnect();
    }

    /// <summary>服务器关闭时踢出所有连接</summary>
    public void KickAll(LogoutReason reason)
    {
        var sessions = new List<TcpSession>(_sessionMap.Values);
        foreach (var session in sessions)
        {
            session.Disconnect();
        }
        Log.Info($"全部断开，共 {sessions.Count} 个连接");
    }

    private void DoHeartbeatCheck()
    {
        var now = DateTime.UtcNow;
        var timeoutSessions = _sessionMap.Values
            .Where(s => s.IsConnected && (now - s.LastActiveTime).TotalSeconds > HeartbeatTimeoutSeconds)
            .ToList();

        foreach (var session in timeoutSessions)
        {
            Log.Warn($"心跳超时 SessionId:{session.SessionId} UserId:{session.UserId}");
            Kick(session, LogoutReason.HeartbeatTimeout);
        }

        if (timeoutSessions.Count > 0)
        {
            Log.Info($"心跳检测: 踢出 {timeoutSessions.Count} 个超时连接，当前在线: {_sessionMap.Count}");
        }
    }
}
