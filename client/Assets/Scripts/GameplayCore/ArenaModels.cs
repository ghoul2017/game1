using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GameplayCore
{
    public static class ArenaLimits
    {
        public const int MaximumUnitsPerSide = 5;
        public const int MaximumTicks = 18_000;
        public const int MaximumEvents = 10_000;
        public const int MaximumBatchCount = 50;
    }

    public enum ArenaSide
    {
        None = 0,
        Attacker = 1,
        Defender = 2
    }

    public enum ArenaBattleOutcome
    {
        None = 0,
        AttackerVictory = 1,
        DefenderVictory = 2,
        Draw = 3
    }

    public enum ArenaBattleEndReason
    {
        None = 0,
        AttackerEliminated = 1,
        DefenderEliminated = 2,
        TickLimitReached = 3,
        EventLimitReached = 4
    }

    public sealed class ArenaUnitSnapshot
    {
        public ArenaUnitSnapshot(
            int unitId,
            int templateId,
            int slot,
            int maxHealth,
            int attackDamage,
            int attackIntervalTicks)
        {
            if (unitId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(unitId), "单位 ID 必须是正数");
            }

            if (templateId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(templateId), "模板 ID 必须是正数");
            }

            if (slot < 0 || slot >= ArenaLimits.MaximumUnitsPerSide)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), "单位槽位超出竞技场上限");
            }

            if (maxHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), "单位生命必须是正数");
            }

            if (attackDamage <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(attackDamage), "单位攻击必须是正数");
            }

            if (attackIntervalTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(attackIntervalTicks), "攻击间隔必须是正数");
            }

            UnitId = unitId;
            TemplateId = templateId;
            Slot = slot;
            MaxHealth = maxHealth;
            AttackDamage = attackDamage;
            AttackIntervalTicks = attackIntervalTicks;
        }

        public int UnitId { get; }

        public int TemplateId { get; }

        public int Slot { get; }

        public int MaxHealth { get; }

        public int AttackDamage { get; }

        public int AttackIntervalTicks { get; }

        internal ArenaUnitSnapshot Copy()
        {
            return new ArenaUnitSnapshot(
                UnitId,
                TemplateId,
                Slot,
                MaxHealth,
                AttackDamage,
                AttackIntervalTicks);
        }
    }

    public sealed class ArenaSquadSnapshot
    {
        private readonly ArenaUnitSnapshot[] _units;
        private readonly ReadOnlyCollection<ArenaUnitSnapshot> _unitView;

        public ArenaSquadSnapshot(
            string snapshotId,
            int rulesVersion,
            int contentVersion,
            IReadOnlyList<ArenaUnitSnapshot> units)
        {
            if (string.IsNullOrWhiteSpace(snapshotId))
            {
                throw new ArgumentException("快照 ID 不能为空", nameof(snapshotId));
            }

            if (rulesVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rulesVersion), "规则版本必须是正数");
            }

            if (contentVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(contentVersion), "内容版本必须是正数");
            }

            if (units == null)
            {
                throw new ArgumentNullException(nameof(units));
            }

            if (units.Count == 0 || units.Count > ArenaLimits.MaximumUnitsPerSide)
            {
                throw new ArgumentException(
                    $"竞技场阵容人数必须在 1 到 {ArenaLimits.MaximumUnitsPerSide} 之间",
                    nameof(units));
            }

            SnapshotId = snapshotId;
            RulesVersion = rulesVersion;
            ContentVersion = contentVersion;
            _units = CopyAndValidateUnits(units);
            _unitView = Array.AsReadOnly(_units);
        }

        public string SnapshotId { get; }

        public int RulesVersion { get; }

        public int ContentVersion { get; }

        public IReadOnlyList<ArenaUnitSnapshot> Units => _unitView;

        internal ArenaSquadSnapshot Copy()
        {
            return new ArenaSquadSnapshot(SnapshotId, RulesVersion, ContentVersion, _units);
        }

        private static ArenaUnitSnapshot[] CopyAndValidateUnits(IReadOnlyList<ArenaUnitSnapshot> units)
        {
            ArenaUnitSnapshot[] result = new ArenaUnitSnapshot[units.Count];
            for (int index = 0; index < units.Count; index++)
            {
                ArenaUnitSnapshot unit = units[index];
                if (unit == null)
                {
                    throw new ArgumentException("阵容不能包含空单位", nameof(units));
                }

                result[index] = unit.Copy();
                for (int previous = 0; previous < index; previous++)
                {
                    if (result[previous].UnitId == result[index].UnitId)
                    {
                        throw new ArgumentException("同一阵容的单位 ID 不能重复", nameof(units));
                    }

                    if (result[previous].Slot == result[index].Slot)
                    {
                        throw new ArgumentException("同一阵容的槽位不能重复", nameof(units));
                    }
                }
            }

            Array.Sort(result, CompareUnits);
            return result;
        }

        private static int CompareUnits(ArenaUnitSnapshot left, ArenaUnitSnapshot right)
        {
            int slotComparison = left.Slot.CompareTo(right.Slot);
            return slotComparison != 0 ? slotComparison : left.UnitId.CompareTo(right.UnitId);
        }
    }

    public sealed class ArenaBattleSetup
    {
        public ArenaBattleSetup(
            uint seed,
            int ticksPerSecond,
            int maximumTicks,
            int maximumEvents,
            int keyFrameIntervalTicks,
            ArenaSquadSnapshot attacker,
            ArenaSquadSnapshot defender)
        {
            if (ticksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ticksPerSecond), "Tick 率必须是正数");
            }

            if (maximumTicks <= 0 || maximumTicks > ArenaLimits.MaximumTicks)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumTicks), "最大 Tick 数超出竞技场上限");
            }

            if (maximumEvents < 8 || maximumEvents > ArenaLimits.MaximumEvents)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumEvents), "最大事件数必须在 8 与竞技场上限之间");
            }

            if (keyFrameIntervalTicks <= 0 || keyFrameIntervalTicks > maximumTicks)
            {
                throw new ArgumentOutOfRangeException(nameof(keyFrameIntervalTicks), "关键帧间隔无效");
            }

            Seed = seed;
            TicksPerSecond = ticksPerSecond;
            MaximumTicks = maximumTicks;
            MaximumEvents = maximumEvents;
            KeyFrameIntervalTicks = keyFrameIntervalTicks;
            Attacker = attacker?.Copy() ?? throw new ArgumentNullException(nameof(attacker));
            Defender = defender?.Copy() ?? throw new ArgumentNullException(nameof(defender));

            if (Attacker.RulesVersion != Defender.RulesVersion)
            {
                throw new ArgumentException("攻守双方规则版本必须一致", nameof(defender));
            }

            if (Attacker.ContentVersion != Defender.ContentVersion)
            {
                throw new ArgumentException("攻守双方内容版本必须一致", nameof(defender));
            }
        }

        public uint Seed { get; }

        public int TicksPerSecond { get; }

        public int MaximumTicks { get; }

        public int MaximumEvents { get; }

        public int KeyFrameIntervalTicks { get; }

        public ArenaSquadSnapshot Attacker { get; }

        public ArenaSquadSnapshot Defender { get; }
    }

    public sealed class ArenaUnitState
    {
        private int _attackCooldownTicksRemaining;
        private int _health;

        internal ArenaUnitState(ArenaUnitSnapshot snapshot)
        {
            UnitId = snapshot.UnitId;
            TemplateId = snapshot.TemplateId;
            Slot = snapshot.Slot;
            MaxHealth = snapshot.MaxHealth;
            AttackDamage = snapshot.AttackDamage;
            AttackIntervalTicks = snapshot.AttackIntervalTicks;
            _health = snapshot.MaxHealth;
        }

        public int UnitId { get; }

        public int TemplateId { get; }

        public int Slot { get; }

        public int MaxHealth { get; }

        public int Health => _health;

        public int AttackDamage { get; }

        public int AttackIntervalTicks { get; }

        public int AttackCooldownTicksRemaining => _attackCooldownTicksRemaining;

        public bool IsAlive => _health > 0;

        internal bool CanAttack => IsAlive && _attackCooldownTicksRemaining == 0;

        internal void AdvanceCooldown()
        {
            if (_attackCooldownTicksRemaining > 0)
            {
                _attackCooldownTicksRemaining--;
            }
        }

        internal void StartAttack()
        {
            _attackCooldownTicksRemaining = AttackIntervalTicks;
        }

        internal int TakeDamage(int damage)
        {
            int actualDamage = Math.Min(_health, damage);
            _health -= actualDamage;
            return actualDamage;
        }
    }

    public sealed class ArenaRun
    {
        private readonly ArenaUnitState[] _attackerUnits;
        private readonly ReadOnlyCollection<ArenaUnitState> _attackerView;
        private readonly ArenaUnitState[] _defenderUnits;
        private readonly ReadOnlyCollection<ArenaUnitState> _defenderView;
        private readonly DeterministicRandom _random;
        private ArenaBattleEndReason _endReason;
        private ArenaBattleOutcome _outcome;
        private long _tick;

        private ArenaRun(ArenaBattleSetup setup)
        {
            Seed = setup.Seed;
            TicksPerSecond = setup.TicksPerSecond;
            MaximumTicks = setup.MaximumTicks;
            MaximumEvents = setup.MaximumEvents;
            KeyFrameIntervalTicks = setup.KeyFrameIntervalTicks;
            RulesVersion = setup.Attacker.RulesVersion;
            ContentVersion = setup.Attacker.ContentVersion;
            AttackerSnapshotId = setup.Attacker.SnapshotId;
            DefenderSnapshotId = setup.Defender.SnapshotId;
            _attackerUnits = CreateStates(setup.Attacker.Units);
            _defenderUnits = CreateStates(setup.Defender.Units);
            _attackerView = Array.AsReadOnly(_attackerUnits);
            _defenderView = Array.AsReadOnly(_defenderUnits);
            _random = new DeterministicRandom(setup.Seed);
        }

        public uint Seed { get; }

        public int TicksPerSecond { get; }

        public int MaximumTicks { get; }

        public int MaximumEvents { get; }

        public int KeyFrameIntervalTicks { get; }

        public int RulesVersion { get; }

        public int ContentVersion { get; }

        public string AttackerSnapshotId { get; }

        public string DefenderSnapshotId { get; }

        public long Tick => _tick;

        public ArenaBattleOutcome Outcome => _outcome;

        public ArenaBattleEndReason EndReason => _endReason;

        public bool HasEnded => _outcome != ArenaBattleOutcome.None;

        public IReadOnlyList<ArenaUnitState> AttackerUnits => _attackerView;

        public IReadOnlyList<ArenaUnitState> DefenderUnits => _defenderView;

        internal ArenaUnitState[] MutableAttackerUnits => _attackerUnits;

        internal ArenaUnitState[] MutableDefenderUnits => _defenderUnits;

        public static ArenaRun Start(ArenaBattleSetup setup)
        {
            if (setup == null)
            {
                throw new ArgumentNullException(nameof(setup));
            }

            return new ArenaRun(setup);
        }

        internal bool NextBoolean()
        {
            return _random.NextInt(2) == 0;
        }

        internal int NextDamagePercent()
        {
            return 90 + _random.NextInt(21);
        }

        internal void BeginTick()
        {
            _tick++;
            AdvanceCooldowns(_attackerUnits);
            AdvanceCooldowns(_defenderUnits);
        }

        internal void Finish(ArenaBattleOutcome outcome, ArenaBattleEndReason reason)
        {
            _outcome = outcome;
            _endReason = reason;
        }

        internal bool HasLivingUnit(ArenaSide side)
        {
            ArenaUnitState[] units = side == ArenaSide.Attacker ? _attackerUnits : _defenderUnits;
            for (int index = 0; index < units.Length; index++)
            {
                if (units[index].IsAlive)
                {
                    return true;
                }
            }

            return false;
        }

        private static ArenaUnitState[] CreateStates(IReadOnlyList<ArenaUnitSnapshot> snapshots)
        {
            ArenaUnitState[] result = new ArenaUnitState[snapshots.Count];
            for (int index = 0; index < snapshots.Count; index++)
            {
                result[index] = new ArenaUnitState(snapshots[index]);
            }

            return result;
        }

        private static void AdvanceCooldowns(ArenaUnitState[] units)
        {
            for (int index = 0; index < units.Length; index++)
            {
                units[index].AdvanceCooldown();
            }
        }
    }
}
