using System;
using System.Threading.Tasks;
using Game;
using Game.Components.Player;
using Game.Components.User;
using Shared.Common.Enums;
using Shared.Common.Protos;
using Shared.Core.ECS;
using Shared.Core.Entities;
using Shared.Core.Network;
using ProtoBuf;
using Xunit;

namespace Server.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PlayerSessionAuthorityCollection
{
    public const string Name = "Player session authority";
}

[Collection(PlayerSessionAuthorityCollection.Name)]
public sealed class PlayerSessionAuthorityTests
{
    [Fact]
    public async Task AlreadyBoundSessionCannotSwitchRoleOrConsumeReconnectFlow()
    {
        Module previousServer = Context.Server;
        TcpSession session = CreateSession("bound");
        try
        {
            PlayerComponent players = CreatePlayerContext();
            PlayerEntity player = AddPlayer(players, session);
            UserComponent users = Context.Server.AddComponent<UserComponent>();
            users.Users.Add(10L, new User { Id = 10L, Session = session });

            EnterGameReqHandler enterGameHandler = new EnterGameReqHandler();
            IMessage? enterMessage = await enterGameHandler.HandleAsync(
                session,
                new EnterGameReq { RoleId = player.Id + 1L }.Serialize());
            EnterGameRes enterResponse = Assert.IsType<EnterGameRes>(enterMessage);
            Assert.Equal("当前会话已绑定其他角色", enterResponse.ErrorMessage);
            Assert.Equal(player.Id, session.RoleId);
            Assert.Same(player, players.GetByRoleId(player.Id));

            ReconnectReqHandler reconnectHandler = new ReconnectReqHandler();
            IMessage? reconnectMessage = await reconnectHandler.HandleAsync(
                session,
                new ReconnectReq { RoleId = player.Id, Token = "must-not-be-consumed" }.Serialize());
            ReconnectRes reconnectResponse = Assert.IsType<ReconnectRes>(reconnectMessage);
            Assert.Equal("当前会话已完成认证，不能重复重连", reconnectResponse.ErrorMessage);
            Assert.Equal(player.Id, session.RoleId);
            Assert.Equal(10L, session.UserId);
        }
        finally
        {
            session.Dispose();
            Context.Server = previousServer;
        }
    }

    [Fact]
    public void RoleAuthorityRequiresThePlayerToPointBackToTheSameSession()
    {
        Module previousServer = Context.Server;
        TcpSession authoritativeSession = CreateSession("authoritative");
        TcpSession staleSession = CreateSession("stale");
        try
        {
            PlayerComponent players = CreatePlayerContext();
            PlayerEntity player = AddPlayer(players, authoritativeSession);
            staleSession.RoleId = player.Id;

            Assert.Null(players.GetBySession(staleSession));
            Assert.False(players.CanEnterRole(staleSession, player.Id));
            Assert.Same(player, players.GetByRoleId(player.Id));
            Assert.Same(authoritativeSession, player.GetComponent<PlayerOnlineComponent>().Session);
            Assert.Same(player, players.GetBySession(authoritativeSession));
            Assert.True(players.CanEnterRole(authoritativeSession, player.Id));
            Assert.False(players.CanEnterRole(authoritativeSession, player.Id + 1L));
        }
        finally
        {
            authoritativeSession.Dispose();
            staleSession.Dispose();
            Context.Server = previousServer;
        }
    }

    [Fact]
    public async Task PlayerHandlersRejectAStaleSessionThatOnlyCarriesTheRoleId()
    {
        Module previousServer = Context.Server;
        TcpSession authoritativeSession = CreateSession("authoritative");
        TcpSession staleSession = CreateSession("stale");
        try
        {
            PlayerComponent players = CreatePlayerContext();
            PlayerEntity player = AddPlayer(players, authoritativeSession);
            staleSession.RoleId = player.Id;
            CountingRpcHandler rpcHandler = new CountingRpcHandler();
            CountingNtfHandler ntfHandler = new CountingNtfHandler();

            IMessage? rejectedRpc = await rpcHandler.HandleAsync(
                staleSession,
                new SessionAuthorityRpcReq().Serialize());
            await ntfHandler.HandleAsync(staleSession, new SessionAuthorityNtf().Serialize());

            SessionAuthorityRpcRes rejectedResponse = Assert.IsType<SessionAuthorityRpcRes>(rejectedRpc);
            Assert.Equal("玩家会话已失效", rejectedResponse.ErrorMessage);
            Assert.Equal(0, rpcHandler.ExecutionCount);
            Assert.Equal(0, ntfHandler.ExecutionCount);

            IMessage? acceptedRpc = await rpcHandler.HandleAsync(
                authoritativeSession,
                new SessionAuthorityRpcReq().Serialize());
            await ntfHandler.HandleAsync(authoritativeSession, new SessionAuthorityNtf().Serialize());

            SessionAuthorityRpcRes acceptedResponse = Assert.IsType<SessionAuthorityRpcRes>(acceptedRpc);
            Assert.Equal(string.Empty, acceptedResponse.ErrorMessage);
            Assert.Equal(1, rpcHandler.ExecutionCount);
            Assert.Equal(1, ntfHandler.ExecutionCount);
        }
        finally
        {
            authoritativeSession.Dispose();
            staleSession.Dispose();
            Context.Server = previousServer;
        }
    }

    [Fact]
    public void CurrentUserSessionCheckRejectsDisconnectedOrReplacedSession()
    {
        Module previousServer = Context.Server;
        TcpSession currentSession = CreateSession("current");
        TcpSession replacementSession = CreateSession("replacement");
        try
        {
            CreatePlayerContext();
            UserComponent users = Context.Server.AddComponent<UserComponent>();
            User user = new User { Id = 10L, Session = currentSession };
            users.Users.Add(user.Id, user);
            currentSession.UserId = user.Id;
            replacementSession.UserId = user.Id;

            Assert.True(UserHelper.IsCurrentSession(currentSession, user.Id));

            user.Session = replacementSession;
            Assert.False(UserHelper.IsCurrentSession(currentSession, user.Id));

            user.Session = currentSession;
            currentSession.Disconnect();
            Assert.False(UserHelper.IsCurrentSession(currentSession, user.Id));
        }
        finally
        {
            currentSession.Dispose();
            replacementSession.Dispose();
            Context.Server = previousServer;
        }
    }

    [Fact]
    public void StaleDisconnectCannotStartOrExtendTheCurrentSessionsRecoveryWindow()
    {
        Module previousServer = Context.Server;
        TcpSession staleSession = CreateSession("stale");
        TcpSession currentSession = CreateSession("current");
        try
        {
            PlayerComponent players = CreatePlayerContext();
            PlayerEntity player = AddPlayer(players, currentSession);
            UserComponent users = Context.Server.AddComponent<UserComponent>();
            users.Users.Add(10L, new User { Id = 10L, Session = currentSession });

            DateTime now = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
            ReconnectTokenRegistry registry = new ReconnectTokenRegistry(TimeSpan.FromMinutes(5.0), () => now);
            string staleToken = registry.Issue(10L, player.Id);
            staleSession.UserId = 10L;
            staleSession.RoleId = player.Id;
            staleSession.ReconnectToken = staleToken;
            Assert.True(registry.BeginRecoveryWindow(10L, player.Id, staleToken));
            Assert.True(
                registry.TryConsumeAndRotate(
                    staleToken,
                    player.Id,
                    out long userId,
                    out long roleId,
                    out string currentToken,
                    out string rotateError));
            Assert.Equal(string.Empty, rotateError);
            currentSession.ReconnectToken = currentToken;

            Assert.False(UserHelper.TryBeginRecoveryWindow(users, players, staleSession, registry));
            now = now.AddHours(1.0);
            Assert.True(UserHelper.TryBeginRecoveryWindow(users, players, currentSession, registry));
            now = now.AddMinutes(4.0);
            Assert.True(
                registry.TryConsumeAndRotate(
                    currentToken,
                    roleId,
                    out long recoveredUserId,
                    out long recoveredRoleId,
                    out string ignoredToken,
                    out string recoveryError));
            Assert.Equal(string.Empty, recoveryError);
            Assert.Equal(userId, recoveredUserId);
            Assert.Equal(roleId, recoveredRoleId);
        }
        finally
        {
            staleSession.Dispose();
            currentSession.Dispose();
            Context.Server = previousServer;
        }
    }

    [Fact]
    public async Task LostReconnectResponseReplaysSameTokenWithoutKickingOrOverwritingAuthority()
    {
        const long userId = 710001L;
        const long roleId = 720001L;
        Module previousServer = Context.Server;
        TcpSession firstSession = CreateSession("first-reconnect");
        TcpSession competingSession = CreateSession("competing-retry");
        TcpSession completedRetrySession = CreateSession("completed-retry");
        ReconnectTokenRegistry registry = ReconnectTokenRegistry.Instance;
        registry.Invalidate(userId, roleId);
        try
        {
            CreatePlayerContext();
            UserComponent users = Context.Server.AddComponent<UserComponent>();
            User user = new User { Id = userId };
            users.Users.Add(userId, user);
            string recoveryToken = registry.Issue(userId, roleId);
            Assert.True(registry.BeginRecoveryWindow(userId, roleId, recoveryToken));
            ReconnectReqHandler handler = new ReconnectReqHandler();
            ReconnectReq request = new ReconnectReq { RoleId = roleId, Token = recoveryToken };

            ReconnectRes first = Assert.IsType<ReconnectRes>(
                await handler.HandleAsync(firstSession, request.Serialize()));
            Assert.Equal(string.Empty, first.ErrorMessage);
            string rotatedToken = first.Token;
            Assert.NotEqual(recoveryToken, rotatedToken);
            Assert.Same(firstSession, user.Session);
            Assert.Equal(roleId, user.PendingReconnectRoleId);

            ReconnectRes sameSessionReplay = Assert.IsType<ReconnectRes>(
                await handler.HandleAsync(firstSession, request.Serialize()));
            Assert.Equal(string.Empty, sameSessionReplay.ErrorMessage);
            Assert.Equal(rotatedToken, sameSessionReplay.Token);
            Assert.Same(firstSession, user.Session);

            ReconnectRes competingReplay = Assert.IsType<ReconnectRes>(
                await handler.HandleAsync(competingSession, request.Serialize()));
            Assert.NotEmpty(competingReplay.ErrorMessage);
            Assert.Equal(rotatedToken, competingReplay.Token);
            Assert.Same(firstSession, user.Session);
            Assert.True(firstSession.IsConnected);
            Assert.Equal(0L, competingSession.UserId);

            Assert.Equal(rotatedToken, registry.IssueOrReuse(userId, roleId, rotatedToken));
            user.PendingReconnectRoleId = 0L;
            firstSession.RoleId = roleId;
            ReconnectRes completedReplay = Assert.IsType<ReconnectRes>(
                await handler.HandleAsync(completedRetrySession, request.Serialize()));
            Assert.NotEmpty(completedReplay.ErrorMessage);
            Assert.Same(firstSession, user.Session);
            Assert.True(firstSession.IsConnected);
            Assert.Equal(0L, completedRetrySession.UserId);
        }
        finally
        {
            registry.Invalidate(userId, roleId);
            firstSession.Dispose();
            competingSession.Dispose();
            completedRetrySession.Dispose();
            Context.Server = previousServer;
        }
    }

    [Fact]
    public async Task PendingReconnectRoleSupportsTwoDisconnectsWithoutGrantingPlayerAuthority()
    {
        const long userId = 730001L;
        const long roleId = 740001L;
        Module previousServer = Context.Server;
        TcpSession firstSession = CreateSession("pending-first");
        TcpSession secondSession = CreateSession("pending-second");
        TcpSession thirdSession = CreateSession("pending-third");
        ReconnectTokenRegistry registry = ReconnectTokenRegistry.Instance;
        registry.Invalidate(userId, roleId);
        try
        {
            PlayerComponent players = CreatePlayerContext();
            UserComponent users = Context.Server.AddComponent<UserComponent>();
            User user = new User { Id = userId };
            users.Users.Add(userId, user);
            string firstToken = registry.Issue(userId, roleId);
            Assert.True(registry.BeginRecoveryWindow(userId, roleId, firstToken));
            ReconnectReqHandler handler = new ReconnectReqHandler();

            ReconnectRes first = Assert.IsType<ReconnectRes>(
                await handler.HandleAsync(
                    firstSession,
                    new ReconnectReq { RoleId = roleId, Token = firstToken }.Serialize()));
            Assert.Equal(string.Empty, first.ErrorMessage);
            Assert.Equal(roleId, user.PendingReconnectRoleId);
            Assert.Equal(0L, firstSession.RoleId);

            CountingRpcHandler rpcHandler = new CountingRpcHandler();
            CountingNtfHandler ntfHandler = new CountingNtfHandler();
            SessionAuthorityRpcRes rejected = Assert.IsType<SessionAuthorityRpcRes>(
                await rpcHandler.HandleAsync(firstSession, new SessionAuthorityRpcReq().Serialize()));
            await ntfHandler.HandleAsync(firstSession, new SessionAuthorityNtf().Serialize());
            Assert.Equal("玩家会话已失效", rejected.ErrorMessage);
            Assert.Equal(0, rpcHandler.ExecutionCount);
            Assert.Equal(0, ntfHandler.ExecutionCount);

            Assert.True(UserHelper.TryBeginRecoveryWindow(users, players, firstSession, registry));
            UserHelper.ClearPendingReconnectRole(users, firstSession);
            user.Session = null;
            ReconnectRes second = Assert.IsType<ReconnectRes>(
                await handler.HandleAsync(
                    secondSession,
                    new ReconnectReq { RoleId = roleId, Token = first.Token }.Serialize()));
            Assert.Equal(string.Empty, second.ErrorMessage);
            Assert.NotEqual(first.Token, second.Token);
            Assert.Same(secondSession, user.Session);
            Assert.Equal(roleId, user.PendingReconnectRoleId);
            Assert.False(UserHelper.TryBeginRecoveryWindow(users, players, firstSession, registry));

            Assert.True(UserHelper.TryBeginRecoveryWindow(users, players, secondSession, registry));
            UserHelper.ClearPendingReconnectRole(users, secondSession);
            user.Session = null;
            ReconnectRes third = Assert.IsType<ReconnectRes>(
                await handler.HandleAsync(
                    thirdSession,
                    new ReconnectReq { RoleId = roleId, Token = second.Token }.Serialize()));
            Assert.Equal(string.Empty, third.ErrorMessage);
            Assert.NotEqual(second.Token, third.Token);
            Assert.Same(thirdSession, user.Session);
            Assert.Equal(roleId, user.PendingReconnectRoleId);
        }
        finally
        {
            registry.Invalidate(userId, roleId);
            firstSession.Dispose();
            secondSession.Dispose();
            thirdSession.Dispose();
            Context.Server = previousServer;
        }
    }

    [Fact]
    public async Task FailedEnterGameKeepsPendingRoleConstraintAndDisconnectRecovery()
    {
        const long userId = 750001L;
        const long pendingRoleId = 760001L;
        Module previousServer = Context.Server;
        TcpSession session = CreateSession("failed-enter-game");
        ReconnectTokenRegistry registry = ReconnectTokenRegistry.Instance;
        registry.Invalidate(userId, pendingRoleId);
        try
        {
            PlayerComponent players = CreatePlayerContext();
            UserComponent users = Context.Server.AddComponent<UserComponent>();
            User user = new User
            {
                Id = userId,
                Session = session,
                PendingReconnectRoleId = pendingRoleId
            };
            users.Users.Add(userId, user);
            session.UserId = userId;
            string token = registry.Issue(userId, pendingRoleId);
            session.ReconnectToken = token;

            EnterGameReqHandler handler = new EnterGameReqHandler();
            EnterGameRes failed = Assert.IsType<EnterGameRes>(
                await handler.HandleAsync(
                    session,
                    new EnterGameReq { RoleId = pendingRoleId + 1L }.Serialize()));

            Assert.Equal("重连角色与进入角色不一致", failed.ErrorMessage);
            Assert.Equal(pendingRoleId, user.PendingReconnectRoleId);
            Assert.Equal(0L, session.RoleId);
            Assert.True(UserHelper.TryBeginRecoveryWindow(users, players, session, registry));
            Assert.True(
                registry.TryConsumeAndRotate(
                    token,
                    pendingRoleId,
                    out long recoveredUserId,
                    out long recoveredRoleId,
                    out string ignoredToken,
                    out string recoveryError));
            Assert.Equal(string.Empty, recoveryError);
            Assert.Equal(userId, recoveredUserId);
            Assert.Equal(pendingRoleId, recoveredRoleId);
        }
        finally
        {
            registry.Invalidate(userId, pendingRoleId);
            session.Dispose();
            Context.Server = previousServer;
        }
    }

    private static PlayerComponent CreatePlayerContext()
    {
        Context.Server = Entity.CreateEntity<Module>();
        return Context.Server.AddComponent<PlayerComponent>();
    }

    private static PlayerEntity AddPlayer(PlayerComponent players, TcpSession session)
    {
        PlayerEntity player = PlayerEntity.Create(20L, 10L, "test", 1, 1);
        player.GetComponent<PlayerOnlineComponent>().Session = session;
        session.UserId = 10L;
        session.RoleId = player.Id;
        players.AddPlayer(player);
        return player;
    }

    private static TcpSession CreateSession(string remoteIp)
    {
        return new TcpSession(new FakeByteTransport(remoteIp), new MessageRouter());
    }

    private sealed class CountingRpcHandler
        : PlayerRpcMessageHandler<SessionAuthorityRpcReq, SessionAuthorityRpcRes>
    {
        internal int ExecutionCount { get; private set; }

        protected override Task ExecuteAsync(
            PlayerEntity player,
            SessionAuthorityRpcReq request,
            SessionAuthorityRpcRes response)
        {
            ExecutionCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class CountingNtfHandler : PlayerNtfMessageHandler<SessionAuthorityNtf>
    {
        internal int ExecutionCount { get; private set; }

        protected override Task ExecuteAsync(PlayerEntity player, SessionAuthorityNtf notification)
        {
            ExecutionCount++;
            return Task.CompletedTask;
        }
    }

    [ProtoContract]
    [Message("Tests.PlayerSessionAuthorityRpcReq")]
    private sealed class SessionAuthorityRpcReq : IMessage
    {
    }

    [ProtoContract]
    [Message("Tests.PlayerSessionAuthorityRpcRes")]
    private sealed class SessionAuthorityRpcRes : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = string.Empty;
    }

    [ProtoContract]
    [Message("Tests.PlayerSessionAuthorityNtf")]
    private sealed class SessionAuthorityNtf : IMessage
    {
    }

    private sealed class FakeByteTransport : IByteTransport
    {
        private bool _isConnected = true;

        internal FakeByteTransport(string remoteIp)
        {
            RemoteIp = remoteIp;
        }

        public bool IsConnected => _isConnected;

        public string RemoteIp { get; }

        public long BytesSent => 0L;

        public long BytesReceivedCount => 0L;

        public int QueuedBytes => 0;

        public int PeakQueuedBytes => 0;

        public long ReplacedSnapshotCount => 0L;

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
        }

        public void Disconnect(LogoutReason reason)
        {
            if (!_isConnected)
            {
                return;
            }

            _isConnected = false;
            Disconnected?.Invoke(reason);
        }

        public void Dispose()
        {
            Disconnect(LogoutReason.NetDisconnected);
        }
    }
}
