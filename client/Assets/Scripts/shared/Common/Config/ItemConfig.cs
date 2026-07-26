using Shared.Core.Config;

namespace Shared.Common.Config
{
    /// <summary>
    /// 物品配置。对应 ItemConfig.csv。
    /// </summary>
    [ConfigTable]
    public class ItemConfig
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public long MaxStack { get; set; }
        public int Quality { get; set; }
    }
}
