using System;
using System.Linq;
using System.Threading.Tasks;
using Game.Components.Player;
using Game.Core;
using Game.Core.Network;
using Game.Database;
using Shared.Common.Protos;
using Shared.Core.Event;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.User;

public static class UserHandler
{
    private static readonly ILog Log = LogManager.GetLogger<UserComponent>();

    [EventSubscriber]
    public static void ServerInitEvent(EventType.ServerInitEvent e)
    {
        Context.Server.AddComponent<UserComponent>();
    }

    /// <summary>
    /// 登录处理。验证账号 → 自动注册 → BindUser → 生成重连Token。
    /// </summary>
    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> LoginHandler(TcpSession session, LoginReq req, LoginRes res)
    {
        UserComponent userComponent = Context.Server.GetComponent<UserComponent>();
        var username = req.Username;
        Log.Info($"登录请求 Session:{session.SessionId} User:{username}");

        // 已登录的会话不允许重复登录
        if (session.UserId != 0)
        {
            res.Code = 1001;
            res.Message = "已登录";
            return res;
        }

        if (userComponent.Usernames.TryGetValue(username, out var oldUser))
        {
            userComponent.Users.Remove(oldUser.Id);
            userComponent.Usernames.Remove(oldUser.Username);
            // todo 踢人下线
        }

        // 查询账号
        var account =
            await MongoDb.Instance.QueryFirstOrDefaultAsync<User>(p => p.Username == username);
        if (account == null)
        {
            // 自动注册
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
            res.Code = 1002;
            res.Message = "密码错误";
            return res;
        }

        var user = new User();
        user.Id = account.Id;
        user.Username = username;
        user.Password = req.Password;
        user.Nickname = username;
        user.CreateTime = DateTime.UtcNow;
        userComponent.Users.Add(user.Id, user);
        userComponent.Usernames.Add(user.Username, user);
        // 绑定会话
        session.UserId = user.Id;
        user.Session = session;

        // 生成重连 Token
        var token = Guid.NewGuid().ToString("N");
        session.ReconnectToken = token;
        await TokenStore.Instance.SetAsync($"reconnect:{token}", $"{account.Id}", TimeSpan.FromMinutes(5));

        res.Code = 0;
        res.Message = "OK";
        res.PlayerId = account.Id;
        res.Token = token;

        Log.Info($"登录成功 User:{username} UserId:{account.Id}");
        return res;
    }

    /// <summary>
    /// 角色列表查询。返回当前账号下的所有角色。
    /// </summary>
    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> RoleListHandler(TcpSession session, RoleListReq req, RoleListRes res)
    {
        if (session.UserId == 0)
        {
            res.Code = 1001;
            res.Message = "未登录";
            return res;
        }

        var roles = await MongoDb.Instance.QueryAsync<UserRole>(r => r.UserId == session.UserId);
        res.Code = 0;
        res.Message = "OK";
        res.Roles = roles.Select(r => new RoleInfo
        {
            RoleId = r.RoleId,
            Name = r.Name,
            Level = r.Level,
            Job = r.Job
        }).ToList();

        Log.Info($"角色列表 UserId:{session.UserId} 数量:{res.Roles.Count}");
        return res;
    }

    /// <summary>
    /// 创建角色。校验名称唯一性 → 生成ID → 写入角色索引 + Role实体。
    /// </summary>
    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> CreateRoleHandler(TcpSession session, CreateRoleReq req, CreateRoleRes res)
    {
        if (session.UserId == 0)
        {
            res.Code = 1001;
            res.Message = "未登录";
            return res;
        }

        var name = req.Name.Trim();

        // 名称校验
        if (string.IsNullOrEmpty(name) || name.Length > 12)
        {
            res.Code = 1002;
            res.Message = "角色名长度不合法（1-12字符）";
            return res;
        }

        // 角色数量限制
        var existing = await MongoDb.Instance.QueryAsync<UserRole>(r => r.UserId == session.UserId);
        if (existing.Count >= UserComponent.MaxRolesPerUser)
        {
            res.Code = 1003;
            res.Message = $"最多创建{UserComponent.MaxRolesPerUser}个角色";
            return res;
        }

        // 名称唯一性
        if (await MongoDb.Instance.AnyAsync<UserRole>(r => r.Name == name))
        {
            res.Code = 1004;
            res.Message = "角色名已存在";
            return res;
        }

        // 生成角色ID（雪花算法）
        var roleId = IdGenerator.Instance.NextId();

        // 创建 Role 实体（持久化到 MongoDB role 集合）
        var role = new Role
        {
            Id = roleId,
            UserId = session.UserId,
            Name = name,
            Level = 1,
            Job = req.Job,
            CreateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        await MongoDb.Instance.InsertAsync(role);

        // 写入角色索引
        await MongoDb.Instance.InsertAsync(new UserRole
        {
            RoleId = roleId,
            UserId = session.UserId,
            Name = name,
            Level = 1,
            Job = req.Job
        });

        res.Code = 0;
        res.Message = "OK";
        res.Role = new RoleInfo { RoleId = roleId, Name = name, Level = 1, Job = req.Job };

        Log.Info($"创建角色 UserId:{session.UserId} RoleId:{roleId} Name:{name}");
        return res;
    }

    /// <summary>
    /// 选角入场。加载角色 → BindRole → 创建 PlayerEntity + 挂载全部 Component → 进入游戏。
    /// </summary>
    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> SelectRoleHandler(TcpSession session, SelectRoleReq req, SelectRoleRes res)
    {
        if (session.UserId == 0)
        {
            res.Code = 1001;
            res.Message = "未登录";
            return res;
        }

        // 加载角色
        var role = await Context.Players.LoadRoleAsync(req.RoleId);
        if (role == null)
        {
            res.Code = 1002;
            res.Message = "角色不存在";
            return res;
        }

        if (role.UserId != session.UserId)
        {
            res.Code = 1003;
            res.Message = "不是你的角色";
            return res;
        }

        // 绑定角色到会话
        session.RoleId = role.Id;

        // 更新重连Token（包含roleId）
        if (!string.IsNullOrEmpty(session.ReconnectToken))
        {
            await TokenStore.Instance.SetAsync(
                $"reconnect:{session.ReconnectToken}",
                $"{session.UserId}:{role.Id}",
                TimeSpan.FromMinutes(5));
        }

        // 创建在线玩家
        var player = PlayerEntity.Create(role.Id, session.UserId, role);
        player.Online.Session = session;

        Context.Players.AddPlayer(player);

        // 发布上线事件，各模块自行挂载 Component 并加载数据
        await EventBus.Instance.PublishAsync(new EventType.PlayerOnlineEvent { RoleId = role.Id });

        res.Code = 0;
        res.Message = "OK";
        res.RoleId = role.Id;
        res.Name = role.Name;
        res.Level = role.Level;
        res.Job = role.Job;

        Log.Info($"选角入场 UserId:{session.UserId} RoleId:{role.Id} Name:{role.Name}");
        return res;
    }

    /// <summary>
    /// 断线重连。验证Token → 恢复会话绑定 → 重新加入在线列表。
    /// </summary>
    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> ReconnectHandler(TcpSession session, ReconnectReq req, ReconnectRes res)
    {
        // 验证 Token
        var tokenKey = $"reconnect:{req.Token}";
        var tokenStr = await TokenStore.Instance.GetAsync(tokenKey);
        if (tokenStr == null)
        {
            res.Code = 1001;
            res.Message = "Token无效或已过期";
            return res;
        }

        // 解析 token: "userId" 或 "userId:roleId"
        var parts = tokenStr.Split(':');
        if (parts.Length < 1 || !long.TryParse(parts[0], out var userId))
        {
            res.Code = 1002;
            res.Message = "Token数据异常";
            return res;
        }

        long roleId = 0;
        if (parts.Length >= 2) long.TryParse(parts[1], out roleId);

        // 如果指定了 roleId，以请求中的为准
        if (req.RoleId > 0) roleId = req.RoleId;

        // 绑定会话
        session.UserId = userId;

        res.Code = 0;
        res.Message = "OK";

        // 如果有角色，恢复在线状态
        if (roleId > 0)
        {
            var role = await Context.Players.LoadRoleAsync(roleId);
            if (role != null && role.UserId == userId)
            {
                session.RoleId = role.Id;

                var player = PlayerEntity.Create(role.Id, userId, role);
                player.Online.Session = session;

                Context.Players.AddPlayer(player);

                // 发布上线事件，各模块自行挂载 Component 并加载数据
                await EventBus.Instance.PublishAsync(new EventType.PlayerOnlineEvent { RoleId = role.Id });

                res.RoleId = role.Id;
                res.Name = role.Name;
                res.Level = role.Level;

                Log.Info($"重连成功 UserId:{userId} RoleId:{role.Id}");
            }
            else
            {
                Log.Warn($"重连角色不存在 RoleId:{roleId} UserId:{userId}");
            }
        }
        else
        {
            Log.Info($"重连成功（未选角） UserId:{userId}");
        }

        // 刷新 Token
        var newToken = Guid.NewGuid().ToString("N");
        session.ReconnectToken = newToken;
        await TokenStore.Instance.SetAsync($"reconnect:{newToken}",
            roleId > 0 ? $"{userId}:{roleId}" : $"{userId}",
            TimeSpan.FromMinutes(5));
        await TokenStore.Instance.RemoveAsync(tokenKey);

        return res;
    }
}