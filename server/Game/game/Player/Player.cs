using System;

namespace Game.Game.Player;

/// <summary>
/// 在线玩家运行时模型。不持久化，仅在玩家在线期间存在于内存中。
/// 持久化数据通过 Role 实体管理。
/// </summary>
public class Player
{
    public long UserId { get; init; }
    public long RoleId { get; init; }
    public string RoleName { get; set; } = string.Empty;
    public int Level { get; set; }
    public int ServerId { get; init; }
    public DateTime LoginTime { get; init; }
    public string LoginIp { get; init; } = string.Empty;

    /// <summary>上次活跃时间（收到消息时更新）</summary>
    public DateTime LastActiveTime { get; set; }

    /// <summary>关联的持久化角色数据（登录时加载）</summary>
    public Role? RoleData { get; set; }
}
