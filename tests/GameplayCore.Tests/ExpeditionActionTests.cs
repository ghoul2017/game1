using System.Collections.Generic;
using GameplayCore;
using Xunit;

namespace GameplayCore.Tests
{
    /// <summary>
    /// 验证队员攻击、普通敌人攻击和资源采集的固定 Tick 行动规则。
    /// </summary>
    public sealed class ExpeditionActionTests
    {
        [Fact]
        public void MemberAttacksOnlyInRangeAndAtConfiguredInterval()
        {
            ExpeditionSetup setup = ExpeditionTestFactory.CreateSetup();
            setup.Members = new[]
            {
                ExpeditionTestFactory.CreateMember(1, attackRange: 0.5f, attackIntervalTicks: 2)
            };
            setup.Enemies = new[]
            {
                ExpeditionTestFactory.CreateNormalEnemy(10, 1.0f, attackRange: 0.1f),
                ExpeditionTestFactory.CreateBoss()
            };
            ExpeditionRun run = ExpeditionRun.Start(setup);

            List<ExpeditionEvent> firstTick = ExpeditionTestFactory.Advance(run);
            List<ExpeditionEvent> secondTick = ExpeditionTestFactory.Advance(run, 1.0f, 0.0f);
            List<ExpeditionEvent> thirdTick = ExpeditionTestFactory.Advance(run);
            List<ExpeditionEvent> fourthTick = ExpeditionTestFactory.Advance(run);

            Assert.Equal(0, CountEvents(firstTick, ExpeditionEventKind.MemberAttacked));
            AssertEvent(
                secondTick,
                ExpeditionEventKind.MemberAttacked,
                2L,
                1,
                10);
            Assert.Equal(0, CountEvents(thirdTick, ExpeditionEventKind.MemberAttacked));
            AssertEvent(
                fourthTick,
                ExpeditionEventKind.MemberAttacked,
                4L,
                1,
                10);
        }

        [Fact]
        public void NormalEnemyDistributesAttacksAcrossLivingMembers()
        {
            ExpeditionSetup setup = ExpeditionTestFactory.CreateSetup();
            setup.Members = new[]
            {
                ExpeditionTestFactory.CreateMember(1, maxHealth: 10, attackRange: 0.1f),
                ExpeditionTestFactory.CreateMember(2, maxHealth: 10, attackRange: 0.1f),
                ExpeditionTestFactory.CreateMember(3, maxHealth: 10, attackRange: 0.1f)
            };
            setup.Enemies = new[]
            {
                ExpeditionTestFactory.CreateNormalEnemy(1, 0.5f, attackDamage: 2, attackRange: 1.0f),
                ExpeditionTestFactory.CreateBoss()
            };
            ExpeditionRun run = ExpeditionRun.Start(setup);
            List<int> targetIds = new List<int>();
            List<long> attackTicks = new List<long>();

            for (int tick = 0; tick < 3; tick++)
            {
                List<ExpeditionEvent> events = ExpeditionTestFactory.Advance(run);
                AddEnemyAttacks(events, targetIds, attackTicks);
            }

            Assert.Equal(new[] { 1, 2, 3 }, targetIds);
            Assert.Equal(new long[] { 1L, 2L, 3L }, attackTicks);
            Assert.Equal(8, run.Members[0].Health);
            Assert.Equal(8, run.Members[1].Health);
            Assert.Equal(8, run.Members[2].Health);
        }

        [Fact]
        public void DeadMemberStopsAttackingBeforeSquadAction()
        {
            ExpeditionSetup setup = ExpeditionTestFactory.CreateSetup();
            setup.Members = new[]
            {
                ExpeditionTestFactory.CreateMember(1, maxHealth: 5),
                ExpeditionTestFactory.CreateMember(2),
                ExpeditionTestFactory.CreateMember(3)
            };
            setup.Enemies = new[]
            {
                ExpeditionTestFactory.CreateBoss(
                    x: 0.0f,
                    attackDamage: 5,
                    actionIntervalTicks: 10,
                    warningDelayTicks: 1)
            };
            ExpeditionRun run = ExpeditionRun.Start(setup);

            ExpeditionTestFactory.Advance(run);
            List<ExpeditionEvent> secondTick = ExpeditionTestFactory.Advance(run);

            Assert.False(run.Members[0].IsAlive);
            AssertEvent(
                secondTick,
                ExpeditionEventKind.MemberDied,
                2L,
                900,
                1);
            Assert.False(HasMemberAttack(secondTick, 1));
            Assert.True(HasMemberAttack(secondTick, 2));
            Assert.True(HasMemberAttack(secondTick, 3));
        }

        [Fact]
        public void ResourceCollectionUsesCadenceAndDepletesOnce()
        {
            ExpeditionSetup setup = ExpeditionTestFactory.CreateSetup();
            setup.Resources = new[]
            {
                ExpeditionTestFactory.CreateResource(
                    x: 0.0f,
                    amount: 5,
                    collectionAmount: 2,
                    collectionIntervalTicks: 2)
            };
            ExpeditionRun run = ExpeditionRun.Start(setup);
            List<ExpeditionEvent> allEvents = new List<ExpeditionEvent>();

            for (int tick = 0; tick < 5; tick++)
            {
                ExpeditionSimulation.Advance(run, 0.0f, 0.0f, allEvents);
            }

            Assert.Equal(new[] { 2, 2, 1 }, GetEventValues(allEvents, ExpeditionEventKind.ResourceCollected));
            Assert.Equal(1, CountEvents(allEvents, ExpeditionEventKind.ResourceDepleted));
            AssertEvent(
                allEvents,
                ExpeditionEventKind.ResourceDepleted,
                5L,
                0,
                800);
            Assert.Equal(5L, run.CollectedResource);
            Assert.Equal(0, run.Resources[0].RemainingAmount);
        }

        [Fact]
        public void NormalEnemySkipsMemberKilledBeforeEnemyAction()
        {
            ExpeditionSetup setup = ExpeditionTestFactory.CreateSetup();
            setup.Members = new[]
            {
                ExpeditionTestFactory.CreateMember(1, maxHealth: 5),
                ExpeditionTestFactory.CreateMember(2, maxHealth: 100),
                ExpeditionTestFactory.CreateMember(3, maxHealth: 100)
            };
            setup.Enemies = new[]
            {
                ExpeditionTestFactory.CreateNormalEnemy(
                    2,
                    0.0f,
                    maxHealth: 1000,
                    attackDamage: 1,
                    attackIntervalTicks: 1),
                ExpeditionTestFactory.CreateBoss(
                    x: 0.0f,
                    maxHealth: 1000,
                    attackDamage: 5,
                    actionIntervalTicks: 10,
                    warningDelayTicks: 1)
            };
            ExpeditionRun run = ExpeditionRun.Start(setup);

            ExpeditionTestFactory.Advance(run);
            List<ExpeditionEvent> secondTick = ExpeditionTestFactory.Advance(run);

            Assert.False(run.Members[0].IsAlive);
            AssertEvent(
                secondTick,
                ExpeditionEventKind.EnemyAttacked,
                2L,
                2,
                2);
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

        private static void AddEnemyAttacks(
            List<ExpeditionEvent> events,
            List<int> targetIds,
            List<long> attackTicks)
        {
            for (int index = 0; index < events.Count; index++)
            {
                if (events[index].Kind == ExpeditionEventKind.EnemyAttacked)
                {
                    targetIds.Add(events[index].TargetId);
                    attackTicks.Add(events[index].Tick);
                }
            }
        }

        private static bool HasMemberAttack(List<ExpeditionEvent> events, int memberId)
        {
            for (int index = 0; index < events.Count; index++)
            {
                if (events[index].Kind == ExpeditionEventKind.MemberAttacked && events[index].SourceId == memberId)
                {
                    return true;
                }
            }

            return false;
        }

        private static int[] GetEventValues(List<ExpeditionEvent> events, ExpeditionEventKind kind)
        {
            List<int> values = new List<int>();
            for (int index = 0; index < events.Count; index++)
            {
                if (events[index].Kind == kind)
                {
                    values.Add(events[index].Value);
                }
            }

            return values.ToArray();
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
    }
}
