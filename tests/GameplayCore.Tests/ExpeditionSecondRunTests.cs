using System;
using System.Collections.Generic;
using GameplayCore;
using Xunit;

namespace GameplayCore.Tests
{
    public sealed class ExpeditionSecondRunTests
    {
        [Fact]
        public void SecondRunUsesNewIdentityResetsRunStateAndKeepsUpgrade()
        {
            ExpeditionCampaignSession campaign = new ExpeditionCampaignSession(
                ExpeditionT2TestFactory.CreateCampaignDefinition());
            ExpeditionRun first = ExpeditionT2TestFactory.CompleteRun(campaign.StartRun());
            int firstDamage = first.Members[0].AttackDamage;
            campaign.ConsumeResult(first);
            Assert.True(campaign.TryUpgradeFacility(out FacilityUpgradeReceipt? upgrade));
            Assert.NotNull(upgrade);
            long balanceAfterUpgrade = campaign.State.BankedResource;

            ExpeditionRun second = campaign.StartRun();

            Assert.NotEqual(first.RunId, second.RunId);
            Assert.Equal(0L, second.Tick);
            Assert.Equal(0L, second.CollectedResource);
            Assert.Equal(ExpeditionPhase.Running, second.Phase);
            Assert.Equal(ExpeditionRoomKind.Start, second.CurrentRoomKind);
            Assert.Single(second.DiscoveredRooms);
            Assert.All(second.Members, member => Assert.Equal(member.MaxHealth, member.Health));
            Assert.True(second.Members[0].AttackDamage > firstDamage);
            Assert.Equal(1, campaign.State.FacilityLevel);
            Assert.Equal(balanceAfterUpgrade, campaign.State.BankedResource);
        }

        [Fact]
        public void SecondRunFirstLegalAttackUsesUpgradedDamageAndFirstRunStaysFrozen()
        {
            ExpeditionCampaignSession campaign = new ExpeditionCampaignSession(
                ExpeditionT2TestFactory.CreateCampaignDefinition());
            ExpeditionRun first = ExpeditionT2TestFactory.CompleteRun(campaign.StartRun());
            long firstTick = first.Tick;
            int firstKills = first.Kills;
            campaign.ConsumeResult(first);
            Assert.True(campaign.TryUpgradeFacility(out FacilityUpgradeReceipt? upgrade));
            ExpeditionRun second = campaign.StartRun();

            List<ExpeditionEvent> events = ExpeditionT2TestFactory.MoveThroughExit(
                second,
                ExpeditionDoorDirection.North);
            ExpeditionEvent attack = Assert.Single(
                events,
                value => value.Kind == ExpeditionEventKind.MemberAttacked && value.SourceId == 1);

            Assert.NotNull(upgrade);
            Assert.Equal(upgrade!.CurrentDamage, attack.Value);
            Assert.Equal(second.Members[0].AttackDamage, attack.Value);
            Assert.Equal(firstTick, first.Tick);
            Assert.Equal(firstKills, first.Kills);
            Assert.Equal(ExpeditionPhase.Completed, first.Phase);
        }

        [Fact]
        public void TwoCompletedRunsUseDifferentSeedsAndOldReceiptStopsBeingLatest()
        {
            ExpeditionCampaignSession campaign = new ExpeditionCampaignSession(
                ExpeditionT2TestFactory.CreateCampaignDefinition());
            ExpeditionRun first = ExpeditionT2TestFactory.CompleteRun(campaign.StartRun());
            SettlementReceipt firstReceipt = campaign.ConsumeResult(first);
            Assert.True(campaign.TryUpgradeFacility(out FacilityUpgradeReceipt? ignored));
            Assert.NotNull(ignored);
            ExpeditionRun second = ExpeditionT2TestFactory.CompleteRun(campaign.StartRun());
            SettlementReceipt secondReceipt = campaign.ConsumeResult(second);

            Assert.NotEqual(first.Result.Seed, second.Result.Seed);
            Assert.NotEqual(firstReceipt.RunId, secondReceipt.RunId);
            Assert.Same(secondReceipt, campaign.State.LatestSettlement);
            Assert.Throws<InvalidOperationException>(() => campaign.ConsumeResult(first));
        }
    }
}
