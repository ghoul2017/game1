using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ProtoBuf;
using Shared.Common.Enums;
using Shared.Common.Protos;
using Shared.Core.Network;
using Xunit;

namespace Shared.Tests
{
    public sealed class TcpSessionTransportTests
    {
        [Fact]
        public void ExpeditionDirectionQuantizationNeverExceedsProtocolCircle()
        {
            ExpeditionProtocolConstants.QuantizeDirection(
                -1.1f,
                4.9f,
                out int quantizedX,
                out int quantizedY);

            long lengthSquared = ((long)quantizedX * quantizedX) + ((long)quantizedY * quantizedY);
            long maximumLengthSquared =
                (long)ExpeditionProtocolConstants.DirectionScale * ExpeditionProtocolConstants.DirectionScale;
            Assert.True(lengthSquared <= maximumLengthSquared);
            Assert.NotEqual(0, quantizedX);
            Assert.NotEqual(0, quantizedY);

            ExpeditionProtocolConstants.QuantizeDirection(1.0f, 0.0f, out quantizedX, out quantizedY);
            Assert.Equal(ExpeditionProtocolConstants.DirectionScale, quantizedX);
            Assert.Equal(0, quantizedY);
        }

        [Fact]
        public void ReliableAndLatestMessagesUseDifferentQueueModes()
        {
            FakeByteTransport transport = new FakeByteTransport();
            using TcpSession session = new TcpSession(transport, new MessageRouter());
            HeartbeatReq message = new HeartbeatReq { ClientTime = 123L };

            session.Send(message);
            session.SendLatest(message);

            Assert.Equal(2, transport.Sends.Count);
            Assert.Equal(TransportSendMode.Reliable, transport.Sends[0].Mode);
            Assert.Equal(TransportSendMode.LatestSnapshot, transport.Sends[1].Mode);
            PacketCodec.DecodeResult? decoded = PacketCodec.TryDecode(
                transport.Sends[1].Bytes,
                0,
                transport.Sends[1].Bytes.Length);
            Assert.NotNull(decoded);
            Assert.Equal("Misc.HeartbeatReq", decoded!.MessageName);
        }

        [Fact]
        public async Task FragmentedReceiveCompletesWaiterOnlyAfterWholePacket()
        {
            FakeByteTransport transport = new FakeByteTransport();
            using TcpSession session = new TcpSession(transport, new MessageRouter());
            Task<HeartbeatReq> waiter = session.WaitForAsync<HeartbeatReq>(1000);
            HeartbeatReq expected = new HeartbeatReq { ClientTime = 456L };
            byte[] packet = PacketCodec.Encode("Misc.HeartbeatReq", expected.Serialize());
            int split = packet.Length / 2;

            transport.Emit(packet, 0, split);
            Assert.False(waiter.IsCompleted);
            transport.Emit(packet, split, packet.Length - split);

            HeartbeatReq actual = await waiter;
            Assert.Equal(expected.ClientTime, actual.ClientTime);
        }

        [Fact]
        public async Task DisconnectFailsPendingRpcExactlyOnce()
        {
            FakeByteTransport transport = new FakeByteTransport();
            using TcpSession session = new TcpSession(transport, new MessageRouter());
            Task<HeartbeatRes> pending = session.CallAsync<HeartbeatRes>(new HeartbeatReq(), 0);
            int disconnectEvents = 0;
            session.OnDisconnected += (_, _) => disconnectEvents++;

            transport.Disconnect(LogoutReason.NetDisconnected);
            transport.Disconnect(LogoutReason.NetDisconnected);

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending);
            Assert.Equal(1, disconnectEvents);
        }

        [Fact]
        public void BytesArrivingAfterDisposeAreIgnored()
        {
            FakeByteTransport transport = new FakeByteTransport();
            TcpSession session = new TcpSession(transport, new MessageRouter());
            session.Dispose();

            Exception? exception = Record.Exception(() => transport.Emit(new byte[] { 1, 2, 3 }, 0, 3));

            Assert.Null(exception);
        }

        [Fact]
        public async Task RpcRegisteredDuringDisconnectFailsWithoutWaitingForTimeout()
        {
            DisconnectDuringConnectionCheckTransport transport = new DisconnectDuringConnectionCheckTransport();
            using TcpSession session = new TcpSession(transport, new MessageRouter());

            Task<HeartbeatRes> pending = session.CallAsync<HeartbeatRes>(new HeartbeatReq(), 0);

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending);
            Assert.Equal(0, transport.SendCount);
        }

        [Fact]
        public async Task WaiterRegisteredDuringDisconnectFailsWithoutWaitingForTimeout()
        {
            DisconnectDuringConnectionCheckTransport transport = new DisconnectDuringConnectionCheckTransport();
            using TcpSession session = new TcpSession(transport, new MessageRouter());

            Task<HeartbeatReq> pending = session.WaitForAsync<HeartbeatReq>(0);

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending);
        }

        [Fact]
        public void NetworkComponentDispatchesDisconnectBeforeMutatingSessionState()
        {
            Queue<Action> dispatchedActions = new Queue<Action>();
            NetworkComponent network = new NetworkComponent
            {
                SessionDispatcher = action => dispatchedActions.Enqueue(action)
            };
            FakeByteTransport transport = new FakeByteTransport();
            TcpSession session = network.AttachClientTransport(transport);
            bool businessCallbackInvoked = false;
            network.SessionDisconnected = (_, _) => businessCallbackInvoked = true;

            transport.Disconnect(LogoutReason.NetDisconnected);

            Assert.False(businessCallbackInvoked);
            Assert.Same(session, network.Get(session.SessionId));
            Assert.Single(dispatchedActions);

            dispatchedActions.Dequeue().Invoke();

            Assert.True(businessCallbackInvoked);
            Assert.Null(network.Get(session.SessionId));
            Assert.Empty(dispatchedActions);
        }

        [Fact]
        public async Task CompletePacketWaitsForConfiguredSessionDispatcher()
        {
            Queue<Action> dispatchedActions = new Queue<Action>();
            FakeByteTransport transport = new FakeByteTransport();
            using TcpSession session = new TcpSession(transport, new MessageRouter())
            {
                PostToMainThread = action => dispatchedActions.Enqueue(action)
            };
            Task<HeartbeatReq> waiter = session.WaitForAsync<HeartbeatReq>(1000);
            HeartbeatReq expected = new HeartbeatReq { ClientTime = 789L };

            byte[] packet = PacketCodec.Encode("Misc.HeartbeatReq", expected.Serialize());
            transport.Emit(packet, 0, packet.Length);

            Assert.False(waiter.IsCompleted);
            Assert.Single(dispatchedActions);

            dispatchedActions.Dequeue().Invoke();
            HeartbeatReq actual = await waiter;

            Assert.Equal(expected.ClientTime, actual.ClientTime);
        }

        [Fact]
        public async Task BusinessHandlersRunToCompletionInPacketOrder()
        {
            MessageRouter router = CreateOrderedSessionRouter();
            FakeByteTransport transport = new FakeByteTransport();
            using TcpSession session = new TcpSession(transport, router);
            using OrderedSessionTestState state = OrderedSessionTestState.Register(session);
            state.BlockFirstHandler = true;

            EmitOrderedNotification(transport, 1);
            await state.FirstHandlerStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(1),
                TestContext.Current.CancellationToken);
            EmitOrderedNotification(transport, 2);

            Assert.False(state.SecondHandlerStarted.Task.IsCompleted);
            Assert.Equal(new[] { "start:1" }, state.GetEvents());

            state.AllowFirstHandlerToComplete();
            await state.SecondHandlerFinished.Task.WaitAsync(
                TimeSpan.FromSeconds(1),
                TestContext.Current.CancellationToken);

            Assert.Equal(new[] { "start:1", "finish:1", "start:2", "finish:2" }, state.GetEvents());
        }

        [Fact]
        public async Task DisconnectCallbackRunsAfterReceivedBusinessHandlerCompletes()
        {
            MessageRouter router = CreateOrderedSessionRouter();
            FakeByteTransport transport = new FakeByteTransport();
            using TcpSession session = new TcpSession(transport, router);
            using OrderedSessionTestState state = OrderedSessionTestState.Register(session);
            state.BlockFirstHandler = true;
            TaskCompletionSource<bool> disconnected = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            session.OnDisconnected += (_, _) =>
            {
                state.RecordEvent("disconnect");
                disconnected.TrySetResult(true);
            };

            EmitOrderedNotification(transport, 1);
            await state.FirstHandlerStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(1),
                TestContext.Current.CancellationToken);
            transport.Disconnect(LogoutReason.NetDisconnected);

            Assert.False(disconnected.Task.IsCompleted);
            Assert.Equal(new[] { "start:1" }, state.GetEvents());

            state.AllowFirstHandlerToComplete();
            await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

            Assert.Equal(new[] { "start:1", "finish:1", "disconnect" }, state.GetEvents());
        }

        [Fact]
        public async Task RpcResponseBypassesBusinessQueueAndCompletesCurrentHandler()
        {
            MessageRouter router = CreateOrderedSessionRouter();
            FakeByteTransport transport = new FakeByteTransport();
            using TcpSession session = new TcpSession(transport, router);
            using OrderedSessionTestState state = OrderedSessionTestState.Register(session);

            EmitOrderedNotification(transport, 1, true);
            await state.RpcWaitStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(1),
                TestContext.Current.CancellationToken);
            EmitOrderedNotification(transport, 2);

            Assert.False(state.SecondHandlerStarted.Task.IsCompleted);
            Assert.Single(transport.Sends);

            PacketCodec.DecodeResult? requestPacket = PacketCodec.TryDecode(
                transport.Sends[0].Bytes,
                0,
                transport.Sends[0].Bytes.Length);
            Assert.NotNull(requestPacket);
            Assert.Equal("Network.RpcRequest", requestPacket!.MessageName);
            TestRpcRequest request;
            using (MemoryStream stream = new MemoryStream(requestPacket.Payload))
            {
                request = Serializer.Deserialize<TestRpcRequest>(stream);
            }

            HeartbeatRes heartbeatResponse = new HeartbeatRes { ClientTime = 71L, ServerTime = 92L };
            TestRpcResponse response = new TestRpcResponse
            {
                RpcId = request.RpcId,
                MessageName = "Misc.HeartbeatRes",
                Payload = heartbeatResponse.Serialize()
            };
            byte[] responsePacket = PacketCodec.Encode("Network.RpcResponse", SerializeContract(response));
            transport.Emit(responsePacket, 0, responsePacket.Length);

            await state.SecondHandlerFinished.Task.WaitAsync(
                TimeSpan.FromSeconds(1),
                TestContext.Current.CancellationToken);

            Assert.Equal(heartbeatResponse.ServerTime, state.RpcServerTime);
            Assert.Equal(new[] { "start:1", "finish:1", "start:2", "finish:2" }, state.GetEvents());
        }

        private static MessageRouter CreateOrderedSessionRouter()
        {
            MessageRouter router = new MessageRouter();
            router.AutoRegister(typeof(OrderedSessionNotificationHandler).Assembly);
            return router;
        }

        private static void EmitOrderedNotification(
            FakeByteTransport transport,
            int sequence,
            bool awaitRpc = false)
        {
            OrderedSessionNotification notification = new OrderedSessionNotification
            {
                Sequence = sequence,
                AwaitRpc = awaitRpc
            };
            byte[] packet = PacketCodec.Encode("Tests.OrderedSessionNotification", notification.Serialize());
            transport.Emit(packet, 0, packet.Length);
        }

        private static byte[] SerializeContract<T>(T value)
        {
            using MemoryStream stream = new MemoryStream();
            Serializer.Serialize(stream, value);
            return stream.ToArray();
        }

        private sealed class FakeByteTransport : IByteTransport
        {
            private bool _connected = true;

            internal List<(byte[] Bytes, TransportSendMode Mode)> Sends { get; } =
                new List<(byte[] Bytes, TransportSendMode Mode)>();

            public bool IsConnected => _connected;

            public string RemoteIp => "fake";

            public long BytesSent { get; private set; }

            public long BytesReceivedCount { get; private set; }

            public int QueuedBytes => 0;

            public int PeakQueuedBytes => 0;

            public long ReplacedSnapshotCount => 0L;

            public event Action<byte[]>? BytesReceived;

            public event Action<LogoutReason>? Disconnected;

            public void StartReceive()
            {
            }

            public void Send(byte[] bytes, TransportSendMode mode)
            {
                Sends.Add((bytes, mode));
                BytesSent += bytes.Length;
            }

            public void Disconnect(LogoutReason reason)
            {
                if (!_connected)
                {
                    return;
                }

                _connected = false;
                Disconnected?.Invoke(reason);
            }

            public void Emit(byte[] source, int offset, int count)
            {
                byte[] bytes = new byte[count];
                Buffer.BlockCopy(source, offset, bytes, 0, count);
                BytesReceivedCount += count;
                BytesReceived?.Invoke(bytes);
            }

            public void Dispose()
            {
                Disconnect(LogoutReason.NetDisconnected);
            }
        }

        private sealed class DisconnectDuringConnectionCheckTransport : IByteTransport
        {
            private bool _disconnectRaised;

            public bool IsConnected
            {
                get
                {
                    if (!_disconnectRaised)
                    {
                        _disconnectRaised = true;
                        Disconnected?.Invoke(LogoutReason.NetDisconnected);
                    }

                    return true;
                }
            }

            public string RemoteIp => "disconnect-race";

            public long BytesSent => 0L;

            public long BytesReceivedCount => 0L;

            public int QueuedBytes => 0;

            public int PeakQueuedBytes => 0;

            public long ReplacedSnapshotCount => 0L;

            public int SendCount { get; private set; }

            public event Action<byte[]>? BytesReceived
            {
                add { }
                remove { }
            }

            public event Action<LogoutReason>? Disconnected;

            public void StartReceive()
            {
            }

            public void Send(byte[] bytes, TransportSendMode mode)
            {
                SendCount++;
            }

            public void Disconnect(LogoutReason reason)
            {
                if (_disconnectRaised)
                {
                    return;
                }

                _disconnectRaised = true;
                Disconnected?.Invoke(reason);
            }

            public void Dispose()
            {
                Disconnect(LogoutReason.NetDisconnected);
            }
        }
    }

    [ProtoContract]
    [Message("Tests.OrderedSessionNotification")]
    public sealed class OrderedSessionNotification : IMessage
    {
        [ProtoMember(1)]
        public int Sequence { get; set; }

        [ProtoMember(2)]
        public bool AwaitRpc { get; set; }
    }

    public sealed class OrderedSessionNotificationHandler : NtfMessageHandler<OrderedSessionNotification>
    {
        protected override async Task ExecuteAsync(TcpSession session, OrderedSessionNotification notification)
        {
            OrderedSessionTestState state = OrderedSessionTestState.Get(session);
            state.RecordEvent($"start:{notification.Sequence}");

            if (notification.Sequence == 1)
            {
                state.FirstHandlerStarted.TrySetResult(true);
                if (notification.AwaitRpc)
                {
                    state.RpcWaitStarted.TrySetResult(true);
                    HeartbeatRes response = await session.CallAsync<HeartbeatRes>(new HeartbeatReq(), 0);
                    state.RpcServerTime = response.ServerTime;
                }
                else if (state.BlockFirstHandler)
                {
                    await state.FirstHandlerMayComplete.Task;
                }
            }
            else if (notification.Sequence == 2)
            {
                state.SecondHandlerStarted.TrySetResult(true);
            }

            state.RecordEvent($"finish:{notification.Sequence}");
            if (notification.Sequence == 2)
            {
                state.SecondHandlerFinished.TrySetResult(true);
            }
        }
    }

    [ProtoContract]
    internal sealed class TestRpcRequest
    {
        [ProtoMember(1)]
        public long RpcId { get; set; }

        [ProtoMember(2)]
        public string MessageName { get; set; } = string.Empty;

        [ProtoMember(3)]
        public byte[] Payload { get; set; } = Array.Empty<byte>();
    }

    [ProtoContract]
    internal sealed class TestRpcResponse
    {
        [ProtoMember(1)]
        public long RpcId { get; set; }

        [ProtoMember(2)]
        public string MessageName { get; set; } = string.Empty;

        [ProtoMember(3)]
        public byte[] Payload { get; set; } = Array.Empty<byte>();

        [ProtoMember(4)]
        public string ErrorMessage { get; set; } = string.Empty;
    }

    internal sealed class OrderedSessionTestState : IDisposable
    {
        private static readonly object RegistryLock = new object();
        private static readonly Dictionary<long, OrderedSessionTestState> Registry =
            new Dictionary<long, OrderedSessionTestState>();

        private readonly object _eventLock = new object();
        private readonly List<string> _events = new List<string>();
        private readonly long _sessionId;

        private OrderedSessionTestState(long sessionId)
        {
            _sessionId = sessionId;
        }

        internal bool BlockFirstHandler { get; set; }

        internal long RpcServerTime { get; set; }

        internal TaskCompletionSource<bool> FirstHandlerStarted { get; } = CreateSignal();

        internal TaskCompletionSource<bool> FirstHandlerMayComplete { get; } = CreateSignal();

        internal TaskCompletionSource<bool> SecondHandlerStarted { get; } = CreateSignal();

        internal TaskCompletionSource<bool> SecondHandlerFinished { get; } = CreateSignal();

        internal TaskCompletionSource<bool> RpcWaitStarted { get; } = CreateSignal();

        internal static OrderedSessionTestState Register(TcpSession session)
        {
            OrderedSessionTestState state = new OrderedSessionTestState(session.SessionId);
            lock (RegistryLock)
            {
                Registry.Add(session.SessionId, state);
            }

            return state;
        }

        internal static OrderedSessionTestState Get(TcpSession session)
        {
            lock (RegistryLock)
            {
                return Registry[session.SessionId];
            }
        }

        internal void AllowFirstHandlerToComplete()
        {
            FirstHandlerMayComplete.TrySetResult(true);
        }

        internal void RecordEvent(string value)
        {
            lock (_eventLock)
            {
                _events.Add(value);
            }
        }

        internal string[] GetEvents()
        {
            lock (_eventLock)
            {
                return _events.ToArray();
            }
        }

        public void Dispose()
        {
            lock (RegistryLock)
            {
                Registry.Remove(_sessionId);
            }
        }

        private static TaskCompletionSource<bool> CreateSignal()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
