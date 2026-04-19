using Shared.Core.Config;

namespace Shared.Common.Config
{
    /// <summary>
    /// 物品使用效果配置。对应 ItemEffect.csv。
    /// 物品使用后根据此配置触发对应效果。
    /// </summary>
    [ConfigTable("ItemEffect.csv", KeyName = "Id")]
    public class ItemEffectConfig
    {
        /// <summary>效果ID</summary>
        public int Id { get; set; }

        /// <summary>关联的物品ID</summary>
        public int ItemId { get; set; }

        /// <summary>效果类型（1=恢复HP, 2=恢复MP, 3=增益BUFF, 4=传送, 5=复活）</summary>
        public int EffectType { get; set; }

        /// <summary>效果值1（HP恢复量/MP恢复量/BUFFID等）</summary>
        public int Value1 { get; set; }

        /// <summary>效果值2（BUFF持续时间秒/BUFF层数等）</summary>
        public int Value2 { get; set; }

        /// <summary>冷却时间（秒），0表示无冷却</summary>
        public int Cooldown { get; set; }

        /// <summary>使用音效</summary>
        public string Sound { get; set; } = "";

        /// <summary>使用特效</summary>
        public string Effect { get; set; } = "";
    }
}
