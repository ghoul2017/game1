using System;
using System.Linq;
using System.Threading.Tasks;
using Game.Core;
using Game.Core.Network;
using Game.Core.Entity;
using Game.Database;
using Game.Game.Player;
using Game.Network;
using Shared.Common.Protos;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.User;

/// <summary>
/// 用户组件系统 — 扩展方法风格 Handler。
/// </summary>
public static class UserComponentSystem
{
    private static readonly ILog Log = LogManager.GetLogger<UserComponent>();

    /// <summary>
    /// 登录处理。验证账号 → 自动注册 → BindUser → 生成重连Token。
    /// </summary>
    [MessageHandler(typeof(MessageHandler<,>))]
    public static async Task<IMessage?> LoginHandler(this UserComponent self, TcpSession session, LoginReq req, LoginResp resp)
    {
        Log.Info($"登录请求 Session:{session.SessionId} User:{req.Username}");

        // 已登录的会话不允许重复登录
        if (session.UserId != 0)
        {
            resp.Code = 1001;
            resp.Message = "已登录";
            return resp;
        }

        // 查询账号
        Database.MySql.Repositories.Player? account = await DatabaseManager.Instance.PlayerRepo.GetByUsernameAsync(req.Username);
        if (account == null)
        {
            // 自动注册
            account = new Database.MySql.Repositories.Player
            {
                Username = req.Username,
                Password = req.Password,
                Nickname = req.Username,
                CreateTime = DateTime.UtcNow
            };
            var id = await DatabaseManager.Instance.PlayerRepo.CreateAsync(account);
            account.Id = id;
            Log.Info($"自动注册账号 User:{req.Username} Id:{id}");
        }
        else if (account.Password != req.Password)
        {
            resp.Code = 1002;
            resp.Message = "密码错误";
            return resp;
        }

        // 绑定会话
        session.UserId = account.Id;
        SessionManager.Instance.BindUser(session, account.Id);

        // 生成重连 Token
        var token = Guid.NewGuid().ToString("N");
        session.ReconnectToken = token;
        await TokenStore.Instance.SetAsync($"reconnect:{token}", $"{account.Id}", TimeSpan.FromMinutes(5));

        resp.Code = 0;
        resp.Message = "OK";
        resp.PlayerId = account.Id;
        resp.Token = token;

        Log.Info($"登录成功 User:{req.Username} UserId:{account.Id}");
        return resp;
    }

    /// <summary>
    /// 角色列表查询。返回当前账号下的所有角色。
    /// </summary>
    [MessageHandler(typeof(MessageHandler<,>))]
    public static async Task<IMessage?> RoleListHandler(this UserComponent self, TcpSession session, RoleListReq req, RoleListResp resp)
    {
        if (session.UserId == 0)
        {
            resp.Code = 1001;
            resp.Message = "未登录";
            return resp;
        }

        var roles = await DatabaseManager.Instance.RoleRepo.GetByUserIdAsync(session.UserId);
        resp.Code = 0;
        resp.Message = "OK";
        resp.Roles = roles.Select(r => new RoleInfo
        {
            RoleId = r.RoleId,
            Name = r.Name,
            Level = r.Level,
            Job = r.Job
        }).ToList();

        Log.Info($"角色列表 UserId:{session.UserId} 数量:{resp.Roles.Count}");
        return resp;
    }

    /// <summary>
    /// 创建角色。校验名称唯一性 → 生成ID → 写入角色索引 + Role实体。
    /// </summary>
    [MessageHandler(typeof(MessageHandler<,>))]
    public static async Task<IMessage?> CreateRoleHandler(this UserComponent self, TcpSession session, CreateRoleReq req, CreateRoleResp resp)
    {
        if (session.UserId == 0)
        {
            resp.Code = 1001;
            resp.Message = "未登录";
            return resp;
        }

        var name = req.Name.Trim();

        // 名称校验
        if (string.IsNullOrEmpty(name) || name.Length > 12)
        {
            resp.Code = 1002;
            resp.Message = "角色名长度不合法（1-12字符）";
            return resp;
        }

        // 角色数量限制
        const int MaxRolesPerUser = 4;
        var existing = await DatabaseManager.Instance.RoleRepo.GetByUserIdAsync(session.UserId);
        if (existing.Count >= MaxRolesPerUser)
        {
            resp.Code = 1003;
            resp.Message = $"最多创建{MaxRolesPerUser}个角色";
            return resp;
        }

        // 名称唯一性
        if (await DatabaseManager.Instance.RoleRepo.NameExistsAsync(name))
        {
            resp.Code = 1004;
            resp.Message = "角色名已存在";
            return resp;
        }

        // 生成角色ID（雪花算法）
        var roleId = IdGenerator.Instance.NextId();

        // 创建 Role 实体（持久化到 p_role）
        var role = new Role
        {
            Id = roleId,
            UserId = session.UserId,
            Name = name,
            Level = 1,
            Job = req.Job,
            CreateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        await EntityFactory.Instance.InsertAsync(role, immediately: true);

        // 写入角色索引
        await DatabaseManager.Instance.RoleRepo.AddAsync(new Database.MySql.Repositories.UserRole
        {
            RoleId = roleId,
            UserId = session.UserId,
            Name = name,
            Level = 1,
            Job = req.Job
        });

        resp.Code = 0;
        resp.Message = "OK";
        resp.Role = new RoleInfo { RoleId = roleId, Name = name, Level = 1, Job = req.Job };

        Log.Info($"创建角色 UserId:{session.UserId} RoleId:{roleId} Name:{name}");
        return resp;
    }

    /// <summary>
    /// 选角入场。加载角色 → BindRole → 加入 PlayerManager → 进入游戏。
    /// </summary>
    [MessageHandler(typeof(MessageHandler<,>))]
    public static async Task<IMessage?> SelectRoleHandler(this UserComponent self, TcpSession session, SelectRoleReq req, SelectRoleResp resp)
    {
        if (session.UserId == 0)
        {
            resp.Code = 1001;
            resp.Message = "未登录";
            return resp;
        }

        // 加载角色
        var role = await PlayerManager.Instance.LoadRoleAsync(req.RoleId);
        if (role == null)
        {
            resp.Code = 1002;
            resp.Message = "角色不存在";
            return resp;
        }

        if (role.UserId != session.UserId)
        {
            resp.Code = 1003;
            resp.Message = "不是你的角色";
            return resp;
        }

        // 绑定角色到会话
        SessionManager.Instance.BindRole(session, role.Id);

        // 更新重连Token（包含roleId）
        if (!string.IsNullOrEmpty(session.ReconnectToken))
        {
            await TokenStore.Instance.SetAsync(
                $"reconnect:{session.ReconnectToken}",
                $"{session.UserId}:{role.Id}",
                TimeSpan.FromMinutes(5));
        }

        // 创建在线玩家
        var player = new Player
        {
            UserId = session.UserId,
            RoleId = role.Id,
            RoleName = role.Name,
            Level = role.Level,
            LoginTime = DateTime.UtcNow,
            LastActiveTime = DateTime.UtcNow,
            RoleData = role
        };
        PlayerManager.Instance.AddPlayer(player);

        resp.Code = 0;
        resp.Message = "OK";
        resp.RoleId = role.Id;
        resp.Name = role.Name;
        resp.Level = role.Level;
        resp.Job = role.Job;

        Log.Info($"选角入场 UserId:{session.UserId} RoleId:{role.Id} Name:{role.Name}");
        return resp;
    }

    /// <summary>
    /// 断线重连。验证Token → 恢复会话绑定 → 重新加入在线列表。
    /// </summary>
    [MessageHandler(typeof(MessageHandler<,>))]
    public static async Task<IMessage?> ReconnectHandler(this UserComponent self, TcpSession session, ReconnectReq req, ReconnectResp resp)
    {
        // 验证 Token
        var tokenKey = $"reconnect:{req.Token}";
        var tokenStr = await TokenStore.Instance.GetAsync(tokenKey);
        if (tokenStr == null)
        {
            resp.Code = 1001;
            resp.Message = "Token无效或已过期";
            return resp;
        }

        // 解析 token: "userId" 或 "userId:roleId"
        var parts = tokenStr.Split(':');
        if (parts.Length < 1 || !long.TryParse(parts[0], out var userId))
        {
            resp.Code = 1002;
            resp.Message = "Token数据异常";
            return resp;
        }

        long roleId = 0;
        if (parts.Length >= 2) long.TryParse(parts[1], out roleId);

        // 如果指定了 roleId，以请求中的为准
        if (req.RoleId > 0) roleId = req.RoleId;

        // 绑定会话
        session.UserId = userId;
        SessionManager.Instance.BindUser(session, userId);

        resp.Code = 0;
        resp.Message = "OK";

        // 如果有角色，恢复在线状态
        if (roleId > 0)
        {
            var role = await PlayerManager.Instance.LoadRoleAsync(roleId);
            if (role != null && role.UserId == userId)
            {
                SessionManager.Instance.BindRole(session, role.Id);

                var player = new Player
                {
                    UserId = userId,
                    RoleId = role.Id,
                    RoleName = role.Name,
                    Level = role.Level,
                    LoginTime = DateTime.UtcNow,
                    LastActiveTime = DateTime.UtcNow,
                    RoleData = role
                };
                PlayerManager.Instance.AddPlayer(player);

                resp.RoleId = role.Id;
                resp.Name = role.Name;
                resp.Level = role.Level;

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

        return resp;
    }
}
