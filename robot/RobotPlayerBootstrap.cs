using System;
using System.Threading.Tasks;
using Shared.Common.Protos;

namespace Robot
{
    internal sealed class RobotPlayerIdentity
    {
        internal long UserId { get; init; }

        internal long RoleId { get; init; }

        internal string ReconnectToken { get; init; } = string.Empty;
    }

    /// <summary>
    /// 为需要独立持久化数据的 Robot 场景创建一次性账号和角色。
    /// </summary>
    internal static class RobotPlayerBootstrap
    {
        internal static async Task<RobotPlayerIdentity> CreateIsolatedPlayerAsync(
            RobotSession session,
            string scenarioPrefix)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            string suffix = Guid.NewGuid().ToString("N");
            string username = $"robot_{scenarioPrefix}_{suffix}";
            string roleName = $"R{suffix.Substring(0, 10)}";

            HandshakeRes handshake = await session.CallAsync<HandshakeRes>(new HandshakeReq());
            Assertions.True(
                string.IsNullOrEmpty(handshake.ErrorMessage),
                $"握手应成功: {handshake.ErrorMessage}");

            LoginRes login = await session.CallAsync<LoginRes>(new LoginReq
            {
                Username = username,
                Password = "robot-only"
            });
            Assertions.True(string.IsNullOrEmpty(login.ErrorMessage), $"登录应成功: {login.ErrorMessage}");
            Assertions.True(login.PlayerId > 0L, "Robot 账号 ID 应大于 0");
            session.UserId = login.PlayerId;

            CreateRoleRes createRole = await session.CallAsync<CreateRoleRes>(new CreateRoleReq
            {
                Name = roleName,
                Job = 1
            });
            Assertions.True(
                string.IsNullOrEmpty(createRole.ErrorMessage),
                $"创建角色应成功: {createRole.ErrorMessage}");
            Assertions.True(createRole.Role.RoleId > 0L, "Robot 角色 ID 应大于 0");

            EnterGameRes enterGame = await session.CallAsync<EnterGameRes>(new EnterGameReq
            {
                RoleId = createRole.Role.RoleId
            });
            Assertions.True(
                string.IsNullOrEmpty(enterGame.ErrorMessage),
                $"进入游戏应成功: {enterGame.ErrorMessage}");
            Assertions.True(!string.IsNullOrEmpty(enterGame.Token), "进入游戏应签发重连 Token");
            session.RoleId = createRole.Role.RoleId;

            return new RobotPlayerIdentity
            {
                UserId = login.PlayerId,
                RoleId = createRole.Role.RoleId,
                ReconnectToken = enterGame.Token
            };
        }
    }
}
