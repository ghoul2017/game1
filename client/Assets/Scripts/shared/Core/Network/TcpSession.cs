using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly IByteTransport _transport;
        private readonly MessageRouter _router;
        private readonly MemoryStream _packetBuffer = new();
        private readonly object _packetBufferLock = new();
        private readonly object _businessQueueLock = new();
        private readonly object _rpcLock = new();
        private readonly object _waitLock = new();
        private readonly Queue<Func<Task>> _businessQueue = new();
        private readonly Dictionary<long, PendingRpcCall> _pendingRpcCalls = new();
        private readonly Dictionary<string, List<PendingMessageWaiter>> _messageWaiters = new();
        private bool _businessQueueRunning;
        private int _disconnectStarted;
        private int _disposed;
        private long _nextRpcId;

        private const string RpcRequestMessageName = "Network.RpcRequest";
        private const string RpcResponseMessageName = "Network.RpcResponse";

        public long SessionId { get; }
        public bool IsConnected => Volatile.Read(ref _disconnectStarted) == 0 && _transport.IsConnected;
        public string RemoteIp { get; }

        public long SentBytes => _transport.BytesSent;

        public long ReceivedBytes => _transport.BytesReceivedCount;

        public int QueuedSendBytes => _transport.QueuedBytes;

        public int PeakQueuedSendBytes => _transport.PeakQueuedBytes;

        public long ReplacedSnapshotCount => _transport.ReplacedSnapshotCount;

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

        /// <summary>可选的调度器，用于将消息处理投递到客户端帧线程或服务端游戏主线程。</summary>
        public Action<Action>? PostToMainThread { get; set; }

        public TcpSession(Socket socket, MessageRouter router)
            : this(new SocketByteTransport(socket), router)
        {
        }

        public TcpSession(IByteTransport transport, MessageRouter router)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            SessionId = IdGenerator.Instance.NextId();
            RemoteIp = transport.RemoteIp;
            _transport.BytesReceived += OnBytesReceived;
            _transport.Disconnected += OnTransportDisconnected;
        }

        public void StartReceive()
        {
            _transport.StartReceive();
        }

        public void Send(IMessage msg)
        {
            string messageName = MessageRegistry.GetName(msg.GetType());
            Send(messageName, msg.Serialize());
        }

        /// <summary>
        /// 发送可被更新状态覆盖的普通快照；可靠结果和语义事件仍必须使用 Send。
        /// </summary>
        public void SendLatest(IMessage snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            string messageName = MessageRegistry.GetName(snapshot.GetType());
            Send(messageName, snapshot.Serialize(), TransportSendMode.LatestSnapshot);
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
            TaskCompletionSource<TRes> tcs = new TaskCompletionSource<TRes>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            PendingRpcCall<TRes> pending = new PendingRpcCall<TRes>(rpcId, resMessageName, tcs);

            lock (_rpcLock)
            {
                _pendingRpcCalls[rpcId] = pending;
            }

            if (!IsConnected)
            {
                bool removed;
                lock (_rpcLock)
                {
                    removed = _pendingRpcCalls.Remove(rpcId);
                }

                if (removed)
                {
                    pending.SetException(new InvalidOperationException("连接已断开"));
                }

                return tcs.Task;
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

            string messageName = MessageRegistry.GetName(typeof(T));
            TaskCompletionSource<T> tcs = new TaskCompletionSource<T>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            PendingMessageWaiter<T> waiter = new PendingMessageWaiter<T>(messageName, tcs);

            lock (_waitLock)
            {
                if (!_messageWaiters.TryGetValue(messageName, out var waiters))
                {
                    waiters = new List<PendingMessageWaiter>();
                    _messageWaiters[messageName] = waiters;
                }

                waiters.Add(waiter);
            }

            if (!IsConnected)
            {
                bool removed;
                lock (_waitLock)
                {
                    removed = RemoveMessageWaiter(waiter);
                }

                if (removed)
                {
                    waiter.SetException(new InvalidOperationException("连接已断开"));
                }

                return tcs.Task;
            }

            if (timeoutMs > 0)
            {
                _ = Task.Delay(timeoutMs).ContinueWith(_ => TimeoutMessageWaiter(waiter, timeoutMs));
            }

            return tcs.Task;
        }

        public void Send(string messageName, byte[] payload)
        {
            Send(messageName, payload, TransportSendMode.Reliable);
        }

        private void Send(string messageName, byte[] payload, TransportSendMode mode)
        {
            if (!IsConnected)
            {
                return;
            }

            byte[] packet = PacketCodec.Encode(messageName, payload);
            try
            {
                _transport.Send(packet, mode);
            }
            catch (Exception exception)
            {
                Log.Error($"[TcpSession] 发送失败: {exception.Message}");
                Disconnect();
            }
        }

        public void Disconnect(LogoutReason reason = LogoutReason.NetDisconnected)
        {
            _transport.Disconnect(reason);
            CompleteDisconnect(reason);
        }

        /// <summary>更新最后活跃时间（收到消息时调用）</summary>
        public void UpdateActiveTime()
        {
            LastActiveTime = DateTime.UtcNow;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _transport.BytesReceived -= OnBytesReceived;
            _transport.Disconnected -= OnTransportDisconnected;
            _transport.Disconnect(LogoutReason.NetDisconnected);
            CompleteDisconnect(LogoutReason.NetDisconnected);
            _transport.Dispose();
            lock (_packetBufferLock)
            {
                _packetBuffer.Dispose();
            }
        }

        private void OnBytesReceived(byte[] bytes)
        {
            if (Volatile.Read(ref _disposed) != 0 || Volatile.Read(ref _disconnectStarted) != 0)
            {
                return;
            }

            lock (_packetBufferLock)
            {
                if (Volatile.Read(ref _disposed) != 0 || Volatile.Read(ref _disconnectStarted) != 0)
                {
                    return;
                }

                _packetBuffer.Write(bytes, 0, bytes.Length);
                ProcessBuffer();
            }
        }

        private void OnTransportDisconnected(LogoutReason reason)
        {
            CompleteDisconnect(reason);
        }

        private void CompleteDisconnect(LogoutReason reason)
        {
            lock (_packetBufferLock)
            {
                if (Interlocked.Exchange(ref _disconnectStarted, 1) != 0)
                {
                    return;
                }

                DisconnectReason = reason;
                Log.Info($"断开连接 SessionId:{SessionId} UserId:{UserId} Reason:{reason}");

                // 基础设施等待必须立即失败，才能解除当前 Handler 内部正在等待的 CallAsync。
                FailPendingOperations(reason);

                // 业务清理与已经解包的业务消息共用同一队列，确保不会越过在途 Handler。
                EnqueueBusinessOperation(() =>
                {
                    OnDisconnected?.Invoke(this, reason);
                    return Task.CompletedTask;
                }, true);
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

                string messageName = result.MessageName;
                byte[] payload = result.Payload;
                if (messageName == RpcResponseMessageName)
                {
                    // RPC 响应属于基础设施完成信号，不能排在业务队列后，否则当前 Handler
                    // await CallAsync 时会等待一个永远无法开始处理的响应。
                    HandleRpcResponse(payload);
                }
                else
                {
                    EnqueueBusinessOperation(() => HandleBusinessPacketAsync(messageName, payload));
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

        private void EnqueueBusinessOperation(Func<Task> operation, bool allowAfterDisconnect = false)
        {
            bool startQueue = false;
            lock (_businessQueueLock)
            {
                if (!allowAfterDisconnect && Volatile.Read(ref _disconnectStarted) != 0)
                {
                    return;
                }

                _businessQueue.Enqueue(operation);
                if (!_businessQueueRunning)
                {
                    _businessQueueRunning = true;
                    startQueue = true;
                }
            }

            if (!startQueue)
            {
                return;
            }

            Action start = () => { _ = ProcessBusinessQueueAsync(); };
            if (PostToMainThread != null)
            {
                PostToMainThread(start);
                return;
            }

            start();
        }

        private async Task ProcessBusinessQueueAsync()
        {
            while (true)
            {
                Func<Task> operation;
                lock (_businessQueueLock)
                {
                    if (_businessQueue.Count == 0)
                    {
                        _businessQueueRunning = false;
                        return;
                    }

                    operation = _businessQueue.Dequeue();
                }

                try
                {
                    await operation();
                }
                catch (Exception exception)
                {
                    Log.Error($"[TcpSession] 业务消息队列执行异常 SessionId:{SessionId}", exception);
                }
            }
        }

        private async Task HandleBusinessPacketAsync(string messageName, byte[] payload)
        {
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
                removed = RemoveMessageWaiter(waiter);
            }

            if (removed)
                waiter.SetException(new TimeoutException($"等待消息超时: {waiter.MessageName}, Timeout={timeoutMs}ms"));
        }

        private bool RemoveMessageWaiter(PendingMessageWaiter waiter)
        {
            if (!_messageWaiters.TryGetValue(waiter.MessageName, out List<PendingMessageWaiter>? waiters))
            {
                return false;
            }

            bool removed = waiters.Remove(waiter);
            if (waiters.Count == 0)
            {
                _messageWaiters.Remove(waiter.MessageName);
            }

            return removed;
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
                    _tcs.TrySetException(new InvalidOperationException(
                        $"RPC响应类型不匹配: 期望={ExpectedMessageName}, 实际={res.MessageName}"));
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
