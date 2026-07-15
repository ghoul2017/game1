using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Shared.Core;
using Shared.Common.Enums;
using Shared.Core.ECS;
using Shared.Core.Log;

#nullable enable
namespace Shared.Core.Network
{
    /// <summary>
    /// 网络组件。管理 TCP 连接，不持有业务级会话映射。
    /// 心跳检测由外部定时调用 DoHeartbeatCheck 驱动。
    /// session 引用由各业务对象（User、PlayerOnlineComponent）自行持有。
    /// 通过 SessionDisconnected 回调通知上层清理 session 引用。
    /// </summary>
    [ComponentOf(typeof(Shared.Core.Entities.Module))]
    public class NetworkComponent : Component
    {
        private static readonly ILog Log = LogManager.GetLogger<NetworkComponent>();

        private readonly MessageRouter _router;
        public string Host { get; set; } = "0.0.0.0";
        public int Port { get; set; } = 9000;
        public int MaxConnections { get; set; } = 5000;

        private Socket? _listenSocket;
        private readonly Dictionary<long, TcpSession> _sessionMap = new(); // sessionId 到 session

        private const int HeartbeatTimeoutSeconds = 60;

        /// <summary>会话断开回调。上层注册此回调清理业务对象上的 session 引用。</summary>
        public Action<TcpSession, LogoutReason>? SessionDisconnected { get; set; }

        public NetworkComponent()
        {
            _router = new MessageRouter();
            _router.AutoRegister();
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
                    Socket clientSocket;
                    using (ShutdownToken.Instance.Token.Register(() => listenSocket.Close()))
                    {
                        clientSocket = await Task.Factory.FromAsync(
                            (callback, state) => listenSocket.BeginAccept(callback, state),
                            listenSocket.EndAccept,
                            null);
                    }
                    var session = new TcpSession(clientSocket, _router);

                    if (_sessionMap.Count >= MaxConnections)
                    {
                        session.Disconnect(LogoutReason.ServerClose);
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
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException) when (ShutdownToken.Instance.IsCancellationRequested)
            {
            }
            finally
            {
                Log.Info("正在关闭...");
                KickAll(LogoutReason.ServerClose);
                _listenSocket?.Close();
            }
        }

        private void OnSessionDisconnected(TcpSession session, LogoutReason reason)
        {
            // 通知上层清理业务对象上的 session 引用
            SessionDisconnected?.Invoke(session, reason);

            _sessionMap.Remove(session.SessionId);
            session.OnDisconnected -= OnSessionDisconnected;
            session.Dispose();

            Log.Info($"会话断开 SessionId:{session.SessionId} Reason:{reason}，当前连接数: {_sessionMap.Count}");
        }

        /// <summary>按 sessionId 获取</summary>
        public TcpSession? Get(long sessionId)
        {
            return _sessionMap.GetValueOrDefault(sessionId);
        }

        /// <summary>客户端模式：主动连接远端服务器</summary>
        public async Task<TcpSession> ConnectAsync(string host, int port)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await Task.Factory.FromAsync(
                (callback, state) => socket.BeginConnect(host, port, callback, state),
                socket.EndConnect,
                null);
            var session = new TcpSession(socket, _router);
            _sessionMap[session.SessionId] = session;
            session.OnDisconnected += OnSessionDisconnected;
            session.StartReceive();

            Log.Info($"客户端连接 {host}:{port} SessionId:{session.SessionId}，当前连接数: {_sessionMap.Count}");
            return session;
        }

        /// <summary>服务器关闭时踢出所有连接</summary>
        public void KickAll(LogoutReason reason)
        {
            var sessions = new List<TcpSession>(_sessionMap.Values);
            foreach (var session in sessions)
            {
                session.Disconnect(reason);
            }

            Log.Info($"全部断开，共 {sessions.Count} 个连接");
        }

        /// <summary>心跳检测（由外部定时调用）</summary>
        public void DoHeartbeatCheck()
        {
            var now = DateTime.UtcNow;
            var timeoutSessions = _sessionMap.Values
                .Where(s => s.IsConnected && (now - s.LastActiveTime).TotalSeconds > HeartbeatTimeoutSeconds)
                .ToList();

            foreach (var session in timeoutSessions)
            {
                Log.Warn($"心跳超时 SessionId:{session.SessionId} UserId:{session.UserId}");
                session.Disconnect(LogoutReason.HeartbeatTimeout);
            }

            if (timeoutSessions.Count > 0)
            {
                Log.Info($"心跳检测: 踢出 {timeoutSessions.Count} 个超时连接，当前在线: {_sessionMap.Count}");
            }
        }
    }
}
