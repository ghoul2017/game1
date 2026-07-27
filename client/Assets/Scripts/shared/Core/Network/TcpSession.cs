using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Shared.Common.Enums;
using Shared.Core.Network;
using Shared.Core.Log;
using ProtoBuf;

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
        private readonly object _rpcLock = new();
        private readonly object _waitLock = new();
        private readonly Dictionary<long, PendingRpcCall> _pendingRpcCalls = new();
        private readonly Dictionary<string, List<PendingMessageWaiter>> _messageWaiters = new();
        private long _nextRpcId;

        private const string RpcRequestMessageName = "Network.RpcRequest";
        private const string RpcResponseMessageName = "Network.RpcResponse";

        public long SessionId { get; }
        public bool IsConnected { get; private set; }
        public string RemoteIp { get; }

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
        
        public TcpSession(Socket socket, MessageRouter router)
        {
            _socket = socket;
            _router = router;
            SessionId = IdGenerator.Instance.NextId();
            RemoteIp = (socket.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? string.Empty;
            IsConnected = true;
        }

        public void StartReceive()
        {
            _ = ReceiveLoopAsync();
        }

        public void Send(IMessage msg)
        {
            var messageName = MessageRegistry.GetName(msg.GetType());
            Send(messageName, msg.Serialize());
        }

        public Task<TRes> CallAsync<TRes>(IMessage req, int timeoutMs = 5000) where TRes : IMessage
        {
            if (req == null)
                throw new ArgumentNullException(nameof(req));
            if (!IsConnected)
                throw new InvalidOperationException("连接已断开");

            var reqMessageName = MessageRegistry.GetName(req.GetType());
            var resMessageName = MessageRegistry.GetName(typeof(TRes));
            var rpcId = Interlocked.Increment(ref _nextRpcId);
            var tcs = new TaskCompletionSource<TRes>();
            var pending = new PendingRpcCall<TRes>(rpcId, resMessageName, tcs);

            lock (_rpcLock)
            {
                _pendingRpcCalls[rpcId] = pending;
            }

            if (timeoutMs > 0)
            {
                _ = Task.Delay(timeoutMs).ContinueWith(_ => TimeoutRpcCall(rpcId, timeoutMs));
            }

            Send(new RpcRequest
            {
                RpcId = rpcId,
                MessageName = reqMessageName,
                Payload = req.Serialize()
            });

            return tcs.Task;
        }

        public Task<T> WaitForAsync<T>(int timeoutMs = 5000) where T : IMessage
        {
            if (!IsConnected)
                throw new InvalidOperationException("连接已断开");

            var messageName = MessageRegistry.GetName(typeof(T));
            var tcs = new TaskCompletionSource<T>();
            var waiter = new PendingMessageWaiter<T>(messageName, tcs);

            lock (_waitLock)
            {
                if (!_messageWaiters.TryGetValue(messageName, out var waiters))
                {
                    waiters = new List<PendingMessageWaiter>();
                    _messageWaiters[messageName] = waiters;
                }

                waiters.Add(waiter);
            }

            if (timeoutMs > 0)
            {
                _ = Task.Delay(timeoutMs).ContinueWith(_ => TimeoutMessageWaiter(waiter, timeoutMs));
            }

            return tcs.Task;
        }

        public void Send(string messageName, byte[] payload)
        {
            if (!IsConnected)
                return;

            var packet = PacketCodec.Encode(messageName, payload);
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
            if (!IsConnected)
                return;
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
            FailPendingOperations(reason);
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

                var messageName = result.MessageName;
                var payload = result.Payload;
                if (PostToMainThread != null)
                {
                    PostToMainThread(() => { _ = HandlePacketAsync(messageName, payload); });
                }
                else
                {
                    _ = HandlePacketAsync(messageName, payload);
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

        private async Task HandlePacketAsync(string messageName, byte[] payload)
        {
            if (messageName == RpcResponseMessageName)
            {
                HandleRpcResponse(payload);
                return;
            }

            if (messageName == RpcRequestMessageName)
            {
                await HandleRpcRequestAsync(payload);
                return;
            }

            CompleteMessageWaiter(messageName, payload);
            await _router.RouteAsync(this, messageName, payload);
        }

        private async Task HandleRpcRequestAsync(byte[] payload)
        {
            RpcRequest req;
            try
            {
                using var ms = new MemoryStream(payload);
                req = Serializer.Deserialize<RpcRequest>(ms);
            }
            catch (Exception ex)
            {
                Log.Error($"RPC请求反序列化失败: {ex.Message}");
                return;
            }

            var rpcRes = new RpcResponse
            {
                RpcId = req.RpcId
            };

            try
            {
                if (string.IsNullOrWhiteSpace(req.MessageName))
                    throw new InvalidDataException("RPC请求消息名不能为空");
                if (!_router.HasHandler(req.MessageName))
                    throw new InvalidOperationException($"未注册的消息: {req.MessageName}");

                var res = await _router.HandleAsync(this, req.MessageName, req.Payload);
                if (res == null)
                    throw new InvalidOperationException($"RPC请求没有响应: {req.MessageName}");

                rpcRes.MessageName = MessageRegistry.GetName(res.GetType());
                rpcRes.Payload = res.Serialize();
            }
            catch (Exception ex)
            {
                Log.Error($"处理RPC请求 {req.MessageName} 异常", ex);
                rpcRes.MessageName = "";
                rpcRes.Payload = new byte[0];
                rpcRes.ErrorMessage = ex.Message;
            }

            Send(rpcRes);
        }

        private void HandleRpcResponse(byte[] payload)
        {
            RpcResponse res;
            try
            {
                using var ms = new MemoryStream(payload);
                res = Serializer.Deserialize<RpcResponse>(ms);
            }
            catch (Exception ex)
            {
                Log.Error($"RPC响应反序列化失败: {ex.Message}");
                return;
            }

            PendingRpcCall? pending = null;
            lock (_rpcLock)
            {
                if (_pendingRpcCalls.TryGetValue(res.RpcId, out pending))
                    _pendingRpcCalls.Remove(res.RpcId);
            }

            if (pending == null)
            {
                Log.Warn($"未找到RPC等待者: RpcId={res.RpcId}");
                return;
            }

            pending.SetResult(res);
        }

        private void CompleteMessageWaiter(string messageName, byte[] payload)
        {
            PendingMessageWaiter? waiter = null;
            lock (_waitLock)
            {
                if (_messageWaiters.TryGetValue(messageName, out var waiters) && waiters.Count > 0)
                {
                    waiter = waiters[0];
                    waiters.RemoveAt(0);
                    if (waiters.Count == 0)
                        _messageWaiters.Remove(messageName);
                }
            }

            waiter?.SetResult(payload);
        }

        private void TimeoutRpcCall(long rpcId, int timeoutMs)
        {
            PendingRpcCall? pending = null;
            lock (_rpcLock)
            {
                if (_pendingRpcCalls.TryGetValue(rpcId, out pending))
                    _pendingRpcCalls.Remove(rpcId);
            }

            pending?.SetException(new TimeoutException($"RPC等待超时: RpcId={rpcId}, Timeout={timeoutMs}ms"));
        }

        private void TimeoutMessageWaiter(PendingMessageWaiter waiter, int timeoutMs)
        {
            var removed = false;
            lock (_waitLock)
            {
                if (_messageWaiters.TryGetValue(waiter.MessageName, out var waiters))
                {
                    removed = waiters.Remove(waiter);
                    if (waiters.Count == 0)
                        _messageWaiters.Remove(waiter.MessageName);
                }
            }

            if (removed)
                waiter.SetException(new TimeoutException($"等待消息超时: {waiter.MessageName}, Timeout={timeoutMs}ms"));
        }

        private void FailPendingOperations(LogoutReason reason)
        {
            List<PendingRpcCall> rpcCalls;
            List<PendingMessageWaiter> waiters;

            lock (_rpcLock)
            {
                rpcCalls = new List<PendingRpcCall>(_pendingRpcCalls.Values);
                _pendingRpcCalls.Clear();
            }

            lock (_waitLock)
            {
                waiters = new List<PendingMessageWaiter>();
                foreach (var item in _messageWaiters.Values)
                    waiters.AddRange(item);
                _messageWaiters.Clear();
            }

            var ex = new InvalidOperationException($"连接已断开: {reason}");
            foreach (var pending in rpcCalls)
                pending.SetException(ex);
            foreach (var waiter in waiters)
                waiter.SetException(ex);
        }

        private abstract class PendingRpcCall
        {
            public long RpcId { get; }
            protected string ExpectedMessageName { get; }

            protected PendingRpcCall(long rpcId, string expectedMessageName)
            {
                RpcId = rpcId;
                ExpectedMessageName = expectedMessageName;
            }

            public abstract void SetResult(RpcResponse res);
            public abstract void SetException(Exception ex);
        }

        private class PendingRpcCall<TRes> : PendingRpcCall where TRes : IMessage
        {
            private readonly TaskCompletionSource<TRes> _tcs;

            public PendingRpcCall(long rpcId, string expectedMessageName, TaskCompletionSource<TRes> tcs)
                : base(rpcId, expectedMessageName)
            {
                _tcs = tcs;
            }

            public override void SetResult(RpcResponse res)
            {
                if (!string.IsNullOrEmpty(res.ErrorMessage))
                {
                    _tcs.TrySetException(new InvalidOperationException(res.ErrorMessage));
                    return;
                }

                if (res.MessageName != ExpectedMessageName)
                {
                    _tcs.TrySetException(new InvalidOperationException($"RPC响应类型不匹配: 期望={ExpectedMessageName}, 实际={res.MessageName}"));
                    return;
                }

                try
                {
                    using var ms = new MemoryStream(res.Payload);
                    var msg = Serializer.Deserialize<TRes>(ms);
                    _tcs.TrySetResult(msg);
                }
                catch (Exception ex)
                {
                    _tcs.TrySetException(ex);
                }
            }

            public override void SetException(Exception ex)
            {
                _tcs.TrySetException(ex);
            }
        }

        private abstract class PendingMessageWaiter
        {
            public string MessageName { get; }

            protected PendingMessageWaiter(string messageName)
            {
                MessageName = messageName;
            }

            public abstract void SetResult(byte[] payload);
            public abstract void SetException(Exception ex);
        }

        private class PendingMessageWaiter<T> : PendingMessageWaiter where T : IMessage
        {
            private readonly TaskCompletionSource<T> _tcs;

            public PendingMessageWaiter(string messageName, TaskCompletionSource<T> tcs)
                : base(messageName)
            {
                _tcs = tcs;
            }

            public override void SetResult(byte[] payload)
            {
                try
                {
                    using var ms = new MemoryStream(payload);
                    var msg = Serializer.Deserialize<T>(ms);
                    _tcs.TrySetResult(msg);
                }
                catch (Exception ex)
                {
                    _tcs.TrySetException(ex);
                }
            }

            public override void SetException(Exception ex)
            {
                _tcs.TrySetException(ex);
            }
        }
    }
}
