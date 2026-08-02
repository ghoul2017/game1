using System;
using GameplayCore;
using ProductExpedition;
using Xunit;

namespace ProductContent.Tests
{
    public sealed class ProductExpeditionFactoryTests
    {
        [Fact]
        public void DefaultProductConsumesCatalogContentAndT2Rules()
        {
            ProductProfile profile = ProductCatalog.Create(ProductExpeditionRun.DefaultProductId);
            ExpeditionSetup setup = ProductExpeditionFactory.CreateSetup(ProductExpeditionRun.DefaultProductId);
            ExpeditionCampaignDefinition campaign = ProductExpeditionFactory.CreateCampaignDefinition(
                ProductExpeditionRun.DefaultProductId);

            Assert.Equal(5.5f, setup.MoveSpeedPerSecond);
            Assert.Equal(-6.0f, setup.MinimumX);
            Assert.Equal(6.0f, setup.MaximumX);
            Assert.Equal(-6.0f, setup.MinimumY);
            Assert.Equal(6.0f, setup.MaximumY);
            Assert.Equal(profile.Content.SquadUnits.Count, setup.Members.Length);
            Assert.Equal(profile.Content.EnemyUnits.Count, setup.Enemies.Length);
            Assert.Equal(profile.Content.ResourceNodeCount, setup.Resources.Length);
            Assert.Equal(ProductExpeditionFactory.DefaultRoomCount, campaign.RoomCount);
            Assert.Equal(ProductExpeditionFactory.DefaultUpgradeCost, campaign.UpgradeCost);
            Assert.Equal(
                ProductExpeditionFactory.DefaultFailureRetentionPercent,
                campaign.FailureRetentionPercent);
            Assert.Equal(ProductExpeditionFactory.DefaultUpgradeDamagePercent, campaign.UpgradeDamagePercent);

            for (int index = 0; index < profile.Content.SquadUnits.Count; index++)
            {
                ProductUnitContent source = profile.Content.SquadUnits[index];
                ExpeditionMemberSetup actual = setup.Members[index];
                Assert.Equal(source.TemplateId, actual.Id);
                Assert.Equal(source.MaxHealth, actual.MaxHealth);
                Assert.Equal(source.AttackDamage, actual.AttackDamage);
                Assert.Equal(source.AttackIntervalTicks, actual.AttackIntervalTicks);
            }

            for (int index = 0; index < profile.Content.EnemyUnits.Count; index++)
            {
                ProductUnitContent source = profile.Content.EnemyUnits[index];
                ExpeditionEnemySetup actual = setup.Enemies[index];
                Assert.Equal(source.TemplateId, actual.Id);
                Assert.Equal(source.MaxHealth, actual.MaxHealth);
                Assert.Equal(source.AttackDamage, actual.AttackDamage);
                Assert.Equal(source.AttackIntervalTicks, actual.ActionIntervalTicks);
            }

            ExpeditionRun directRun = ExpeditionRun.Start(setup);
            Assert.Equal(profile.Content.ResourceNodeCount, directRun.Resources.Count);
        }

        [Fact]
        public void AuthoritativeRunAppliesConfiguredUpgradeAndProductTimer()
        {
            ProductExpeditionRun baseRun = ProductExpeditionFactory.CreateRun(
                ProductExpeditionRun.DefaultProductId,
                10,
                1L,
                123u,
                0);
            ProductExpeditionRun upgradedRun = ProductExpeditionFactory.CreateRun(
                ProductExpeditionRun.DefaultProductId,
                10,
                2L,
                123u,
                1);
            ProductExpeditionRun timedRun = ProductExpeditionFactory.CreateRun(
                ProductExpeditionRun.TimedPlaceholderProductId,
                10,
                3L,
                123u,
                0);

            int baseDamage = baseRun.Run.Members[0].AttackDamage;
            int expectedDamage = Math.Max(
                baseDamage + 1,
                checked((baseDamage * (100 + ProductExpeditionFactory.DefaultUpgradeDamagePercent) + 50) / 100));
            Assert.Equal(expectedDamage, upgradedRun.Run.Members[0].AttackDamage);
            Assert.False(baseRun.HasTimeLimit);
            Assert.True(timedRun.HasTimeLimit);
            Assert.Equal(1_800L, timedRun.RemainingRuleTicks);
        }

        [Fact]
        public void InvalidTickRateIsRejectedBeforeCreatingContent()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ProductExpeditionFactory.CreateSetup(ProductExpeditionRun.DefaultProductId, 0));
        }
    }
}
