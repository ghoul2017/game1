namespace Game.Components.Bag;

/// <summary>
/// 背包物品实例栈。
/// </summary>
public class BagItem
{
    /// <summary>物品唯一ID（运行时生成）</summary>
    public long Uid { get; set; }

    /// <summary>配置表ID</summary>
    public int ConfigId { get; set; }

    /// <summary>数量</summary>
    public long Count { get; set; }

    /// <summary>是否绑定</summary>
    public bool Bind { get; set; }

    /// <summary>当前只作为不透明堆叠元数据，不参与有效性判断。</summary>
    public long ExpireTime { get; set; }
}
