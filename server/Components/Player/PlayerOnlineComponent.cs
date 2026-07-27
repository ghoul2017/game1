using MongoDB.Bson.Serialization.Attributes;
using Shared.Core.ECS;
using Shared.Core.Network;

namespace Game.Components.Player;

/// <summary>
/// 玩家会话元数据组件。除 Session 外随 PlayerEntity 完整文档保存。
/// </summary>
[ComponentOf(typeof(PlayerEntity))]
public class PlayerOnlineComponent : Component
{
    public long UserId { get; set; }

    [BsonIgnore]
    public TcpSession? Session { get; set; }
    public DateTime LoginTime { get; set; }
    public DateTime LastActiveTime { get; set; }
    public string LoginIp { get; set; } = "";
}
