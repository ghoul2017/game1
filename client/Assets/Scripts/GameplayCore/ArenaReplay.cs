using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GameplayCore
{
    public enum ArenaEventKind
    {
        UnitAttacked = 1,
        UnitDamaged = 2,
        UnitDefeated = 3,
        BattleEnded = 4
    }

    public readonly struct ArenaEvent : IEquatable<ArenaEvent>
    {
        internal ArenaEvent(
            long tick,
            ArenaEventKind kind,
            ArenaSide sourceSide,
            int sourceUnitId,
            ArenaSide targetSide,
            int targetUnitId,
            int value,
            int remainingHealth)
        {
            Tick = tick;
            Kind = kind;
            SourceSide = sourceSide;
            SourceUnitId = sourceUnitId;
            TargetSide = targetSide;
            TargetUnitId = targetUnitId;
            Value = value;
            RemainingHealth = remainingHealth;
        }

        public long Tick { get; }

        public ArenaEventKind Kind { get; }

        public ArenaSide SourceSide { get; }

        public int SourceUnitId { get; }

        public ArenaSide TargetSide { get; }

        public int TargetUnitId { get; }

        public int Value { get; }

        public int RemainingHealth { get; }

        public bool Equals(ArenaEvent other)
        {
            return Tick == other.Tick && Kind == other.Kind && SourceSide == other.SourceSide &&
                SourceUnitId == other.SourceUnitId && TargetSide == other.TargetSide &&
                TargetUnitId == other.TargetUnitId && Value == other.Value &&
                RemainingHealth == other.RemainingHealth;
        }

        public override bool Equals(object obj)
        {
            return obj is ArenaEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Tick.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Kind;
                hashCode = (hashCode * 397) ^ (int)SourceSide;
                hashCode = (hashCode * 397) ^ SourceUnitId;
                hashCode = (hashCode * 397) ^ (int)TargetSide;
                hashCode = (hashCode * 397) ^ TargetUnitId;
                hashCode = (hashCode * 397) ^ Value;
                hashCode = (hashCode * 397) ^ RemainingHealth;
                return hashCode;
            }
        }
    }

    public readonly struct ArenaUnitFrame : IEquatable<ArenaUnitFrame>
    {
        public ArenaUnitFrame(ArenaSide side, int unitId, int health)
        {
            if (side == ArenaSide.None)
            {
                throw new ArgumentOutOfRangeException(nameof(side));
            }

            if (unitId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(unitId));
            }

            if (health < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(health));
            }

            Side = side;
            UnitId = unitId;
            Health = health;
        }

        public ArenaSide Side { get; }

        public int UnitId { get; }

        public int Health { get; }

        public bool Equals(ArenaUnitFrame other)
        {
            return Side == other.Side && UnitId == other.UnitId && Health == other.Health;
        }

        public override bool Equals(object obj)
        {
            return obj is ArenaUnitFrame other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (int)Side;
                hashCode = (hashCode * 397) ^ UnitId;
                hashCode = (hashCode * 397) ^ Health;
                return hashCode;
            }
        }
    }

    public sealed class ArenaKeyFrame
    {
        private readonly ArenaUnitFrame[] _units;
        private readonly ReadOnlyCollection<ArenaUnitFrame> _unitView;

        internal ArenaKeyFrame(long tick, ArenaUnitFrame[] units)
        {
            Tick = tick;
            _units = (ArenaUnitFrame[])units.Clone();
            _unitView = Array.AsReadOnly(_units);
        }

        public long Tick { get; }

        public IReadOnlyList<ArenaUnitFrame> Units => _unitView;

        internal ArenaKeyFrame Copy()
        {
            return new ArenaKeyFrame(Tick, _units);
        }

        internal static ArenaKeyFrame Capture(ArenaRun run)
        {
            ArenaUnitFrame[] units = new ArenaUnitFrame[
                run.AttackerUnits.Count + run.DefenderUnits.Count];
            int outputIndex = 0;

            for (int index = 0; index < run.AttackerUnits.Count; index++)
            {
                ArenaUnitState unit = run.AttackerUnits[index];
                units[outputIndex++] = new ArenaUnitFrame(ArenaSide.Attacker, unit.UnitId, unit.Health);
            }

            for (int index = 0; index < run.DefenderUnits.Count; index++)
            {
                ArenaUnitState unit = run.DefenderUnits[index];
                units[outputIndex++] = new ArenaUnitFrame(ArenaSide.Defender, unit.UnitId, unit.Health);
            }

            return new ArenaKeyFrame(run.Tick, units);
        }
    }

    public sealed class ArenaBattleResult
    {
        internal ArenaBattleResult(
            ArenaBattleOutcome outcome,
            ArenaBattleEndReason endReason,
            long finalTick)
        {
            Outcome = outcome;
            EndReason = endReason;
            FinalTick = finalTick;
        }

        public ArenaBattleOutcome Outcome { get; }

        public ArenaBattleEndReason EndReason { get; }

        public long FinalTick { get; }
    }

    public sealed class ArenaReplay
    {
        private readonly ArenaEvent[] _events;
        private readonly ReadOnlyCollection<ArenaEvent> _eventView;
        private readonly ArenaKeyFrame[] _keyFrames;
        private readonly ReadOnlyCollection<ArenaKeyFrame> _keyFrameView;

        internal ArenaReplay(
            ArenaBattleSetup setup,
            ArenaBattleResult result,
            IReadOnlyList<ArenaEvent> events,
            IReadOnlyList<ArenaKeyFrame> keyFrames)
        {
            Seed = setup.Seed;
            TicksPerSecond = setup.TicksPerSecond;
            RulesVersion = setup.Attacker.RulesVersion;
            ContentVersion = setup.Attacker.ContentVersion;
            Attacker = setup.Attacker.Copy();
            Defender = setup.Defender.Copy();
            Result = result ?? throw new ArgumentNullException(nameof(result));
            _events = CopyEvents(events);
            _keyFrames = CopyKeyFrames(keyFrames);
            _eventView = Array.AsReadOnly(_events);
            _keyFrameView = Array.AsReadOnly(_keyFrames);

            if (_keyFrames.Length < 2 || _keyFrames[0].Tick != 0L ||
                _keyFrames[_keyFrames.Length - 1].Tick != result.FinalTick)
            {
                throw new ArgumentException("录像必须包含初始和最终关键帧", nameof(keyFrames));
            }
        }

        public uint Seed { get; }

        public int TicksPerSecond { get; }

        public int RulesVersion { get; }

        public int ContentVersion { get; }

        public ArenaSquadSnapshot Attacker { get; }

        public ArenaSquadSnapshot Defender { get; }

        public ArenaBattleResult Result { get; }

        public IReadOnlyList<ArenaEvent> Events => _eventView;

        public IReadOnlyList<ArenaKeyFrame> KeyFrames => _keyFrameView;

        public ulong ComputeEventHash()
        {
            const ulong offset = 14695981039346656037UL;
            ulong hash = offset;
            Hash(ref hash, (ulong)RulesVersion);
            Hash(ref hash, (ulong)ContentVersion);
            Hash(ref hash, Seed);
            Hash(ref hash, (ulong)Result.Outcome);
            Hash(ref hash, (ulong)Result.EndReason);
            Hash(ref hash, (ulong)Result.FinalTick);

            for (int index = 0; index < _events.Length; index++)
            {
                ArenaEvent current = _events[index];
                Hash(ref hash, (ulong)current.Tick);
                Hash(ref hash, (ulong)current.Kind);
                Hash(ref hash, (ulong)current.SourceSide);
                Hash(ref hash, (ulong)current.SourceUnitId);
                Hash(ref hash, (ulong)current.TargetSide);
                Hash(ref hash, (ulong)current.TargetUnitId);
                Hash(ref hash, (ulong)current.Value);
                Hash(ref hash, (ulong)current.RemainingHealth);
            }

            return hash;
        }

        private static ArenaEvent[] CopyEvents(IReadOnlyList<ArenaEvent> events)
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            ArenaEvent[] result = new ArenaEvent[events.Count];
            for (int index = 0; index < events.Count; index++)
            {
                result[index] = events[index];
            }

            return result;
        }

        private static ArenaKeyFrame[] CopyKeyFrames(IReadOnlyList<ArenaKeyFrame> keyFrames)
        {
            if (keyFrames == null)
            {
                throw new ArgumentNullException(nameof(keyFrames));
            }

            ArenaKeyFrame[] result = new ArenaKeyFrame[keyFrames.Count];
            for (int index = 0; index < keyFrames.Count; index++)
            {
                ArenaKeyFrame keyFrame = keyFrames[index];
                if (keyFrame == null)
                {
                    throw new ArgumentException("录像关键帧不能包含空值", nameof(keyFrames));
                }

                result[index] = keyFrame.Copy();
                if (index > 0 && result[index - 1].Tick >= result[index].Tick)
                {
                    throw new ArgumentException("录像关键帧 Tick 必须严格递增", nameof(keyFrames));
                }
            }

            return result;
        }

        private static void Hash(ref ulong hash, ulong value)
        {
            const ulong prime = 1099511628211UL;
            for (int index = 0; index < sizeof(ulong); index++)
            {
                hash ^= (byte)(value >> (index * 8));
                hash *= prime;
            }
        }
    }

    public sealed class ArenaReplayPlayback
    {
        private readonly Dictionary<long, int> _healthByUnit = new Dictionary<long, int>();
        private readonly ArenaReplay _replay;

        public ArenaReplayPlayback(ArenaReplay replay)
        {
            _replay = replay ?? throw new ArgumentNullException(nameof(replay));
            Seek(0L);
        }

        public long CurrentTick { get; private set; }

        public bool HasEnded => CurrentTick == _replay.Result.FinalTick;

        public ArenaBattleOutcome VisibleOutcome => HasEnded ? _replay.Result.Outcome : ArenaBattleOutcome.None;

        public void Advance(int tickCount)
        {
            if (tickCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickCount));
            }

            Seek(Math.Min(_replay.Result.FinalTick, CurrentTick + tickCount));
        }

        public void SkipToEnd()
        {
            Seek(_replay.Result.FinalTick);
        }

        public void Seek(long targetTick)
        {
            if (targetTick < 0L || targetTick > _replay.Result.FinalTick)
            {
                throw new ArgumentOutOfRangeException(nameof(targetTick));
            }

            ArenaKeyFrame baseline = FindBaseline(targetTick);
            _healthByUnit.Clear();
            for (int index = 0; index < baseline.Units.Count; index++)
            {
                ArenaUnitFrame unit = baseline.Units[index];
                _healthByUnit[CreateUnitKey(unit.Side, unit.UnitId)] = unit.Health;
            }

            for (int index = 0; index < _replay.Events.Count; index++)
            {
                ArenaEvent current = _replay.Events[index];
                if (current.Tick <= baseline.Tick || current.Tick > targetTick ||
                    current.Kind != ArenaEventKind.UnitDamaged)
                {
                    continue;
                }

                _healthByUnit[CreateUnitKey(current.TargetSide, current.TargetUnitId)] = current.RemainingHealth;
            }

            CurrentTick = targetTick;
        }

        public int GetHealth(ArenaSide side, int unitId)
        {
            if (!_healthByUnit.TryGetValue(CreateUnitKey(side, unitId), out int health))
            {
                throw new ArgumentOutOfRangeException(nameof(unitId), "录像中不存在该单位");
            }

            return health;
        }

        private ArenaKeyFrame FindBaseline(long targetTick)
        {
            ArenaKeyFrame result = _replay.KeyFrames[0];
            for (int index = 1; index < _replay.KeyFrames.Count; index++)
            {
                ArenaKeyFrame candidate = _replay.KeyFrames[index];
                if (candidate.Tick > targetTick)
                {
                    break;
                }

                result = candidate;
            }

            return result;
        }

        private static long CreateUnitKey(ArenaSide side, int unitId)
        {
            return ((long)side << 32) | (uint)unitId;
        }
    }
}
