namespace Game.Components.Bag;

/// <summary>
/// 道具添加输入，也是业务层获得展示使用的归一化数据。
/// </summary>
public sealed class BagItemAddData
{
    public int ConfigId { get; set; }
    public long Count { get; set; }
    public bool Bind { get; set; }
    public long ExpireTime { get; set; }
}
