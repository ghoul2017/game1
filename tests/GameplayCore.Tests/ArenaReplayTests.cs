using GameplayCore;
using Xunit;

namespace GameplayCore.Tests
{
    public sealed class ArenaReplayTests
    {
        [Fact]
        public void OneTimesFourTimesAndSkipReachIdenticalFinalState()
        {
            ArenaReplay replay = ArenaBattleRunner.Run(ArenaTestFactory.CreateSetup()).Replay;
            ArenaReplayPlayback oneTimes = new ArenaReplayPlayback(replay);
            ArenaReplayPlayback fourTimes = new ArenaReplayPlayback(replay);
            ArenaReplayPlayback skipped = new ArenaReplayPlayback(replay);

            while (!oneTimes.HasEnded)
            {
                oneTimes.Advance(1);
            }

            while (!fourTimes.HasEnded)
            {
                fourTimes.Advance(4);
            }

            skipped.SkipToEnd();

            Assert.Equal(replay.Result.Outcome, oneTimes.VisibleOutcome);
            Assert.Equal(oneTimes.VisibleOutcome, fourTimes.VisibleOutcome);
            Assert.Equal(oneTimes.VisibleOutcome, skipped.VisibleOutcome);
            AssertFinalHealthEqual(replay, oneTimes, fourTimes, skipped);
        }

        [Fact]
        public void SeekUsesKeyFrameWithoutResimulatingBattle()
        {
            ArenaReplay replay = ArenaBattleRunner.Run(ArenaTestFactory.CreateSetup()).Replay;
            ArenaReplayPlayback playback = new ArenaReplayPlayback(replay);
            long middleTick = replay.Result.FinalTick / 2L;

            playback.Seek(middleTick);
            int firstHealth = playback.GetHealth(ArenaSide.Attacker, replay.Attacker.Units[0].UnitId);
            playback.Seek(0L);
            playback.Seek(middleTick);

            Assert.Equal(firstHealth, playback.GetHealth(ArenaSide.Attacker, replay.Attacker.Units[0].UnitId));
            Assert.Equal(middleTick, playback.CurrentTick);
        }

        private static void AssertFinalHealthEqual(
            ArenaReplay replay,
            ArenaReplayPlayback first,
            ArenaReplayPlayback second,
            ArenaReplayPlayback third)
        {
            for (int index = 0; index < replay.Attacker.Units.Count; index++)
            {
                int unitId = replay.Attacker.Units[index].UnitId;
                Assert.Equal(first.GetHealth(ArenaSide.Attacker, unitId), second.GetHealth(ArenaSide.Attacker, unitId));
                Assert.Equal(first.GetHealth(ArenaSide.Attacker, unitId), third.GetHealth(ArenaSide.Attacker, unitId));
            }

            for (int index = 0; index < replay.Defender.Units.Count; index++)
            {
                int unitId = replay.Defender.Units[index].UnitId;
                Assert.Equal(first.GetHealth(ArenaSide.Defender, unitId), second.GetHealth(ArenaSide.Defender, unitId));
                Assert.Equal(first.GetHealth(ArenaSide.Defender, unitId), third.GetHealth(ArenaSide.Defender, unitId));
            }
        }
    }
}
