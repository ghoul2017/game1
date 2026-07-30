using System.Collections.Generic;
using GameplayCore;
using Xunit;

namespace GameplayCore.Tests
{
    /// <summary>
    /// 验证 Boss 警告的位置锁定、群体伤害和死亡清理规则。
    /// </summary>
    public sealed class ExpeditionBossTests
    {
        [Fact]
        public void BossWarningLocksPositionAndCanBeDodged()
        {
            ExpeditionSetup setup = ExpeditionTestFactory.CreateSetup();
            setup.MoveSpeedPerSecond = 2.0f;
            setup.Enemies = new[]
            {
                ExpeditionTestFactory.CreateBoss(
                    x: 0.0f,
                    actionIntervalTicks: 10,
                    warningDelayTicks: 2,
                    warningRadius: 0.5f)
            };
            ExpeditionRun run = ExpeditionRun.Start(setup);

            List<ExpeditionEvent> firstTick = ExpeditionTestFactory.Advance(run);
            ExpeditionTestFactory.Advance(run, 1.0f, 0.0f);
            List<ExpeditionEvent> thirdTick = ExpeditionTestFactory.Advance(run);

            ExpeditionEvent started = GetSingleEvent(firstTick, ExpeditionEventKind.BossWarningStarted);
            ExpeditionEvent dodged = GetSingleEvent(thirdTick, ExpeditionEventKind.BossWarningDodged);
            Assert.Equal(1L, started.Tick);
            Assert.Equal(900, started.SourceId);
            Assert.Equal(3L, dodged.Tick);
            Assert.Equal(900, dodged.SourceId);
            Assert.Equal(0.0f, started.X);
            Assert.Equal(0.0f, dodged.X);
            Assert.Equal(0.0f, dodged.Y);
            Assert.Equal(100, run.Members[0].Health);
            Assert.False(run.Enemies[0].WarningActive);
        }

        [Fact]
        public void BossWarningHitDamagesEveryLivingMember()
        {
            ExpeditionSetup setup = ExpeditionTestFactory.CreateSetup();
            setup.Members = new[]
            {
                ExpeditionTestFactory.CreateMember(1, maxHealth: 10),
                ExpeditionTestFactory.CreateMember(2, maxHealth: 10),
                ExpeditionTestFactory.CreateMember(3, maxHealth: 10)
            };
            setup.Enemies = new[]
            {
                ExpeditionTestFactory.CreateBoss(
                    x: 0.0f,
                    attackDamage: 7,
                    actionIntervalTicks: 10,
                    warningDelayTicks: 1)
            };
            ExpeditionRun run = ExpeditionRun.Start(setup);

            ExpeditionTestFactory.Advance(run);
            List<ExpeditionEvent> secondTick = ExpeditionTestFactory.Advance(run);

            ExpeditionEvent hit = GetSingleEvent(secondTick, ExpeditionEventKind.BossWarningHit);
            Assert.Equal(2L, hit.Tick);
            Assert.Equal(900, hit.SourceId);
            Assert.Equal(7, hit.Value);
            Assert.Equal(3, CountEvents(secondTick, ExpeditionEventKind.MemberDamaged));
            Assert.Equal(3, run.Members[0].Health);
            Assert.Equal(3, run.Members[1].Health);
            Assert.Equal(3, run.Members[2].Health);
        }

        [Fact]
        public void FirstMemberDeathDoesNotFailRunWhileOthersLive()
        {
            ExpeditionSetup setup = ExpeditionTestFactory.CreateSetup();
            setup.Members = new[]
            {
                ExpeditionTestFactory.CreateMember(1, maxHealth: 5),
                ExpeditionTestFactory.CreateMember(2, maxHealth: 10),
                ExpeditionTestFactory.CreateMember(3, maxHealth: 10)
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
            ExpeditionTestFactory.Advance(run);

            Assert.False(run.Members[0].IsAlive);
            Assert.True(run.Members[1].IsAlive);
            Assert.True(run.Members[2].IsAlive);
            Assert.Equal(ExpeditionPhase.Running, run.Phase);
        }

        [Fact]
        public void KillingBossClearsPendingWarning()
        {
            ExpeditionSetup setup = ExpeditionTestFactory.CreateSetup();
            setup.Enemies = new[]
            {
                ExpeditionTestFactory.CreateBoss(
                    x: 0.0f,
                    maxHealth: 3,
                    actionIntervalTicks: 10,
                    warningDelayTicks: 2)
            };
            ExpeditionRun run = ExpeditionRun.Start(setup);

            List<ExpeditionEvent> firstTick = ExpeditionTestFactory.Advance(run);
            List<ExpeditionEvent> laterEvents = new List<ExpeditionEvent>();
            ExpeditionSimulation.Advance(run, 0.0f, 0.0f, laterEvents);
            ExpeditionSimulation.Advance(run, 0.0f, 0.0f, laterEvents);

            Assert.Equal(1, CountEvents(firstTick, ExpeditionEventKind.BossWarningStarted));
            Assert.Equal(1, CountEvents(firstTick, ExpeditionEventKind.EnemyDied));
            Assert.False(run.Enemies[0].WarningActive);
            Assert.Equal(0, CountEvents(laterEvents, ExpeditionEventKind.BossWarningHit));
            Assert.Equal(0, CountEvents(laterEvents, ExpeditionEventKind.BossWarningDodged));
        }

        private static ExpeditionEvent GetSingleEvent(List<ExpeditionEvent> events, ExpeditionEventKind kind)
        {
            ExpeditionEvent result = default;
            int count = 0;
            for (int index = 0; index < events.Count; index++)
            {
                if (events[index].Kind == kind)
                {
                    result = events[index];
                    count++;
                }
            }

            Assert.Equal(1, count);
            return result;
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
    }
}
