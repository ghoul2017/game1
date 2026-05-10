using System;
using System.Linq;
using System.Threading.Tasks;
using Game.Components.Player;
using Game.Core;
using Game.Core.Network;
using Game.Database;
using Shared.Common.Enums;
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
            // 踢旧连接下线
            if (oldUser.Session != null)
            {
                var oldSession = oldUser.Session;
                // 先清理 PlayerEntity（如果已选角）
                if (oldSession.RoleId != 0)
                {
                    var oldPlayer = Context.Players.GetByRoleId(oldSession.RoleId);
                    if (oldPlayer != null)
                    {
                        oldPlayer.Online.Session = null;
                        _ = Context.Players.SaveRoleAsync(oldPlayer);
                        Context.Players.RemovePlayer(oldSession.RoleId);
                        EventBus.Instance.Publish(new EventType.PlayerOfflineEvent { RoleId = oldSession.RoleId });
                    }
                    // 发踢人通知再断开
                    oldSession.Send(new KickNtf { Reason = (int)LogoutReason.AnotherSession });
                }
                oldSession.Disconnect();
            }
            userComponent.Users.Remove(oldUser.Id);
            userComponent.Usernames.Remove(oldUser.Username);
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
        var token = TokenSigner.Instance.Generate(account.Id);
        session.ReconnectToken = token;

        // 查询角色列表
        var roles = await MongoDb.Instance.QueryAsync<UserRole>(r => r.UserId == account.Id);

        res.Code = 0;
        res.Message = "OK";
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

        // 创建 RoleDataComponent 实体（持久化到 MongoDB role 集合）
        var roleData = new RoleDataComponent
        {
            Id = roleId,
            UserId = session.UserId,
            Name = name,
            Level = 1,
            Job = req.Job,
            CreateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        await MongoDb.Instance.InsertAsync(roleData);

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
    /// 进入游戏。用 UserRole 验证角色归属 → 调用 PlayerSystem.EnterGame。
    /// </summary>
    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> EnterGameHandler(TcpSession session, EnterGameReq req, EnterGameRes res)
    {
        if (session.UserId == 0)
        {
            res.Code = 1001;
            res.Message = "未登录";
            return res;
        }

        // 用 UserRole 验证角色归属（轻量索引，不加载完整 Role 实体）
        var userRole = await MongoDb.Instance.QueryFirstOrDefaultAsync<UserRole>(
            r => r.RoleId == req.RoleId && r.UserId == session.UserId);
        if (userRole == null)
        {
            res.Code = 1002;
            res.Message = "角色不存在或不属于该账号";
            return res;
        }

        // 调用 PlayerSystem.EnterGame 完成创建
        var result = await Context.Players.EnterGame(req.RoleId, session.UserId, session);
        if (result == null)
        {
            res.Code = 1003;
            res.Message = "进入游戏失败";
            return res;
        }

        var (player, token) = result.Value;
        var roleData = player.RoleData;

        res.Code = 0;
        res.Message = "OK";
        res.RoleId = roleData.Id;
        res.Name = roleData.Name;
        res.Level = roleData.Level;
        res.Job = roleData.Job;
        res.Token = token;

        return res;
    }

    /// <summary>
    /// 断线重连。验证Token签名 + 检查LastToken匹配 → 恢复会话绑定 → 发新Token。
    /// 客户端收到成功后自行走 EnterGameReq 流程。
    /// </summary>
    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> ReconnectHandler(TcpSession session, ReconnectReq req, ReconnectRes res)
    {
        // 验证 Token 签名和有效期
        var parsed = TokenSigner.Instance.Validate(req.Token);
        if (parsed == null)
        {
            res.Code = 1001;
            res.Message = "Token无效或已过期";
            return res;
        }

        var (userId, roleId, _) = parsed.Value;

        // 如果有 roleId，检查 RoleDataComponent.LastToken 匹配（单次有效）
        RoleDataComponent? roleData = null;
        if (roleId > 0)
        {
            roleData = await Context.Players.LoadRoleDataAsync(roleId);
            if (roleData == null || roleData.LastToken != req.Token)
            {
                res.Code = 1002;
                res.Message = "Token已失效";
                return res;
            }
        }

        // 绑定会话
        session.UserId = userId;

        // 生成新 Token
        var newToken = TokenSigner.Instance.Generate(userId, roleId);
        session.ReconnectToken = newToken;

        // 更新 LastToken
        if (roleData != null)
        {
            roleData.LastToken = newToken;
        }

        res.Code = 0;
        res.Message = "OK";
        res.Token = newToken;

        Log.Info($"重连成功 UserId:{userId}");
        return res;
    }
}