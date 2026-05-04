using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Game.Components.User;
using Game.Game.Player;
using Game.Network;
using Shared.Core.ECS;
using Shared.Core.Log;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Core.Network;

/// <summary>
/// 网络组件。内聚服务端监听、客户端连接、会话管理、RPC 调用能力。
/// </summary>
[ComponentOf(typeof(Entities.Module))]
public class NetworkComponent : Component
{
    private static readonly ILog Log = LogManager.GetLogger<NetworkComponent>();

    // ==================== 配置 ====================
    private readonly MessageRouter _router;
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 9000;
    public int MaxConnections { get; set; } = 5000;

    private CancellationTokenSource? _cts;

    // ==================== 服务端 ====================
    private Socket? _listenSocket;
    private readonly Dictionary<long, TcpSession> _sessionMap = new(); // sessionId -> session

    /// <summary>心跳超时时间（秒），未登录会话超过此时间将被踢出</summary>
    private const int HeartbeatTimeoutSeconds = 60;

    /// <summary>心跳检测间隔（秒）</summary>
    private const int CheckIntervalSeconds = 20;

    private readonly System.Timers.Timer _heartbeatTimer;

    public NetworkComponent()
    {
        _router = new MessageRouter();
        _router.AutoRegister();
        _heartbeatTimer = new System.Timers.Timer(CheckIntervalSeconds * 1000);
        _heartbeatTimer.Elapsed += OnHeartbeatCheck;
        _heartbeatTimer.AutoReset = true;
        _heartbeatTimer.Start();
    }

    public async Task Listen(string host, int port, CancellationToken cancellationToken = default,
        int maxConnections = 5000)
    {
        Host = host;
        Port = port;
        MaxConnections = maxConnections;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);


        var endpoint = new IPEndPoint(IPAddress.Parse(Host), Port);
        _listenSocket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        _listenSocket.Bind(endpoint);
        _listenSocket.Listen(MaxConnections);

        Log.Info($"监听 {Host}:{Port}");

        await AcceptAsync(_listenSocket, _cts.Token);
    }

    private async Task AcceptAsync(Socket listenSocket, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var clientSocket = await listenSocket.AcceptAsync(token);
                var session = new TcpSession(clientSocket, _router);

                if (_sessionMap.Count >= MaxConnections)
                {
                    session.Disconnect();
                    Log.Warn("达到最大连接数，拒绝新连接");
                    continue;
                }

                Add(session);
                session.OnDisconnected += OnSessionDisconnected;
                session.StartReceive();
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
        // 断线回调可能来自非主线程，投递到游戏主线程执行
        var gameCtx = SynchronizationContext.Current as GameSynchronizationContext;
        if (gameCtx != null && !GameSynchronizationContext.IsMainThread)
        {
            gameCtx.Post(_ => HandleDisconnection(session), null);
            return;
        }
        HandleDisconnection(session);
    }

    private void HandleDisconnection(TcpSession session)
    {
        // 如果已选角，从在线列表移除并保存数据
        if (session.RoleId != 0)
        {
            var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
            if (player != null)
            {
                _ = PlayerManager.Instance.SaveRoleAsync(player);
                PlayerManager.Instance.RemovePlayer(session.RoleId);
            }
        }

        Remove(session);
        session.OnDisconnected -= OnSessionDisconnected;
        session.Dispose();
    }

    /// <summary>添加会话（连接建立时调用）</summary>
    public void Add(TcpSession session)
    {
        _sessionMap[session.SessionId] = session;
        Log.Info($"会话添加 SessionId:{session.SessionId}，当前连接数: {_sessionMap.Count}");
    }

    /// <summary>添加会话（连接建立时调用）</summary>
    public TcpSession? Get(long sessionId)
    {
        return _sessionMap.GetValueOrDefault(sessionId);
    }

    /// <summary>移除会话（连接断开时调用）</summary>
    public void Remove(TcpSession session)
    {
        _sessionMap.Remove(session.SessionId);
        Log.Info($"会话移除 SessionId:{session.SessionId}，当前连接数: {_sessionMap.Count}");
    }

    /// <summary>踢人（发送断线通知后断开连接）</summary>
    public void Kick(TcpSession session, LogoutReason reason)
    {
        Log.Info($"踢人 SessionId:{session.SessionId} UserId:{session.UserId} Reason:{reason}");

        // 发送踢人通知
        try
        {
            var ntf = new KickNtf { Reason = (int)reason };
            session.Send(ntf);
        }
        catch (Exception e)
        {
            Log.Error("踢人失败", e);
        }

        // 移除映射并断开连接
        Remove(session);
        session.Disconnect();
    }

    /// <summary>服务器关闭时踢出所有连接</summary>
    public void KickAll(LogoutReason reason)
    {
        List<TcpSession> sessions = new List<TcpSession>(_sessionMap.Values);
        _sessionMap.Clear();

        foreach (var session in sessions)
        {
            session.Disconnect();
        }

        Log.Info($"全部断开，共 {sessions.Count} 个连接");
    }

    private void OnHeartbeatCheck(object? sender, ElapsedEventArgs e)
    {
        // Timer 回调在非主线程，投递到游戏主线程执行
        var gameCtx = SynchronizationContext.Current as GameSynchronizationContext;
        if (gameCtx != null)
        {
            gameCtx.Post(_ => DoHeartbeatCheck(), null);
        }
        else
        {
            DoHeartbeatCheck();
        }
    }

    private void DoHeartbeatCheck()
    {
        var now = DateTime.UtcNow;
        List<TcpSession> timeoutSessions = _sessionMap.Values
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