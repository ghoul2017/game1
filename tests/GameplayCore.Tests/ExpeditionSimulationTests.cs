using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameplayCore;
using Xunit;

namespace GameplayCore.Tests
{
    /// <summary>
    /// 验证固定 Tick 移动、开局数据边界和运行态所有权。
    /// </summary>
    public sealed class ExpeditionSimulationTests
    {
        private const float Tolerance = 0.0001f;

        [Fact]
        public void ZeroDirectionAdvancesOneTickWithoutMoving()
        {
            ExpeditionRun run = ExpeditionRun.Start(ExpeditionTestFactory.CreateSetup());

            ExpeditionTestFactory.Advance(run);

            Assert.Equal(1L, run.Tick);
            AssertClose(0.0f, run.SquadX);
            AssertClose(0.0f, run.SquadY);
        }

        [Fact]
        public void DirectionIsNormalizedBeforeMovement()
        {
            ExpeditionSetup setup = ExpeditionTestFactory.CreateSetup();
            setup.MoveSpeedPerSecond = 2.0f;
            ExpeditionRun run = ExpeditionRun.Start(setup);

            ExpeditionTestFactory.Advance(run, 3.0f, 4.0f);

            AssertClose(1.2f, run.SquadX);
            AssertClose(1.6f, run.SquadY);
        }

        [Fact]
        public void MultipleAdvancesProduceKnownTickAndPositionAtConfiguredTickRate()
        {
            ExpeditionSetup setup = ExpeditionTestFactory.CreateSetup();
            setup.TicksPerSecond = 4;
            setup.MoveSpeedPerSecond = 2.0f;
            ExpeditionRun run = ExpeditionRun.Start(setup);

            ExpeditionTestFactory.Advance(run, 1.0f, 0.0f);
            ExpeditionTestFactory.Advance(run, 0.0f, 1.0f);
            ExpeditionTestFactory.Advance(run);

            Assert.Equal(3L, run.Tick);
            AssertClose(0.5f, run.SquadX);
            AssertClose(0.5f, run.SquadY);
        }

        [Fact]
        public void MovementStopsAtActivityBounds()
        {
            ExpeditionSetup setup = ExpeditionTestFactory.CreateSetup();
            setup.MoveSpeedPerSecond = 10.0f;
            setup.MinimumX = -1.0f;
            setup.MaximumX = 1.0f;
            setup.MinimumY = -1.0f;
            setup.MaximumY = 1.0f;
            setup.ExitX = 0.0f;
            setup.Enemies = new[] { ExpeditionTestFactory.CreateBoss(x: 0.0f, attackRange: 0.1f) };
            setup.Resources = new[] { ExpeditionTestFactory.CreateResource(x: -0.5f) };
            ExpeditionRun run = ExpeditionRun.Start(setup);

            ExpeditionTestFactory.Advance(run, 1.0f, 0.0f);

            AssertClose(1.0f, run.SquadX);
            AssertClose(0.0f, run.SquadY);
        }

        [Fact]
        public void AdvanceRejectsInvalidInputWithoutChangingRun()
        {
            ExpeditionRun run = ExpeditionRun.Start(ExpeditionTestFactory.CreateSetup());

            Assert.Throws<ArgumentOutOfRangeException>(
                () => ExpeditionSimulation.Advance(run, float.NaN, 0.0f, new List<ExpeditionEvent>()));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ExpeditionSimulation.Advance(
                    run,
                    0.0f,
                    float.PositiveInfinity,
                    new List<ExpeditionEvent>()));
            Assert.Equal(0L, run.Tick);
        }

        [Fact]
        public void RunCopiesSetupValuesAtStart()
        {
            ExpeditionSetup setup = ExpeditionTestFactory.CreateSetup();
            ExpeditionRun run = ExpeditionRun.Start(setup);

            setup.MoveSpeedPerSecond = 20.0f;
            setup.Members[0].MaxHealth = 1;
            setup.Enemies[0].MaxHealth = 1;
            setup.Resources[0].Amount = 1;

            AssertClose(1.0f, run.MoveSpeedPerSecond);
            Assert.Equal(100, run.Members[0].MaxHealth);
            Assert.Equal(1000, run.Enemies[0].MaxHealth);
            Assert.Equal(10, run.Resources[0].InitialAmount);
        }

        [Fact]
        public void RuntimeStateDoesNotExposePublicSetters()
        {
            Type[] stateTypes =
            {
                typeof(ExpeditionRun),
                typeof(ExpeditionMember),
                typeof(ExpeditionEnemy),
                typeof(ExpeditionResource)
            };

            foreach (Type stateType in stateTypes)
            {
                PropertyInfo[] properties = stateType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
                Assert.NotEmpty(properties);
                Assert.All(
                    properties,
                    property => Assert.False(property.CanWrite, $"{stateType.Name}.{property.Name}"));
            }
        }

        [Fact]
        public void SetupRejectsInvalidTickRateMoveSpeedAndRadius()
        {
            ExpeditionSetup invalidTick = ExpeditionTestFactory.CreateSetup();
            invalidTick.TicksPerSecond = 0;
            ExpeditionSetup invalidRadius = ExpeditionTestFactory.CreateSetup();
            invalidRadius.TargetAcquireRadius = float.NaN;
            float[] invalidMoveSpeeds =
            {
                0.0f,
                -1.0f,
                float.NaN,
                float.PositiveInfinity,
                float.NegativeInfinity
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => ExpeditionRun.Start(invalidTick));
            Assert.Throws<ArgumentOutOfRangeException>(() => ExpeditionRun.Start(invalidRadius));
            foreach (float moveSpeed in invalidMoveSpeeds)
            {
                ExpeditionSetup invalidMoveSpeed = ExpeditionTestFactory.CreateSetup();
                invalidMoveSpeed.MoveSpeedPerSecond = moveSpeed;
                Assert.Throws<ArgumentOutOfRangeException>(() => ExpeditionRun.Start(invalidMoveSpeed));
            }
        }

        [Fact]
        public void SetupRejectsInvalidMemberCountAndDuplicateIds()
        {
            ExpeditionSetup emptySquad = ExpeditionTestFactory.CreateSetup();
            emptySquad.Members = Array.Empty<ExpeditionMemberSetup>();
            ExpeditionSetup duplicate = ExpeditionTestFactory.CreateSetup();
            duplicate.Members[1].Id = duplicate.Members[0].Id;

            Assert.Throws<ArgumentException>(() => ExpeditionRun.Start(emptySquad));
            Assert.Throws<ArgumentException>(() => ExpeditionRun.Start(duplicate));
        }

        [Fact]
        public void SetupAcceptsFiveMembers()
        {
            ExpeditionSetup setup = ExpeditionTestFactory.CreateSetup();
            setup.Members = Enumerable.Range(1, 5)
                .Select(id => ExpeditionTestFactory.CreateMember(id))
                .ToArray();

            ExpeditionRun run = ExpeditionRun.Start(setup);

            Assert.Equal(5, run.Members.Count);
        }

        [Fact]
        public void SetupRejectsMoreThanFiveMembers()
        {
            ExpeditionSetup setup = ExpeditionTestFactory.CreateSetup();
            setup.Members = Enumerable.Range(1, 6)
                .Select(id => ExpeditionTestFactory.CreateMember(id))
                .ToArray();

            Assert.Throws<ArgumentException>(() => ExpeditionRun.Start(setup));
        }

        [Fact]
        public void SetupRejectsInvalidHealthAndResourceAmount()
        {
            ExpeditionSetup invalidHealth = ExpeditionTestFactory.CreateSetup();
            invalidHealth.Members[0].MaxHealth = 0;
            ExpeditionSetup invalidResource = ExpeditionTestFactory.CreateSetup();
            invalidResource.Resources[0].Amount = -1;

            Assert.Throws<ArgumentOutOfRangeException>(() => ExpeditionRun.Start(invalidHealth));
            Assert.Throws<ArgumentOutOfRangeException>(() => ExpeditionRun.Start(invalidResource));
        }

        [Fact]
        public void SetupRejectsExitAndObjectsOutsideBounds()
        {
            ExpeditionSetup invalidExit = ExpeditionTestFactory.CreateSetup();
            invalidExit.ExitX = 101.0f;
            ExpeditionSetup invalidEnemy = ExpeditionTestFactory.CreateSetup();
            invalidEnemy.Enemies[0].X = 101.0f;

            Assert.Throws<ArgumentOutOfRangeException>(() => ExpeditionRun.Start(invalidExit));
            Assert.Throws<ArgumentOutOfRangeException>(() => ExpeditionRun.Start(invalidEnemy));
        }

        [Fact]
        public void SetupRequiresExactlyOneBoss()
        {
            ExpeditionSetup missingBoss = ExpeditionTestFactory.CreateSetup();
            missingBoss.Enemies = new[] { ExpeditionTestFactory.CreateNormalEnemy(1, 10.0f) };
            ExpeditionSetup twoBosses = ExpeditionTestFactory.CreateSetup();
            twoBosses.Enemies = new[]
            {
                ExpeditionTestFactory.CreateBoss(900, 70.0f),
                ExpeditionTestFactory.CreateBoss(901, 80.0f)
            };

            Assert.Throws<ArgumentException>(() => ExpeditionRun.Start(missingBoss));
            Assert.Throws<ArgumentException>(() => ExpeditionRun.Start(twoBosses));
        }

        private static void AssertClose(float expected, float actual)
        {
            Assert.InRange(Math.Abs(expected - actual), 0.0f, Tolerance);
        }
    }
}
