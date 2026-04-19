using ProtoBuf;

namespace Game.Game.Bag;

/// <summary>
/// 背包物品。每个格子一个实例。
/// </summary>
[ProtoContract]
public class BagItem
{
    /// <summary>物品唯一ID（运行时生成）</summary>
    [ProtoMember(1)]
    public long Uid { get; set; }

    /// <summary>配置表ID（对应 ItemConfig.Id）</summary>
    [ProtoMember(2)]
    public int ConfigId { get; set; }

    /// <summary>数量</summary>
    [ProtoMember(3)]
    public int Count { get; set; }

    /// <summary>格子索引</summary>
    [ProtoMember(4)]
    public int Index { get; set; }

    /// <summary>是否绑定</summary>
    [ProtoMember(5)]
    public bool Bind { get; set; }

    /// <summary>过期时间（Unix毫秒，0=永不过期）</summary>
    [ProtoMember(6)]
    public long ExpireTime { get; set; }
}
