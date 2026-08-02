using System;
using System.Collections.Generic;
using System.Linq;
using GameplayCore;
using Xunit;

namespace GameplayCore.Tests
{
    public sealed class ArenaSimulationTests
    {
        [Fact]
        public void SameSnapshotsAndSeedProduceFixedHashTenThousandTimes()
        {
            const ulong expectedHash = 16321593159651442450UL;
            ArenaBattleSetup setup = ArenaTestFactory.CreateSetup();

            for (int iteration = 0; iteration < 10_000; iteration++)
            {
                ArenaBattleExecution execution = ArenaBattleRunner.Run(setup);

                Assert.Equal(ArenaBattleOutcome.DefenderVictory, execution.Result.Outcome);
                Assert.Equal(ArenaBattleEndReason.AttackerEliminated, execution.Result.EndReason);
                Assert.Equal(expectedHash, execution.Replay.ComputeEventHash());
            }
        }

        [Fact]
        public void SetupCopiesSnapshotsAndUsesStableSlotOrder()
        {
            ArenaUnitSnapshot[] sourceUnits =
            {
                new ArenaUnitSnapshot(3, 1003, 2, 100, 10, 1),
                new ArenaUnitSnapshot(1, 1001, 0, 100, 10, 1),
                new ArenaUnitSnapshot(2, 1002, 1, 100, 10, 1)
            };
            ArenaSquadSnapshot attacker = new ArenaSquadSnapshot("attack", 1, 1, sourceUnits);
            ArenaBattleSetup setup = new ArenaBattleSetup(
                1U,
                10,
                100,
                100,
                10,
                attacker,
                ArenaTestFactory.CreateDefender());

            sourceUnits[0] = new ArenaUnitSnapshot(9, 1009, 4, 1, 1, 1);

            Assert.Equal(new[] { 1, 2, 3 }, setup.Attacker.Units.Select(unit => unit.UnitId).ToArray());
            Assert.Equal(100, setup.Attacker.Units[2].MaxHealth);
        }

        [Fact]
        public void UnitCountDuplicateSlotAndVersionMismatchAreRejected()
        {
            ArenaUnitSnapshot[] tooMany = Enumerable.Range(1, ArenaLimits.MaximumUnitsPerSide + 1)
                .Select(id => new ArenaUnitSnapshot(
                    id,
                    id,
                    (id - 1) % ArenaLimits.MaximumUnitsPerSide,
                    10,
                    1,
                    1))
                .ToArray();
            ArenaUnitSnapshot[] duplicateSlots =
            {
                new ArenaUnitSnapshot(1, 1, 0, 10, 1, 1),
                new ArenaUnitSnapshot(2, 2, 0, 10, 1, 1)
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => new ArenaUnitSnapshot(6, 6, 5, 10, 1, 1));
            Assert.Throws<ArgumentException>(() => new ArenaSquadSnapshot("too-many", 1, 1, tooMany));
            Assert.Throws<ArgumentException>(() => new ArenaSquadSnapshot("duplicate", 1, 1, duplicateSlots));

            ArenaSquadSnapshot defender = new ArenaSquadSnapshot(
                "defender-v2",
                2,
                1,
                new[] { new ArenaUnitSnapshot(100, 100, 0, 10, 1, 1) });
            Assert.Throws<ArgumentException>(() => new ArenaBattleSetup(
                1U,
                10,
                100,
                100,
                10,
                ArenaTestFactory.CreateAttacker(),
                defender));
        }

        [Fact]
        public void TickLimitProducesBoundedDrawAndFinalKeyFrame()
        {
            ArenaBattleSetup setup = new ArenaBattleSetup(
                7U,
                10,
                1,
                100,
                1,
                new ArenaSquadSnapshot(
                    "attack",
                    1,
                    1,
                    new[] { new ArenaUnitSnapshot(1, 1, 0, 1_000_000, 1, 100) }),
                new ArenaSquadSnapshot(
                    "defend",
                    1,
                    1,
                    new[] { new ArenaUnitSnapshot(2, 2, 0, 1_000_000, 1, 100) }));

            ArenaBattleExecution execution = ArenaBattleRunner.Run(setup);

            Assert.Equal(ArenaBattleOutcome.Draw, execution.Result.Outcome);
            Assert.Equal(ArenaBattleEndReason.TickLimitReached, execution.Result.EndReason);
            Assert.Equal(1L, execution.Result.FinalTick);
            Assert.Equal(0L, execution.Replay.KeyFrames[0].Tick);
            Assert.Equal(1L, execution.Replay.KeyFrames[execution.Replay.KeyFrames.Count - 1].Tick);
            Assert.True(execution.Replay.Events.Count <= setup.MaximumEvents);
        }

        [Fact]
        public void EventLimitStopsBeforeCapacityCanBeExceeded()
        {
            ArenaBattleSetup setup = new ArenaBattleSetup(
                9U,
                10,
                100,
                8,
                10,
                ArenaTestFactory.CreateAttacker(),
                ArenaTestFactory.CreateDefender());

            ArenaBattleExecution execution = ArenaBattleRunner.Run(setup);

            Assert.Equal(ArenaBattleOutcome.Draw, execution.Result.Outcome);
            Assert.Equal(ArenaBattleEndReason.EventLimitReached, execution.Result.EndReason);
            Assert.True(execution.Replay.Events.Count <= setup.MaximumEvents);
            Assert.Equal(ArenaEventKind.BattleEnded, execution.Replay.Events[execution.Replay.Events.Count - 1].Kind);
        }

        [Fact]
        public void BatchRunnerAcceptsFiftyAndRejectsUnboundedInput()
        {
            ArenaBattleSetup[] accepted = Enumerable.Range(0, ArenaLimits.MaximumBatchCount)
                .Select(index => ArenaTestFactory.CreateSetup())
                .ToArray();
            ArenaBattleSetup[] rejected = Enumerable.Range(0, ArenaLimits.MaximumBatchCount + 1)
                .Select(index => ArenaTestFactory.CreateSetup())
                .ToArray();

            IReadOnlyList<ArenaBattleExecution> executions = ArenaBatchRunner.Run(accepted);

            Assert.Equal(ArenaLimits.MaximumBatchCount, executions.Count);
            Assert.Throws<ArgumentException>(() => ArenaBatchRunner.Run(rejected));
        }
    }
}
