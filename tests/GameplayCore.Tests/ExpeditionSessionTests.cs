using System;
using System.Reflection;
using GameplayCore;
using Xunit;

namespace GameplayCore.Tests
{
    /// <summary>
    /// 验证 GameplayCore 的最小移动行为、输入边界和状态所有权。
    /// </summary>
    public sealed class ExpeditionSessionTests
    {
        private const float Tolerance = 0.0001f;

        [Fact]
        public void ZeroDirectionOnlyAdvancesTick()
        {
            ExpeditionSession session = CreateSession(movePerTick: 2.0f);

            session.Step(new MoveCommand(0.0f, 0.0f));

            Assert.Equal(1L, session.State.Tick);
            AssertClose(0.0f, session.State.SquadX);
            AssertClose(0.0f, session.State.SquadY);
        }

        [Fact]
        public void UnitDirectionMovesByConfiguredDistance()
        {
            ExpeditionSession session = CreateSession(movePerTick: 2.5f);

            session.Step(new MoveCommand(1.0f, 0.0f));

            Assert.Equal(1L, session.State.Tick);
            AssertClose(2.5f, session.State.SquadX);
            AssertClose(0.0f, session.State.SquadY);
        }

        [Theory]
        [InlineData(1.0f, 1.0f, 0.70710677f, 0.70710677f)]
        [InlineData(3.0f, 4.0f, 0.6f, 0.8f)]
        public void DirectionLongerThanOneIsNormalized(
            float directionX,
            float directionY,
            float expectedUnitX,
            float expectedUnitY)
        {
            const float movePerTick = 2.0f;
            ExpeditionSession session = CreateSession(movePerTick);

            session.Step(new MoveCommand(directionX, directionY));

            AssertClose(expectedUnitX * movePerTick, session.State.SquadX);
            AssertClose(expectedUnitY * movePerTick, session.State.SquadY);
            double distance = Math.Sqrt(
                (double)session.State.SquadX * session.State.SquadX +
                (double)session.State.SquadY * session.State.SquadY);
            Assert.InRange(distance, movePerTick - Tolerance, movePerTick + Tolerance);
        }

        [Fact]
        public void MultipleStepsProduceExpectedTickAndPosition()
        {
            ExpeditionSession session = CreateSession(movePerTick: 0.5f);

            session.Step(new MoveCommand(1.0f, 0.0f));
            session.Step(new MoveCommand(0.0f, 1.0f));
            session.Step(new MoveCommand(0.0f, 0.0f));

            Assert.Equal(3L, session.State.Tick);
            AssertClose(0.5f, session.State.SquadX);
            AssertClose(0.5f, session.State.SquadY);
        }

        [Fact]
        public void EquivalentSessionsProduceExactlyEqualState()
        {
            ExpeditionState sharedInitialState = new ExpeditionState(7L, 3.0f, -2.0f);
            ExpeditionSession first = new ExpeditionSession(sharedInitialState, 0.75f);
            ExpeditionSession second = new ExpeditionSession(sharedInitialState, 0.75f);
            MoveCommand[] commands =
            {
                new MoveCommand(1.0f, 0.0f),
                new MoveCommand(3.0f, 4.0f),
                new MoveCommand(-1.0f, 0.0f),
                new MoveCommand(0.0f, 0.0f)
            };

            foreach (MoveCommand command in commands)
            {
                first.Step(command);
                second.Step(command);
            }

            Assert.Equal(first.State.Tick, second.State.Tick);
            Assert.Equal(first.State.SquadX, second.State.SquadX);
            Assert.Equal(first.State.SquadY, second.State.SquadY);
        }

        [Fact]
        public void MoveCommandRejectsNonFiniteDirections()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MoveCommand(float.NaN, 0.0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MoveCommand(float.PositiveInfinity, 0.0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MoveCommand(0.0f, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MoveCommand(0.0f, float.NegativeInfinity));
        }

        [Fact]
        public void InitialStateRejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExpeditionState(-1L, 0.0f, 0.0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExpeditionState(0L, float.NaN, 0.0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExpeditionState(0L, 0.0f, float.PositiveInfinity));
        }

        [Fact]
        public void SessionRejectsInvalidMoveDistanceAndMissingState()
        {
            ExpeditionState state = new ExpeditionState(0L, 0.0f, 0.0f);

            Assert.Throws<ArgumentNullException>(() => new ExpeditionSession(null!, 1.0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExpeditionSession(state, 0.0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExpeditionSession(state, -1.0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExpeditionSession(state, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExpeditionSession(state, float.PositiveInfinity));
        }

        [Fact]
        public void SessionOwnsAnIndependentCopyOfInitialState()
        {
            ExpeditionState initialState = new ExpeditionState(4L, 8.0f, 9.0f);
            ExpeditionSession session = new ExpeditionSession(initialState, 1.0f);

            session.Step(new MoveCommand(1.0f, 0.0f));

            Assert.Equal(4L, initialState.Tick);
            AssertClose(8.0f, initialState.SquadX);
            AssertClose(9.0f, initialState.SquadY);
            Assert.Equal(5L, session.State.Tick);
            AssertClose(9.0f, session.State.SquadX);
            AssertClose(9.0f, session.State.SquadY);
        }

        [Fact]
        public void StatePropertiesCannotBeChangedByConsumers()
        {
            PropertyInfo[] properties = typeof(ExpeditionState).GetProperties(
                BindingFlags.Instance | BindingFlags.Public);

            Assert.NotEmpty(properties);
            foreach (PropertyInfo property in properties)
            {
                Assert.False(property.CanWrite, $"{property.Name} 不应公开 setter");
            }
        }

        private static ExpeditionSession CreateSession(float movePerTick)
        {
            return new ExpeditionSession(new ExpeditionState(0L, 0.0f, 0.0f), movePerTick);
        }

        private static void AssertClose(float expected, float actual)
        {
            Assert.InRange(Math.Abs(expected - actual), 0.0f, Tolerance);
        }
    }
}
