using System.Collections.Generic;
using GameplayCore;
using ProductExpedition;
using Xunit;

namespace ProductContent.Tests
{
    public sealed class ProductExpeditionRunTests
    {
        [Fact]
        public void TimedProductFailsExactlyOnceAtTickOneThousandEightHundredAndSettles()
        {
            ExpeditionCampaignSession session = CreateSession();
            ExpeditionRun run = session.StartRun();
            ProductExpeditionRun productRun = new ProductExpeditionRun(
                run,
                ProductExpeditionRun.TimedPlaceholderProductId);
            List<ExpeditionEvent> events = new List<ExpeditionEvent>();

            Advance(productRun, 1_799, events);

            Assert.False(run.HasEnded);
            Assert.Equal(1_799L, run.Tick);
            Assert.Equal(1L, productRun.RemainingRuleTicks);

            events.Clear();
            bool terminatedByProductRule = productRun.Advance(0.0f, 0.0f, events);

            Assert.True(terminatedByProductRule);
            Assert.True(run.HasEnded);
            Assert.Equal(ExpeditionPhase.Failed, run.Phase);
            Assert.NotNull(run.Result);
            Assert.Equal(1_800L, run.Result.Tick);
            Assert.Single(events, value => value.Kind == ExpeditionEventKind.Failed);

            ExpeditionResult originalResult = run.Result;
            events.Clear();
            Assert.False(productRun.Advance(0.0f, 0.0f, events));
            Assert.Same(originalResult, run.Result);
            Assert.Equal(1_800L, run.Tick);
            Assert.Empty(events);

            SettlementReceipt receipt = session.ConsumeResult(run);
            Assert.Equal(ExpeditionPhase.Failed, receipt.Phase);
            Assert.Equal(run.Result.CollectedResource, receipt.CollectedResource);
            Assert.False(session.State.HasActiveRun);
            Assert.Same(receipt, session.ConsumeResult(run));
        }

        [Fact]
        public void DefaultProductDoesNotEndAStationaryExpedition()
        {
            ExpeditionRun run = CreateSession().StartRun();
            ProductExpeditionRun productRun = new ProductExpeditionRun(
                run,
                ProductExpeditionRun.DefaultProductId);
            List<ExpeditionEvent> events = new List<ExpeditionEvent>();

            Advance(productRun, 2_000, events);

            Assert.False(productRun.HasTimeLimit);
            Assert.False(productRun.HasTimeExpired);
            Assert.False(run.HasEnded);
            Assert.Equal(2_000L, run.Tick);
        }

        [Fact]
        public void ProductChoiceIsPerCompositionAndNewRunResetsRuleTicks()
        {
            ExpeditionCampaignSession session = CreateSession();
            ExpeditionRun firstRun = session.StartRun();
            ProductExpeditionRun firstProductRun = new ProductExpeditionRun(
                firstRun,
                ProductExpeditionRun.TimedPlaceholderProductId);
            List<ExpeditionEvent> events = new List<ExpeditionEvent>();

            Advance(firstProductRun, 100, events);
            Assert.Equal(1_700L, firstProductRun.RemainingRuleTicks);

            Advance(firstProductRun, 1_700, events);
            session.ConsumeResult(firstRun);
            ExpeditionRun secondRun = session.StartRun();
            ProductExpeditionRun secondProductRun = new ProductExpeditionRun(
                secondRun,
                ProductExpeditionRun.TimedPlaceholderProductId);

            ExpeditionRun defaultRun = CreateSession().StartRun();
            ProductExpeditionRun defaultProductRun = new ProductExpeditionRun(
                defaultRun,
                ProductExpeditionRun.DefaultProductId);

            Assert.Equal(0L, secondProductRun.RuleTick);
            Assert.Equal(1_800L, secondProductRun.RemainingRuleTicks);
            Assert.False(secondRun.HasEnded);
            Assert.False(defaultProductRun.HasTimeLimit);
            Assert.Equal(ProductExpeditionRun.DefaultProductId, defaultProductRun.ProductId);
        }

        private static void Advance(
            ProductExpeditionRun productRun,
            int tickCount,
            List<ExpeditionEvent> events)
        {
            for (int tick = 0; tick < tickCount && !productRun.Run.HasEnded; tick++)
            {
                events.Clear();
                productRun.Advance(0.0f, 0.0f, events);
            }
        }

        private static ExpeditionCampaignSession CreateSession()
        {
            ExpeditionSetup template = new ExpeditionSetup
            {
                TicksPerSecond = 10,
                MoveSpeedPerSecond = 1.0f,
                MinimumX = -100.0f,
                MaximumX = 100.0f,
                MinimumY = -100.0f,
                MaximumY = 100.0f,
                InitialSquadX = 0.0f,
                InitialSquadY = 0.0f,
                TargetAcquireRadius = 2.0f,
                TargetReleaseMargin = 1.0f,
                EnemyThreatRadius = 1.0f,
                ExitX = 90.0f,
                ExitY = 90.0f,
                ExitRadius = 1.0f,
                Members = new[]
                {
                    new ExpeditionMemberSetup
                    {
                        Id = 1,
                        MaxHealth = 100,
                        AttackDamage = 1,
                        AttackRange = 1.0f,
                        AttackIntervalTicks = 10
                    }
                },
                Enemies = new[]
                {
                    new ExpeditionEnemySetup
                    {
                        Id = 100,
                        Kind = ExpeditionEnemyKind.Normal,
                        X = 80.0f,
                        Y = 80.0f,
                        MaxHealth = 10_000,
                        AttackDamage = 1,
                        AttackRange = 0.5f,
                        ActionIntervalTicks = 100,
                        BossWarningDelayTicks = 0,
                        BossWarningRadius = 0.0f
                    },
                    new ExpeditionEnemySetup
                    {
                        Id = 101,
                        Kind = ExpeditionEnemyKind.Boss,
                        X = 80.0f,
                        Y = 80.0f,
                        MaxHealth = 10_000,
                        AttackDamage = 1,
                        AttackRange = 0.5f,
                        ActionIntervalTicks = 100,
                        BossWarningDelayTicks = 10,
                        BossWarningRadius = 1.0f
                    }
                },
                Resources = new[]
                {
                    new ExpeditionResourceSetup
                    {
                        Id = 200,
                        X = 70.0f,
                        Y = 70.0f,
                        Amount = 10,
                        CollectionAmount = 1,
                        InteractionRange = 1.0f,
                        CollectionIntervalTicks = 10
                    }
                }
            };
            ExpeditionCampaignDefinition definition = new ExpeditionCampaignDefinition(
                1234u,
                6,
                30,
                50,
                20,
                template);
            return new ExpeditionCampaignSession(definition);
        }
    }
}
