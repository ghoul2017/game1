using System;
using System.Threading.Tasks;
using Shared.Common.Enums;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Robot
{
    /// <summary>
    /// Robot 客户端会话。通过 NetworkComponent 创建 TcpSession，复用共享网络层。
    /// </summary>
    public class RobotSession : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger<RobotSession>();

        private NetworkComponent? _network;
        private TcpSession? _session;
        private string? _host;
        private int _port;

        public bool IsConnected => _session != null && _session.IsConnected;

        public string Host => _host ?? throw new InvalidOperationException("尚未连接过服务端");

        public int Port => _port;

        public long UserId
        {
            get => CurrentSession.UserId;
            set => CurrentSession.UserId = value;
        }

        public long RoleId
        {
            get => CurrentSession.RoleId;
            set => CurrentSession.RoleId = value;
        }

        private TcpSession CurrentSession
        {
            get
            {
                if (_session == null)
                    throw new InvalidOperationException("未连接");
                return _session;
            }
        }

        /// <summary>连接服务器</summary>
        public async Task ConnectAsync(string host, int port)
        {
            _host = host;
            _port = port;
            _network = new NetworkComponent();
            _session = await _network.ConnectAsync(host, port);

            Log.Info($"Robot 连接 {host}:{port}");
        }

        /// <summary>重新连接上一次使用的服务端</summary>
        public Task ReconnectAsync()
        {
            if (_host == null)
                throw new InvalidOperationException("尚未连接过服务端");
            return ConnectAsync(_host, _port);
        }

        /// <summary>发送消息，不等待响应</summary>
        public Task SendAsync(IMessage msg)
        {
            CurrentSession.Send(msg);
            return Task.CompletedTask;
        }

        /// <summary>发送请求并等待指定类型响应</summary>
        public Task<T> CallAsync<T>(IMessage req, int timeoutMs = 5000) where T : IMessage
        {
            return CurrentSession.CallAsync<T>(req, timeoutMs);
        }

        /// <summary>兼容旧测试命名：发送请求并等待指定类型响应</summary>
        public Task<T> SendAndWaitAsync<T>(IMessage req, int timeoutMs = 5000) where T : IMessage
        {
            return CallAsync<T>(req, timeoutMs);
        }

        /// <summary>等待指定类型的服务端推送或裸响应</summary>
        public Task<T> WaitForAsync<T>(int timeoutMs = 5000) where T : IMessage
        {
            return CurrentSession.WaitForAsync<T>(timeoutMs);
        }

        /// <summary>兼容旧测试命名：等待指定类型的消息</summary>
        public Task<T> WaitForMsgAsync<T>(int timeoutMs = 5000) where T : IMessage
        {
            return WaitForAsync<T>(timeoutMs);
        }

        /// <summary>断开连接</summary>
        public void Disconnect()
        {
            if (_session == null)
                return;

            _session.Disconnect(LogoutReason.NetDisconnected);
            _session = null;
            _network = null;

            Log.Info("Robot 断开连接");
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
