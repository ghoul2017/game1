using System.Threading.Tasks;
using Shared.Common.Protos;

namespace Robot
{
    /// <summary>
    /// 登录流程测试：握手 → 登录 → 验证角色列表
    /// </summary>
    public class LoginTest : TestScenario
    {
        public override string Name => "login_test";

        public override async Task Run()
        {
            // 1. 握手
            Log("发送 HandshakeReq...");
            var handshakeRes = await Session.SendAndWaitAsync<HandshakeRes>(new HandshakeReq());
            Assertions.True(string.IsNullOrEmpty(handshakeRes.ErrorMessage), $"握手应成功: {handshakeRes.ErrorMessage}");

            // 2. 登录
            Log("发送 LoginReq...");
            var loginReq = new LoginReq
            {
                Username = "test",
                Password = "123456"
            };
            var loginRes = await Session.SendAndWaitAsync<LoginRes>(loginReq);
            Assertions.True(string.IsNullOrEmpty(loginRes.ErrorMessage), $"登录应成功: {loginRes.ErrorMessage}");
            Assertions.True(loginRes.PlayerId > 0, "PlayerId 应大于 0");
            Assertions.NotNull(loginRes.Token, "Token 不应为 null");

            Session.UserId = loginRes.PlayerId;

            Log($"登录成功: PlayerId={loginRes.PlayerId}, 角色数={loginRes.Roles.Count}, Token={loginRes.Token}");

            // 3. 验证角色列表
            if (loginRes.Roles.Count > 0)
            {
                Log("角色列表:");
                foreach (var role in loginRes.Roles)
                {
                    Log($"  RoleId={role.RoleId} Name={role.Name} Level={role.Level} Job={role.Job}");
                }
            }
            else
            {
                Log("无角色，跳过 EnterGame 测试");
            }
        }
    }
}
