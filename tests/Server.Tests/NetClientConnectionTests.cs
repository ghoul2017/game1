using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Client.Logic;
using ProtoBuf;
using Shared.Common.Enums;
using Shared.Common.Protos;
using Shared.Core.Network;
using Xunit;

namespace Server.Tests;

public sealed class NetClientConnectionTests
{
    [Fact]
    public async Task EnterGameFailureAfterReconnectClosesAuthenticatedSession()
    {
        string previousToken = GameData.Token;
        RoleData previousRole = GameData.CurrentRole;
        EnterMapData previousMap = GameData.CurrentMap;
        FakeByteTransport transport = new FakeByteTransport();
        NetClient client = new NetClient();
        TcpSession session = new TcpSession(transport, new MessageRouter());
        FieldInfo sessionField = typeof(NetClient).GetField(
            "_session",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        MethodInfo failureMethod = typeof(NetClient).GetMethod(
            "FailReconnectAfterEnterGame",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        try
        {
            sessionField.SetValue(client, session);
            GameData.Token = "authenticated-token";
            GameData.CurrentRole = new RoleData { RoleId = 700001L };
            GameData.CurrentMap = new EnterMapData { MapCfgId = 2 };

            failureMethod.Invoke(client, new object[] { session, "role rejected" });
            LogoutReason reason = await transport.DisconnectedTask.WaitAsync(
                TimeSpan.FromSeconds(2.0),
                TestContext.Current.CancellationToken);

            Assert.Equal(LogoutReason.NetDisconnected, reason);
            Assert.False(transport.IsConnected);
            Assert.Equal(NetClient.ConnState.Disconnected, client.State);
            Assert.Equal(string.Empty, GameData.Token);
            Assert.Null(GameData.CurrentRole);
            Assert.Null(GameData.CurrentMap);
        }
        finally
        {
            client.Dispose();
            GameData.Token = previousToken;
            GameData.CurrentRole = previousRole;
            GameData.CurrentMap = previousMap;
        }
    }

    [Fact]
    public async Task StaleHandshakeCompletionDoesNotDisconnectReplacementSession()
    {
        FakeByteTransport staleTransport = new FakeByteTransport();
        FakeByteTransport currentTransport = new FakeByteTransport();
        using TcpSession staleSession = new TcpSession(staleTransport, new MessageRouter());
        using TcpSession currentSession = new TcpSession(currentTransport, new MessageRouter());
        NetClient client = new NetClient();
        FieldInfo sessionField = typeof(NetClient).GetField(
            "_session",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        FieldInfo stateField = typeof(NetClient).GetField(
            "_state",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        MethodInfo handshakeMethod = typeof(NetClient).GetMethod(
            "StartHandshakeAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        sessionField.SetValue(client, staleSession);
        stateField.SetValue(client, NetClient.ConnState.Handshaking);
        Task staleHandshake = (Task)handshakeMethod.Invoke(client, new object[] { staleSession })!;
        Assert.Single(staleTransport.Sends);

        sessionField.SetValue(client, currentSession);
        EmitRpcResponse(
            staleTransport,
            staleTransport.Sends[0],
            new HandshakeRes { ErrorMessage = "stale rejected" });
        await staleHandshake.WaitAsync(TimeSpan.FromSeconds(2.0), TestContext.Current.CancellationToken);

        Assert.True(currentTransport.IsConnected);
        Assert.Same(currentSession, sessionField.GetValue(client));
        client.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelOrDisposeRejectsLateConnectionAndClosesSession(bool dispose)
    {
        TaskCompletionSource<TcpSession> connection = new TaskCompletionSource<TcpSession>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        FakeByteTransport transport = new FakeByteTransport();
        NetClient client = new NetClient
        {
            SessionFactory = (_, _) => connection.Task
        };

        client.Connect();
        if (dispose)
        {
            client.Dispose();
        }
        else
        {
            client.Disconnect();
        }

        connection.SetResult(new TcpSession(transport, new MessageRouter()));
        LogoutReason reason = await transport.DisconnectedTask.WaitAsync(
            TimeSpan.FromSeconds(2.0),
            TestContext.Current.CancellationToken);

        Assert.Equal(LogoutReason.NetDisconnected, reason);
        Assert.False(transport.IsConnected);
        Assert.Equal(NetClient.ConnState.Disconnected, client.State);
        client.Dispose();
    }

    private sealed class FakeByteTransport : IByteTransport
    {
        private readonly TaskCompletionSource<LogoutReason> _disconnected =
            new TaskCompletionSource<LogoutReason>(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _isConnected = true;

        public bool IsConnected => _isConnected;

        public string RemoteIp => "late-connect";

        public long BytesSent => 0L;

        public long BytesReceivedCount => 0L;

        public int QueuedBytes => 0;

        public int PeakQueuedBytes => 0;

        public long ReplacedSnapshotCount => 0L;

        internal Task<LogoutReason> DisconnectedTask => _disconnected.Task;

        internal List<byte[]> Sends { get; } = new List<byte[]>();

        public event Action<byte[]>? BytesReceived;

        public event Action<LogoutReason>? Disconnected;

        public void StartReceive()
        {
        }

        public void Send(byte[] bytes, TransportSendMode mode)
        {
            Sends.Add(bytes);
        }

        internal void Emit(byte[] bytes)
        {
            BytesReceived?.Invoke(bytes);
        }

        public void Disconnect(LogoutReason reason)
        {
            if (!_isConnected)
            {
                return;
            }

            _isConnected = false;
            _disconnected.TrySetResult(reason);
            Disconnected?.Invoke(reason);
        }

        public void Dispose()
        {
            Disconnect(LogoutReason.NetDisconnected);
        }
    }

    private static void EmitRpcResponse<T>(FakeByteTransport transport, byte[] requestBytes, T response)
        where T : IMessage
    {
        PacketCodec.DecodeResult? requestPacket = PacketCodec.TryDecode(requestBytes, 0, requestBytes.Length);
        Assert.NotNull(requestPacket);
        TestRpcRequest request;
        using (MemoryStream stream = new MemoryStream(requestPacket!.Payload))
        {
            request = Serializer.Deserialize<TestRpcRequest>(stream);
        }

        TestRpcResponse rpcResponse = new TestRpcResponse
        {
            RpcId = request.RpcId,
            MessageName = MessageRegistry.GetName(typeof(T)),
            Payload = response.Serialize()
        };
        using MemoryStream responseStream = new MemoryStream();
        Serializer.Serialize(responseStream, rpcResponse);
        transport.Emit(PacketCodec.Encode("Network.RpcResponse", responseStream.ToArray()));
    }

    [ProtoContract]
    private sealed class TestRpcRequest
    {
        [ProtoMember(1)]
        public long RpcId { get; set; }
    }

    [ProtoContract]
    private sealed class TestRpcResponse
    {
        [ProtoMember(1)]
        public long RpcId { get; set; }

        [ProtoMember(2)]
        public string MessageName { get; set; } = string.Empty;

        [ProtoMember(3)]
        public byte[] Payload { get; set; } = Array.Empty<byte>();
    }
}
