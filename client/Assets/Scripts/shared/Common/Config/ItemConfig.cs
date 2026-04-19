using Shared.Core.Config;

namespace Shared.Common.Config
{
    /// <summary>
    /// 物品配置。对应 Item.csv。
    /// </summary>
    [ConfigTable("Item.csv")]
    public class ItemConfig
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public int MaxStack { get; set; }
        public int Quality { get; set; }
    }
}
