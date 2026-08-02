using System;
using GameplayCore;
using Xunit;

namespace GameplayCore.Tests
{
    public sealed class ExpeditionCampaignTests
    {
        [Fact]
        public void SuccessfulResultIsBankedOnceAndImmediateRetryReturnsSameReceipt()
        {
            ExpeditionCampaignSession campaign = new ExpeditionCampaignSession(
                ExpeditionT2TestFactory.CreateCampaignDefinition());
            ExpeditionRun run = ExpeditionT2TestFactory.CompleteRun(campaign.StartRun());

            SettlementReceipt first = campaign.ConsumeResult(run);
            SettlementReceipt repeated = campaign.ConsumeResult(run);

            Assert.Same(first, repeated);
            Assert.Equal(run.CollectedResource, first.BankedResource);
            Assert.Equal(first.BalanceAfter, campaign.State.BankedResource);
            Assert.False(campaign.State.HasActiveRun);
            Assert.True(first.BalanceAfter >= ExpeditionT2TestFactory.UpgradeCost);
        }

        [Fact]
        public void FailureAfterOptionalResourceUsesRetentionAndCanStartAgain()
        {
            ExpeditionSetup template = ExpeditionT2TestFactory.CreateTemplate(
                memberDamage: 1,
                normalHealth: 1,
                bossHealth: 1000,
                bossDamage: 1000);
            ExpeditionCampaignSession campaign = new ExpeditionCampaignSession(
                ExpeditionT2TestFactory.CreateCampaignDefinition(
                    campaignSeed: 99u,
                    failureRetentionPercent: 50,
                    template: template));
            ExpeditionRun run = campaign.StartRun();
            ExploreOneOptionalBranch(run);
            ExpeditionT2TestFactory.CompleteRun(run);

            Assert.Equal(ExpeditionPhase.Failed, run.Phase);
            Assert.True(run.CollectedResource > 0L);
            SettlementReceipt receipt = campaign.ConsumeResult(run);
            Assert.Equal(run.CollectedResource * 50L / 100L, receipt.BankedResource);
            Assert.Same(receipt, campaign.ConsumeResult(run));

            ExpeditionRun next = campaign.StartRun();
            Assert.NotEqual(run.RunId, next.RunId);
            Assert.Equal(0L, next.CollectedResource);
        }

        [Fact]
        public void UpgradeRejectsActiveInsufficientAndRepeatedAttemptsWithoutMutation()
        {
            ExpeditionCampaignSession campaign = new ExpeditionCampaignSession(
                ExpeditionT2TestFactory.CreateCampaignDefinition());

            Assert.False(campaign.TryUpgradeFacility(out FacilityUpgradeReceipt? beforeRun));
            Assert.Null(beforeRun);
            ExpeditionRun run = campaign.StartRun();
            ExpeditionCampaignState activeState = campaign.State;
            Assert.False(campaign.TryUpgradeFacility(out FacilityUpgradeReceipt? whileActive));
            Assert.Null(whileActive);
            Assert.Same(activeState, campaign.State);

            ExpeditionT2TestFactory.CompleteRun(run);
            campaign.ConsumeResult(run);
            Assert.True(campaign.TryUpgradeFacility(out FacilityUpgradeReceipt? receipt));
            Assert.NotNull(receipt);
            ExpeditionCampaignState upgradedState = campaign.State;

            Assert.False(campaign.TryUpgradeFacility(out FacilityUpgradeReceipt? repeated));
            Assert.Null(repeated);
            Assert.Same(upgradedState, campaign.State);
            Assert.Equal(1, campaign.State.FacilityLevel);
            Assert.Equal(receipt!.BalanceAfter, campaign.State.BankedResource);
            Assert.True(receipt.CurrentDamage > receipt.PreviousDamage);
        }

        [Fact]
        public void SameRunIdWithDifferentResultContentIsRejectedWithoutStateChange()
        {
            ExpeditionCampaignSession campaign = new ExpeditionCampaignSession(
                ExpeditionT2TestFactory.CreateCampaignDefinition());
            ExpeditionRun accepted = ExpeditionT2TestFactory.CompleteRun(campaign.StartRun());
            campaign.ConsumeResult(accepted);
            ExpeditionCampaignState state = campaign.State;

            ExpeditionSetup template = ExpeditionT2TestFactory.CreateTemplate();
            ExpeditionRoomGraph graph = ExpeditionRoomGraphGenerator.Generate(
                9876u,
                8,
                ExpeditionT2TestFactory.UpgradeCost,
                template);
            ExpeditionRun conflicting = ExpeditionT2TestFactory.CompleteRun(
                ExpeditionRun.Start(ExpeditionT2TestFactory.CreateRunSetup(template, graph, accepted.RunId)));

            Assert.Throws<InvalidOperationException>(() => campaign.ConsumeResult(conflicting));
            Assert.Same(state, campaign.State);
        }

        private static void ExploreOneOptionalBranch(ExpeditionRun run)
        {
            bool explored = false;
            for (int room = 0; room < 10 && !explored; room++)
            {
                ExpeditionT2TestFactory.MoveThroughExit(run, ExpeditionDoorDirection.North);
                AdvanceUntilDoorsOpen(run);

                for (int index = 0; index < run.VisibleExits.Count; index++)
                {
                    ExpeditionVisibleExit exit = run.VisibleExits[index];
                    if (exit.Direction != ExpeditionDoorDirection.East &&
                        exit.Direction != ExpeditionDoorDirection.West)
                    {
                        continue;
                    }

                    ExpeditionT2TestFactory.MoveThroughExit(run, exit.Direction);
                    AdvanceUntilDoorsOpen(run);
                    ExpeditionT2TestFactory.MoveThroughExit(
                        run,
                        exit.Direction == ExpeditionDoorDirection.East
                            ? ExpeditionDoorDirection.West
                            : ExpeditionDoorDirection.East);
                    ExpeditionTestFactory.Advance(run, -run.SquadX / run.MovePerTick, 0.0f);
                    explored = true;
                    break;
                }
            }

            Assert.True(explored);
        }

        private static void AdvanceUntilDoorsOpen(ExpeditionRun run)
        {
            for (int tick = 0; tick < 20; tick++)
            {
                bool allOpen = run.VisibleExits.Count > 0;
                for (int index = 0; index < run.VisibleExits.Count; index++)
                {
                    allOpen &= run.VisibleExits[index].IsOpen;
                }

                if (allOpen)
                {
                    return;
                }

                ExpeditionTestFactory.Advance(run);
            }

            Assert.Fail("房间未在预期 Tick 内完成");
        }
    }
}
