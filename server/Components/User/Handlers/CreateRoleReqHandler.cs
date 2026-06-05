using System;
using System.Threading.Tasks;
using Game.Database;
using Shared.Common.Protos;
using Shared.Core;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.User;

public sealed class CreateRoleReqHandler : RpcMessageHandler<CreateRoleReq, CreateRoleRes>
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(CreateRoleReqHandler));

    protected override async Task ExecuteAsync(TcpSession session, CreateRoleReq req, CreateRoleRes res)
    {
        if (session.UserId == 0)
        {
            res.ErrorMessage = "未登录";
            return;
        }

        var name = req.Name.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 12)
        {
            res.ErrorMessage = "角色名长度不合法（1-12字符）";
            return;
        }

        var existing = await MongoDb.Instance.QueryAsync<UserRole>(r => r.UserId == session.UserId);
        if (existing.Count >= UserComponent.MaxRolesPerUser)
        {
            res.ErrorMessage = $"最多创建{UserComponent.MaxRolesPerUser}个角色";
            return;
        }

        if (await MongoDb.Instance.AnyAsync<UserRole>(r => r.Name == name))
        {
            res.ErrorMessage = "角色名已存在";
            return;
        }

        var roleId = IdGenerator.Instance.NextId();
        await MongoDb.Instance.InsertAsync(new UserRole
        {
            RoleId = roleId,
            UserId = session.UserId,
            Name = name,
            Level = 1,
            Job = req.Job
        });

        res.Role = new RoleInfo { RoleId = roleId, Name = name, Level = 1, Job = req.Job };

        Log.Info($"创建角色 UserId:{session.UserId} RoleId:{roleId} Name:{name}");
    }
}
