namespace Game.Components.Bag;

/// <summary>
/// 背包物品。每个格子一个实例。
/// </summary>
public class BagItem
{
    /// <summary>物品唯一ID（运行时生成）</summary>
    public long Uid { get; set; }

    /// <summary>配置表ID</summary>
    public int ConfigId { get; set; }

    /// <summary>数量</summary>
    public int Count { get; set; }

    /// <summary>格子索引</summary>
    public int Index { get; set; }

    /// <summary>是否绑定</summary>
    public bool Bind { get; set; }

    /// <summary>过期时间（Unix毫秒，0=永不过期）</summary>
    public long ExpireTime { get; set; }
}
