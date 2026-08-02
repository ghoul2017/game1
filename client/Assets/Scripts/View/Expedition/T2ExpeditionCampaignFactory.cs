using GameplayCore;
using ProductExpedition;

namespace Client.Expedition
{
    /// <summary>
    /// 保留 T2 公共入口，并委托产品组合层创建与服务端同源的战役参数。
    /// </summary>
    public static class T2ExpeditionCampaignFactory
    {
        public const int DefaultFailureRetentionPercent = ProductExpeditionFactory.DefaultFailureRetentionPercent;
        public const int DefaultRoomCount = ProductExpeditionFactory.DefaultRoomCount;
        public const uint DefaultSeed = ProductExpeditionFactory.DefaultSeed;
        public const int DefaultUpgradeCost = ProductExpeditionFactory.DefaultUpgradeCost;
        public const int DefaultUpgradeDamagePercent = ProductExpeditionFactory.DefaultUpgradeDamagePercent;

        public static ExpeditionCampaignDefinition Create()
        {
            return Create(ProductExpeditionFactory.DefaultTicksPerSecond);
        }

        public static ExpeditionCampaignDefinition Create(int ticksPerSecond)
        {
            return ProductExpeditionFactory.CreateCampaignDefinition(
                ProductExpeditionRun.DefaultProductId,
                ticksPerSecond);
        }
    }
}
