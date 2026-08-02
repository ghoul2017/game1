using System.Collections.Generic;
using GameplayCore;
using Xunit;

namespace GameplayCore.Tests
{
    public sealed class ExpeditionRoomProgressionTests
    {
        [Fact]
        public void PublicProjectionContainsOnlyDiscoveredRooms()
        {
            ExpeditionCampaignSession campaign = new ExpeditionCampaignSession(
                ExpeditionT2TestFactory.CreateCampaignDefinition());
            ExpeditionRun run = campaign.StartRun();

            Assert.Single(run.DiscoveredRooms);
            Assert.Equal(ExpeditionRoomKind.Start, run.DiscoveredRooms[0].Kind);
            Assert.True(run.DiscoveredRooms[0].IsCurrent);
            Assert.Single(run.VisibleExits);
            Assert.True(run.VisibleExits[0].IsOpen);

            List<ExpeditionEvent> events = ExpeditionT2TestFactory.MoveThroughExit(
                run,
                ExpeditionDoorDirection.North);

            Assert.Equal(2, run.DiscoveredRooms.Count);
            Assert.Equal(ExpeditionRoomKind.Combat, run.CurrentRoomKind);
            Assert.Contains(events, value => value.Kind == ExpeditionEventKind.RoomDiscovered);
            Assert.Contains(events, value => value.Kind == ExpeditionEventKind.RoomChanged);
            Assert.Contains(events, value => value.Kind == ExpeditionEventKind.RouteMarked);
        }

        [Fact]
        public void LockedDoorDoesNotChangeRoomBeforeCompletion()
        {
            ExpeditionSetup template = ExpeditionT2TestFactory.CreateTemplate(
                memberDamage: 1,
                memberRange: 0.1f,
                normalHealth: 100,
                normalRange: 0.1f,
                bossHealth: 100);
            ExpeditionCampaignSession campaign = new ExpeditionCampaignSession(
                ExpeditionT2TestFactory.CreateCampaignDefinition(template: template));
            ExpeditionRun run = campaign.StartRun();
            ExpeditionT2TestFactory.MoveThroughExit(run, ExpeditionDoorDirection.North);
            int combatRoomId = run.CurrentRoomId;

            for (int tick = 0; tick < 10; tick++)
            {
                ExpeditionTestFactory.Advance(run, 0.0f, 1.0f);
            }

            Assert.Equal(combatRoomId, run.CurrentRoomId);
            Assert.All(run.VisibleExits, value => Assert.False(value.IsOpen));
        }

        [Fact]
        public void BacktrackingToClearedRoomDoesNotRespawnContentOrRewards()
        {
            ExpeditionCampaignSession campaign = new ExpeditionCampaignSession(
                ExpeditionT2TestFactory.CreateCampaignDefinition());
            ExpeditionRun run = campaign.StartRun();
            ExpeditionT2TestFactory.MoveThroughExit(run, ExpeditionDoorDirection.North);
            AdvanceUntilDoorsOpen(run);
            int clearedRoomId = run.CurrentRoomId;
            int killsAfterClear = run.Kills;
            long resourceAfterClear = run.CollectedResource;
            Assert.All(run.Enemies, enemy => Assert.False(enemy.IsAlive));

            ExpeditionT2TestFactory.MoveThroughExit(run, ExpeditionDoorDirection.North);
            AdvanceUntilDoorsOpen(run);
            int killsBeforeBacktrack = run.Kills;
            long resourceBeforeBacktrack = run.CollectedResource;
            ExpeditionT2TestFactory.MoveThroughExit(run, ExpeditionDoorDirection.South);

            Assert.Equal(clearedRoomId, run.CurrentRoomId);
            Assert.All(run.Enemies, enemy => Assert.False(enemy.IsAlive));
            Assert.True(killsBeforeBacktrack >= killsAfterClear);
            Assert.True(resourceBeforeBacktrack >= resourceAfterClear);
            Assert.Equal(killsBeforeBacktrack, run.Kills);
            Assert.Equal(resourceBeforeBacktrack, run.CollectedResource);
        }

        [Fact]
        public void MainRouteEmitsOneRuleDrivenDangerHintBeforeBoss()
        {
            ExpeditionCampaignSession campaign = new ExpeditionCampaignSession(
                ExpeditionT2TestFactory.CreateCampaignDefinition());
            ExpeditionRun run = campaign.StartRun();
            int dangerHintCount = 0;

            for (int tick = 0; tick < 100 && !run.HasEnded; tick++)
            {
                List<ExpeditionEvent> events = ExpeditionTestFactory.Advance(run, 0.0f, 1.0f);
                for (int index = 0; index < events.Count; index++)
                {
                    if (events[index].Kind == ExpeditionEventKind.DangerHint)
                    {
                        dangerHintCount++;
                    }
                }
            }

            Assert.Equal(1, dangerHintCount);
            Assert.Equal(ExpeditionPhase.Completed, run.Phase);
        }

        [Fact]
        public void TwoRunsSharingGraphDoNotShareMutableRoomProgress()
        {
            ExpeditionSetup template = ExpeditionT2TestFactory.CreateTemplate();
            ExpeditionRoomGraph graph = ExpeditionRoomGraphGenerator.Generate(
                77u,
                8,
                ExpeditionT2TestFactory.UpgradeCost,
                template);
            ExpeditionRun first = ExpeditionRun.Start(
                ExpeditionT2TestFactory.CreateRunSetup(template, graph, 1L));
            ExpeditionRun second = ExpeditionRun.Start(
                ExpeditionT2TestFactory.CreateRunSetup(template, graph, 2L));

            ExpeditionT2TestFactory.MoveThroughExit(first, ExpeditionDoorDirection.North);

            Assert.Equal(2, first.DiscoveredRooms.Count);
            Assert.Single(second.DiscoveredRooms);
            Assert.Equal(ExpeditionRoomKind.Start, second.CurrentRoomKind);
            Assert.Equal(0L, second.Tick);
        }

        private static void AdvanceUntilDoorsOpen(ExpeditionRun run)
        {
            for (int tick = 0; tick < 20; tick++)
            {
                bool open = run.VisibleExits.Count > 0;
                for (int index = 0; index < run.VisibleExits.Count; index++)
                {
                    open &= run.VisibleExits[index].IsOpen;
                }

                if (open)
                {
                    return;
                }

                ExpeditionTestFactory.Advance(run);
            }

            Assert.Fail("房间未在预期 Tick 内完成");
        }
    }
}
