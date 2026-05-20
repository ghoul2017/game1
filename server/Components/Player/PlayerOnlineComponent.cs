using Shared.Core.ECS;
using Shared.Core.Network;

namespace Game.Components.Player;

/// <summary>
/// 玩家在线状态组件。不持久化，仅在玩家在线期间存在。
/// </summary>
[ComponentOf(typeof(PlayerEntity))]
public class PlayerOnlineComponent : Component
{
    public long UserId { get; set; }
    public TcpSession? Session { get; set; }
    public DateTime LoginTime { get; set; }
    public DateTime LastActiveTime { get; set; }
    public string LoginIp { get; set; } = "";
}
