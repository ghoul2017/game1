using System.Collections.Generic;
using GameplayCore;
using Xunit;

namespace GameplayCore.Tests
{
    /// <summary>
    /// 验证出口、胜负、重开和相同输入重放的一致性。
    /// </summary>
    public sealed class ExpeditionLifecycleTests
    {
        [Fact]
        public void ExitRemainsClosedWhileBossLives()
        {
            ExpeditionRun run = ExpeditionRun.Start(ExpeditionTestFactory.CreateSetup());

            List<ExpeditionEvent> events = ExpeditionTestFactory.Advance(run);

            Assert.False(run.ExitOpen);
            Assert.Equal(0, CountEvents(events, ExpeditionEventKind.ExitOpened));
        }

        [Fact]
        public void KillingBossAtExitOpensExitAndCompletesOnce()
        {
            ExpeditionSetup setup = ExpeditionTestFactory.CreateSetup();
            setup.ExitX = 0.0f;
            setup.Enemies = new[]
            {
                ExpeditionTestFactory.CreateBoss(x: 0.0f, maxHealth: 3, warningDelayTicks: 1)
            };
            ExpeditionRun run = ExpeditionRun.Start(setup);

            List<ExpeditionEvent> firstTick = ExpeditionTestFactory.Advance(run);
            long terminalTick = run.Tick;
            List<ExpeditionEvent> afterEnd = ExpeditionTestFactory.Advance(run);

            Assert.Equal(ExpeditionPhase.Completed, run.Phase);
            Assert.True(run.ExitOpen);
            Assert.Equal(1, run.Kills);
            AssertEventOrder(
                firstTick,
                ExpeditionEventKind.EnemyDied,
                ExpeditionEventKind.ExitOpened,
                ExpeditionEventKind.Completed);
            AssertEvent(firstTick, ExpeditionEventKind.EnemyDied, 1L, 3, 900);
            AssertEvent(firstTick, ExpeditionEventKind.ExitOpened, 1L, 0, 0);
            AssertEvent(firstTick, ExpeditionEventKind.Completed, 1L, 0, 0);
            Assert.Empty(afterEnd);
            Assert.Equal(terminalTick, run.Tick);
        }

        [Fact]
        public void AllMemberDeathsFailOnceAndStopProgress()
        {
            ExpeditionSetup setup = ExpeditionTestFactory.CreateSetup();
            setup.Enemies = new[]
            {
                ExpeditionTestFactory.CreateBoss(
                    x: 0.0f,
                    attackDamage: 100,
                    maxHealth: 1000,
                    actionIntervalTicks: 10,
                    warningDelayTicks: 1)
            };
            ExpeditionRun run = ExpeditionRun.Start(setup);

            ExpeditionTestFactory.Advance(run);
            List<ExpeditionEvent> secondTick = ExpeditionTestFactory.Advance(run);
            long terminalTick = run.Tick;
            List<ExpeditionEvent> afterEnd = ExpeditionTestFactory.Advance(run);

            Assert.Equal(ExpeditionPhase.Failed, run.Phase);
            Assert.Equal(1, CountEvents(secondTick, ExpeditionEventKind.Failed));
            AssertEvent(secondTick, ExpeditionEventKind.Failed, 2L, 0, 0);
            Assert.Empty(afterEnd);
            Assert.Equal(terminalTick, run.Tick);
        }

        [Fact]
        public void BossDeathAndLastMemberDeathInSameTickOpenExitThenFail()
        {
            ExpeditionSetup setup = ExpeditionTestFactory.CreateSetup();
            setup.Members = new[]
            {
                ExpeditionTestFactory.CreateMember(1, maxHealth: 1, attackDamage: 1)
            };
            setup.Enemies = new[]
            {
                ExpeditionTestFactory.CreateNormalEnemy(
                    1,
                    6.0f,
                    attackDamage: 1,
                    attackRange: 10.0f),
                ExpeditionTestFactory.CreateBoss(x: 0.0f, maxHealth: 1, warningDelayTicks: 1)
            };
            ExpeditionRun run = ExpeditionRun.Start(setup);

            List<ExpeditionEvent> terminalEvents = ExpeditionTestFactory.Advance(run);
            long terminalTick = run.Tick;
            List<ExpeditionEvent> afterEnd = ExpeditionTestFactory.Advance(run);

            Assert.Equal(ExpeditionPhase.Failed, run.Phase);
            Assert.True(run.ExitOpen);
            Assert.Equal(0, CountEvents(terminalEvents, ExpeditionEventKind.Completed));
            Assert.Equal(1, CountEvents(terminalEvents, ExpeditionEventKind.Failed));
            int bossDeathIndex = FindEventIndex(terminalEvents, ExpeditionEventKind.EnemyDied);
            int memberDeathIndex = FindEventIndex(terminalEvents, ExpeditionEventKind.MemberDied);
            int exitOpenedIndex = FindEventIndex(terminalEvents, ExpeditionEventKind.ExitOpened);
            int failedIndex = FindEventIndex(terminalEvents, ExpeditionEventKind.Failed);
            Assert.True(bossDeathIndex >= 0);
            Assert.True(bossDeathIndex < memberDeathIndex);
            Assert.True(memberDeathIndex < exitOpenedIndex);
            Assert.True(exitOpenedIndex < failedIndex);
            Assert.Empty(afterEnd);
            Assert.Equal(terminalTick, run.Tick);
        }

        [Fact]
        public void StartingNewRunDoesNotReusePreviousState()
        {
            ExpeditionSetup setup = ExpeditionTestFactory.CreateSetup();
            setup.Resources = new[]
            {
                ExpeditionTestFactory.CreateResource(x: 0.0f, amount: 3, collectionAmount: 2)
            };
            ExpeditionRun firstRun = ExpeditionRun.Start(setup);

            ExpeditionTestFactory.Advance(firstRun);
            ExpeditionRun secondRun = ExpeditionRun.Start(setup);

            Assert.Equal(2L, firstRun.CollectedResource);
            Assert.Equal(0L, secondRun.Tick);
            Assert.Equal(0L, secondRun.CollectedResource);
            Assert.Equal(3, secondRun.Resources[0].RemainingAmount);
            Assert.Equal(ExpeditionTargetKind.None, secondRun.TargetKind);
            Assert.Equal(ExpeditionPhase.Running, secondRun.Phase);
            Assert.False(secondRun.ExitOpen);
        }

        [Fact]
        public void SameSetupAndCommandStreamProduceSameStateAndEvents()
        {
            ExpeditionSetup setup = ExpeditionTestFactory.CreateSetup();
            setup.Enemies = new[]
            {
                ExpeditionTestFactory.CreateNormalEnemy(10, 2.0f, maxHealth: 4, attackRange: 1.0f),
                ExpeditionTestFactory.CreateBoss(x: 5.0f, maxHealth: 100)
            };
            setup.Resources = new[]
            {
                ExpeditionTestFactory.CreateResource(x: -2.0f)
            };
            ExpeditionRun firstRun = ExpeditionRun.Start(setup);
            ExpeditionRun secondRun = ExpeditionRun.Start(setup);
            List<ExpeditionEvent> firstEvents = new List<ExpeditionEvent>();
            List<ExpeditionEvent> secondEvents = new List<ExpeditionEvent>();
            (float X, float Y)[] commands =
            {
                (1.0f, 0.0f),
                (0.0f, 0.0f),
                (1.0f, 0.0f),
                (1.0f, 0.0f),
                (0.0f, 1.0f)
            };

            for (int index = 0; index < commands.Length; index++)
            {
                ExpeditionSimulation.Advance(firstRun, commands[index].X, commands[index].Y, firstEvents);
                ExpeditionSimulation.Advance(secondRun, commands[index].X, commands[index].Y, secondEvents);
            }

            AssertEventsEqual(firstEvents, secondEvents);
            AssertRunsEqual(firstRun, secondRun);
        }

        private static void AssertEventOrder(
            List<ExpeditionEvent> events,
            ExpeditionEventKind first,
            ExpeditionEventKind second,
            ExpeditionEventKind third)
        {
            int firstIndex = FindEventIndex(events, first);
            int secondIndex = FindEventIndex(events, second);
            int thirdIndex = FindEventIndex(events, third);
            Assert.True(firstIndex >= 0);
            Assert.True(firstIndex < secondIndex);
            Assert.True(secondIndex < thirdIndex);
        }

        private static int FindEventIndex(List<ExpeditionEvent> events, ExpeditionEventKind kind)
        {
            for (int index = 0; index < events.Count; index++)
            {
                if (events[index].Kind == kind)
                {
                    return index;
                }
            }

            return -1;
        }

        private static int CountEvents(List<ExpeditionEvent> events, ExpeditionEventKind kind)
        {
            int count = 0;
            for (int index = 0; index < events.Count; index++)
            {
                if (events[index].Kind == kind)
                {
                    count++;
                }
            }

            return count;
        }

        private static void AssertEvent(
            List<ExpeditionEvent> events,
            ExpeditionEventKind kind,
            long tick,
            int sourceId,
            int targetId)
        {
            int count = 0;
            for (int index = 0; index < events.Count; index++)
            {
                ExpeditionEvent current = events[index];
                if (current.Kind == kind)
                {
                    Assert.Equal(tick, current.Tick);
                    Assert.Equal(sourceId, current.SourceId);
                    Assert.Equal(targetId, current.TargetId);
                    count++;
                }
            }

            Assert.Equal(1, count);
        }

        private static void AssertEventsEqual(
            List<ExpeditionEvent> expected,
            List<ExpeditionEvent> actual)
        {
            Assert.Equal(expected.Count, actual.Count);
            for (int index = 0; index < expected.Count; index++)
            {
                Assert.Equal(expected[index], actual[index]);
            }
        }

        private static void AssertRunsEqual(ExpeditionRun expected, ExpeditionRun actual)
        {
            Assert.Equal(expected.Tick, actual.Tick);
            Assert.Equal(expected.SquadX, actual.SquadX);
            Assert.Equal(expected.SquadY, actual.SquadY);
            Assert.Equal(expected.Phase, actual.Phase);
            Assert.Equal(expected.TargetKind, actual.TargetKind);
            Assert.Equal(expected.TargetId, actual.TargetId);
            Assert.Equal(expected.Kills, actual.Kills);
            Assert.Equal(expected.CollectedResource, actual.CollectedResource);
            Assert.Equal(expected.ExitOpen, actual.ExitOpen);

            Assert.Equal(expected.Members.Count, actual.Members.Count);
            for (int index = 0; index < expected.Members.Count; index++)
            {
                Assert.Equal(expected.Members[index].Id, actual.Members[index].Id);
                Assert.Equal(expected.Members[index].Health, actual.Members[index].Health);
                Assert.Equal(
                    expected.Members[index].AttackCooldownTicksRemaining,
                    actual.Members[index].AttackCooldownTicksRemaining);
            }

            Assert.Equal(expected.Enemies.Count, actual.Enemies.Count);
            for (int index = 0; index < expected.Enemies.Count; index++)
            {
                Assert.Equal(expected.Enemies[index].Id, actual.Enemies[index].Id);
                Assert.Equal(expected.Enemies[index].Health, actual.Enemies[index].Health);
                Assert.Equal(expected.Enemies[index].WarningActive, actual.Enemies[index].WarningActive);
                Assert.Equal(expected.Enemies[index].WarningResolveTick, actual.Enemies[index].WarningResolveTick);
                Assert.Equal(
                    expected.Enemies[index].ActionCooldownTicksRemaining,
                    actual.Enemies[index].ActionCooldownTicksRemaining);
            }

            Assert.Equal(expected.Resources.Count, actual.Resources.Count);
            for (int index = 0; index < expected.Resources.Count; index++)
            {
                Assert.Equal(expected.Resources[index].Id, actual.Resources[index].Id);
                Assert.Equal(expected.Resources[index].RemainingAmount, actual.Resources[index].RemainingAmount);
                Assert.Equal(
                    expected.Resources[index].CollectionCooldownTicksRemaining,
                    actual.Resources[index].CollectionCooldownTicksRemaining);
            }
        }
    }
}
