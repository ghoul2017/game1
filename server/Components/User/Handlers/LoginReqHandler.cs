using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Game.Database;
using Shared.Common.Enums;
using Shared.Common.Protos;
using Shared.Core;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.User;

public sealed class LoginReqHandler : RpcMessageHandler<LoginReq, LoginRes>
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(LoginReqHandler));

    protected override async Task ExecuteAsync(TcpSession session, LoginReq req, LoginRes res)
    {
        UserComponent userComponent = Context.Server.GetComponent<UserComponent>();

        string username = req.Username;
        Log.Info($"登录请求 Session:{session.SessionId} User:{username}");

        if (session.UserId != 0)
        {
            res.ErrorMessage = "已登录";
            return;
        }

        User? account = await MongoDb.Instance.QueryFirstOrDefaultAsync<User>(p => p.Username == username);
        if (account == null)
        {
            account = new User
            {
                Id = IdGenerator.Instance.NextId(),
                Username = username,
                Password = req.Password,
                Nickname = username,
                CreateTime = DateTime.UtcNow
            };
            await MongoDb.Instance.InsertAsync(account);
            Log.Info($"自动注册账号 User:{username} Id:{account.Id}");
        }
        else if (account.Password != req.Password)
        {
            res.ErrorMessage = "密码错误";
            return;
        }

        List<UserRole> roles = await MongoDb.Instance.QueryAsync<UserRole>(r => r.UserId == account.Id);

        // 只有凭据已经验证成功，才允许替换同账号的在线会话和废除其重连令牌。
        ReconnectTokenRegistry.Instance.InvalidateUser(account.Id);
        if (userComponent.Usernames.TryGetValue(username, out User? oldUser))
        {
            if (oldUser.Session != null)
            {
                UserHelper.KickUser(oldUser.Session, LogoutReason.AnotherSession);
            }

            userComponent.Users.Remove(oldUser.Id);
            userComponent.Usernames.Remove(oldUser.Username);
        }

        User user = new User
        {
            Id = account.Id,
            Username = username,
            Password = req.Password,
            Nickname = username,
            CreateTime = DateTime.UtcNow,
            Session = session
        };
        userComponent.Users.Add(user.Id, user);
        userComponent.Usernames.Add(user.Username, user);
        session.UserId = user.Id;

        string token = TokenSigner.Instance.Generate(account.Id);
        session.ReconnectToken = token;

        res.PlayerId = account.Id;
        res.Token = token;
        res.Roles = roles.Select(r => new RoleInfo
        {
            RoleId = r.RoleId,
            Name = r.Name,
            Level = r.Level,
            Job = r.Job
        }).ToList();

        Log.Info($"登录成功 User:{username} UserId:{account.Id} 角色数:{res.Roles.Count}");
    }
}
