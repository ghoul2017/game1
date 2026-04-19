using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Game.Core;
using Game.Game.Player;
using Shared.Core.Log;
using Game.game.user;

namespace Game.Network;

public class TcpServer
{
    private static readonly ILog Log = LogManager.GetLogger<TcpServer>();
    private readonly ServerOptions _options;
    private readonly MessageRouter _router;
    private Socket? _listenSocket;
    private CancellationTokenSource? _cts;

    public TcpServer(ServerOptions options, MessageRouter router)
    {
        _options = options;
        _router = router;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _cts.Token;

        var endpoint = new IPEndPoint(IPAddress.Parse(_options.Host), _options.Port);
        _listenSocket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        _listenSocket.Bind(endpoint);
        _listenSocket.Listen(_options.MaxConnections);

        Log.Info($"监听 {_options.Host}:{_options.Port}");

        try
        {
            while (!token.IsCancellationRequested)
            {
                var clientSocket = await _listenSocket.AcceptAsync(token);
                var session = new TcpSession(clientSocket, _router);

                if (SessionManager.Instance.OnlineCount >= _options.MaxConnections)
                {
                    session.Disconnect();
                    Log.Warn("达到最大连接数，拒绝新连接");
                    continue;
                }

                SessionManager.Instance.Add(session);
                session.OnDisconnected += OnSessionDisconnected;
                session.StartReceive();
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            Log.Info("正在关闭...");
            SessionManager.Instance.KickAll(LogoutReason.ServerClose);
            _listenSocket?.Close();
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
    }

    private void OnSessionDisconnected(TcpSession session)
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

        SessionManager.Instance.Remove(session);
        session.OnDisconnected -= OnSessionDisconnected;
        session.Dispose();
    }
}
