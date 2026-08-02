using System;

namespace GameplayCore
{
    /// <summary>
    /// 保存一局终态的不可变结果，只能由 ExpeditionRun 在进入终态时创建。
    /// </summary>
    public sealed class ExpeditionResult : IEquatable<ExpeditionResult>
    {
        internal ExpeditionResult(
            long runId,
            uint seed,
            ExpeditionPhase phase,
            long tick,
            int kills,
            long collectedResource)
        {
            if (phase == ExpeditionPhase.Running)
            {
                throw new ArgumentException("运行中的远征不能产生结果", nameof(phase));
            }

            RunId = runId;
            Seed = seed;
            Phase = phase;
            Tick = tick;
            Kills = kills;
            CollectedResource = collectedResource;
        }

        public long RunId { get; }

        public uint Seed { get; }

        public ExpeditionPhase Phase { get; }

        public long Tick { get; }

        public int Kills { get; }

        public long CollectedResource { get; }

        public bool Equals(ExpeditionResult other)
        {
            return other != null && RunId == other.RunId && Seed == other.Seed && Phase == other.Phase &&
                Tick == other.Tick && Kills == other.Kills && CollectedResource == other.CollectedResource;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ExpeditionResult);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = RunId.GetHashCode();
                hashCode = (hashCode * 397) ^ Seed.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Phase;
                hashCode = (hashCode * 397) ^ Tick.GetHashCode();
                hashCode = (hashCode * 397) ^ Kills;
                hashCode = (hashCode * 397) ^ CollectedResource.GetHashCode();
                return hashCode;
            }
        }
    }
}
