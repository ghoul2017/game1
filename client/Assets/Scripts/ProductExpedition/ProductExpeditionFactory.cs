using System;
using System.Collections.Generic;
using GameplayCore;
using ProductContent;

namespace ProductExpedition
{
    /// <summary>
    /// 将产品内容与产品中立的远征规则组合为唯一的本地/服务端开局配置。
    /// </summary>
    public static class ProductExpeditionFactory
    {
        public const int DefaultTicksPerSecond = ProductCatalog.AuthoredTicksPerSecond;
        public const int DefaultFailureRetentionPercent = 100;
        public const int DefaultRoomCount = 7;
        public const uint DefaultSeed = 0xB41A2026u;
        public const int DefaultUpgradeCost = 30;
        public const int DefaultUpgradeDamagePercent = 25;

        public static ExpeditionSetup CreateSetup(string productId)
        {
            return CreateSetup(productId, DefaultTicksPerSecond);
        }

        public static ExpeditionSetup CreateSetup(string productId, int ticksPerSecond)
        {
            if (ticksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            }

            ProductProfile profile = ProductCatalog.Create(productId);
            ProductContentPack content = profile.Content;
            return new ExpeditionSetup
            {
                TicksPerSecond = ticksPerSecond,
                MoveSpeedPerSecond = 5.5f,
                MinimumX = -6.0f,
                MaximumX = 6.0f,
                MinimumY = -6.0f,
                MaximumY = 6.0f,
                InitialSquadX = 0.0f,
                InitialSquadY = 0.0f,
                TargetAcquireRadius = 4.5f,
                TargetReleaseMargin = 1.5f,
                EnemyThreatRadius = 3.5f,
                ExitX = 0.0f,
                ExitY = 5.0f,
                ExitRadius = 1.0f,
                Members = CreateMembers(content.SquadUnits, ticksPerSecond),
                Enemies = CreateEnemies(content.EnemyUnits, ticksPerSecond),
                Resources = CreateResources(content.ResourceNodeCount, ticksPerSecond)
            };
        }

        public static ExpeditionCampaignDefinition CreateCampaignDefinition(string productId)
        {
            return CreateCampaignDefinition(productId, DefaultTicksPerSecond);
        }

        public static ExpeditionCampaignDefinition CreateCampaignDefinition(
            string productId,
            int ticksPerSecond)
        {
            return new ExpeditionCampaignDefinition(
                DefaultSeed,
                DefaultRoomCount,
                DefaultUpgradeCost,
                DefaultFailureRetentionPercent,
                DefaultUpgradeDamagePercent,
                CreateSetup(productId, ticksPerSecond));
        }

        public static ProductExpeditionRun CreateRun(
            string productId,
            int ticksPerSecond,
            long runId,
            uint seed,
            int facilityLevel)
        {
            ExpeditionCampaignDefinition campaign = CreateCampaignDefinition(productId, ticksPerSecond);
            ExpeditionRun run = campaign.CreateRun(runId, seed, facilityLevel);
            return new ProductExpeditionRun(run, productId);
        }

        public static int GetRepresentativeDamage(string productId, int facilityLevel)
        {
            ExpeditionCampaignDefinition campaign = CreateCampaignDefinition(productId);
            return campaign.GetRepresentativeDamage(facilityLevel);
        }

        private static ExpeditionMemberSetup[] CreateMembers(
            IReadOnlyList<ProductUnitContent> units,
            int ticksPerSecond)
        {
            ExpeditionMemberSetup[] result = new ExpeditionMemberSetup[units.Count];
            for (int index = 0; index < units.Count; index++)
            {
                ProductUnitContent unit = units[index];
                result[index] = new ExpeditionMemberSetup
                {
                    Id = unit.TemplateId,
                    MaxHealth = unit.MaxHealth,
                    AttackDamage = unit.AttackDamage,
                    AttackRange = 3.2f,
                    AttackIntervalTicks = ScaleAuthoredTicks(unit.AttackIntervalTicks, ticksPerSecond)
                };
            }

            return result;
        }

        private static ExpeditionEnemySetup[] CreateEnemies(
            IReadOnlyList<ProductUnitContent> units,
            int ticksPerSecond)
        {
            int resultCount = Math.Max(2, units.Count);
            ExpeditionEnemySetup[] result = new ExpeditionEnemySetup[resultCount];
            int normalCount = resultCount - 1;
            for (int index = 0; index < normalCount; index++)
            {
                ProductUnitContent unit = units[Math.Min(index, units.Count - 1)];
                result[index] = new ExpeditionEnemySetup
                {
                    Id = unit.TemplateId,
                    Kind = ExpeditionEnemyKind.Normal,
                    X = GetDistributedX(index, normalCount, 2.0f),
                    Y = index % 2 == 0 ? 0.0f : 1.5f,
                    MaxHealth = unit.MaxHealth,
                    AttackDamage = unit.AttackDamage,
                    AttackRange = 2.5f,
                    ActionIntervalTicks = ScaleAuthoredTicks(unit.AttackIntervalTicks, ticksPerSecond),
                    BossWarningDelayTicks = 0,
                    BossWarningRadius = 0.0f
                };
            }

            ProductUnitContent bossUnit = units[units.Count - 1];
            int bossId = units.Count == 1 ? checked(bossUnit.TemplateId + 1_000_000) : bossUnit.TemplateId;
            result[result.Length - 1] = new ExpeditionEnemySetup
            {
                Id = bossId,
                Kind = ExpeditionEnemyKind.Boss,
                X = 0.0f,
                Y = 1.5f,
                MaxHealth = bossUnit.MaxHealth,
                AttackDamage = bossUnit.AttackDamage,
                AttackRange = 4.5f,
                ActionIntervalTicks = ScaleAuthoredTicks(bossUnit.AttackIntervalTicks, ticksPerSecond),
                BossWarningDelayTicks = ticksPerSecond,
                BossWarningRadius = 2.0f
            };
            return result;
        }

        private static ExpeditionResourceSetup[] CreateResources(int count, int ticksPerSecond)
        {
            ExpeditionResourceSetup[] result = new ExpeditionResourceSetup[count];
            for (int index = 0; index < count; index++)
            {
                result[index] = new ExpeditionResourceSetup
                {
                    Id = 5001 + index,
                    X = GetDistributedX(index, count, 4.0f),
                    Y = 1.5f,
                    Amount = 30,
                    CollectionAmount = 10,
                    InteractionRange = 3.0f,
                    CollectionIntervalTicks = Math.Max(1, ticksPerSecond / 3)
                };
            }

            return result;
        }

        private static float GetDistributedX(int index, int count, float span)
        {
            return count <= 1 ? 0.0f : -span * 0.5f + span * index / (count - 1);
        }

        private static int ScaleAuthoredTicks(int authoredTicks, int ticksPerSecond)
        {
            long scaled = checked((long)authoredTicks * ticksPerSecond + DefaultTicksPerSecond / 2);
            return Math.Max(1, checked((int)(scaled / DefaultTicksPerSecond)));
        }
    }
}
