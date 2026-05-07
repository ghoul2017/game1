using Shared.Core.ECS;

namespace Game.Game.Player;

/// <summary>
/// 玩家实体。Id = RoleId。在线玩家的运行时容器。
/// </summary>
public class PlayerEntity : Entity
{
    /// <summary>在线状态组件</summary>
    public PlayerOnlineComponent Online => GetComponent<PlayerOnlineComponent>();

    /// <summary>角色持久化数据组件</summary>
    public RoleDataComponent RoleData => GetComponent<RoleDataComponent>();

    /// <summary>创建玩家实体（选角入场时调用）</summary>
    public static PlayerEntity Create(long roleId, long userId, Role role)
    {
        var entity = CreateEntityById<PlayerEntity>(roleId);
        var online = entity.AddComponent<PlayerOnlineComponent>();
        online.UserId = userId;
        online.LoginTime = DateTime.UtcNow;
        online.LastActiveTime = DateTime.UtcNow;

        var roleData = entity.AddComponent<RoleDataComponent>();
        roleData.Role = role;

        return entity;
    }
}
