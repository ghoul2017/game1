using Shared.Core.ECS;

namespace Game.Game.Player;

/// <summary>
/// 玩家在线状态组件。不持久化，仅在玩家在线期间存在。
/// </summary>
[ComponentOf(typeof(PlayerEntity))]
public class PlayerOnlineComponent : Component
{
    public long UserId { get; set; }
    public DateTime LoginTime { get; set; }
    public DateTime LastActiveTime { get; set; }
    public string LoginIp { get; set; } = "";
}
