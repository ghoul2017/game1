using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GameplayCore
{
    public sealed class ArenaBattleExecution
    {
        internal ArenaBattleExecution(ArenaBattleResult result, ArenaReplay replay)
        {
            Result = result;
            Replay = replay;
        }

        public ArenaBattleResult Result { get; }

        public ArenaReplay Replay { get; }
    }

    public static class ArenaBattleRunner
    {
        public static ArenaBattleExecution Run(ArenaBattleSetup setup)
        {
            if (setup == null)
            {
                throw new ArgumentNullException(nameof(setup));
            }

            ArenaRun run = ArenaRun.Start(setup);
            List<ArenaEvent> events = new List<ArenaEvent>();
            List<ArenaKeyFrame> keyFrames = new List<ArenaKeyFrame>
            {
                ArenaKeyFrame.Capture(run)
            };

            while (!run.HasEnded)
            {
                ArenaSimulation.Advance(run, events);
                if (run.HasEnded || run.Tick % run.KeyFrameIntervalTicks == 0L)
                {
                    keyFrames.Add(ArenaKeyFrame.Capture(run));
                }
            }

            ArenaBattleResult result = new ArenaBattleResult(run.Outcome, run.EndReason, run.Tick);
            ArenaReplay replay = new ArenaReplay(setup, result, events, keyFrames);
            return new ArenaBattleExecution(result, replay);
        }
    }

    public static class ArenaBatchRunner
    {
        public static IReadOnlyList<ArenaBattleExecution> Run(IReadOnlyList<ArenaBattleSetup> setups)
        {
            if (setups == null)
            {
                throw new ArgumentNullException(nameof(setups));
            }

            if (setups.Count == 0 || setups.Count > ArenaLimits.MaximumBatchCount)
            {
                throw new ArgumentException(
                    $"竞技场批次数必须在 1 到 {ArenaLimits.MaximumBatchCount} 之间",
                    nameof(setups));
            }

            ArenaBattleExecution[] result = new ArenaBattleExecution[setups.Count];
            for (int index = 0; index < setups.Count; index++)
            {
                if (setups[index] == null)
                {
                    throw new ArgumentException("竞技场批次不能包含空配置", nameof(setups));
                }

                result[index] = ArenaBattleRunner.Run(setups[index]);
            }

            return new ReadOnlyCollection<ArenaBattleExecution>(result);
        }
    }
}
