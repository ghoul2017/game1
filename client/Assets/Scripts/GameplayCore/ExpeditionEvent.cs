using System;

namespace GameplayCore
{
    /// <summary>
    /// 区分远征事件中的队员、敌人和资源点。
    /// </summary>
    public enum ExpeditionObjectKind
    {
        None = 0,
        Member = 1,
        Enemy = 2,
        Resource = 3
    }

    /// <summary>
    /// 区分一个固定 Tick 内产生的局内语义事件。
    /// </summary>
    public enum ExpeditionEventKind
    {
        TargetChanged = 0,
        MemberAttacked = 1,
        EnemyAttacked = 2,
        MemberDamaged = 3,
        MemberDied = 4,
        EnemyDamaged = 5,
        EnemyDied = 6,
        ResourceCollected = 7,
        ResourceDepleted = 8,
        BossWarningStarted = 9,
        BossWarningHit = 10,
        BossWarningDodged = 11,
        ExitOpened = 12,
        Completed = 13,
        Failed = 14
    }

    /// <summary>
    /// 携带 View 和行为测试所需的 Tick、对象、数值与事件位置。
    /// </summary>
    public readonly struct ExpeditionEvent : IEquatable<ExpeditionEvent>
    {
        internal ExpeditionEvent(
            long tick,
            ExpeditionEventKind kind,
            ExpeditionObjectKind sourceKind,
            int sourceId,
            ExpeditionObjectKind targetKind,
            int targetId,
            int value,
            float x,
            float y)
        {
            Tick = tick;
            Kind = kind;
            SourceKind = sourceKind;
            SourceId = sourceId;
            TargetKind = targetKind;
            TargetId = targetId;
            Value = value;
            X = x;
            Y = y;
        }

        public long Tick { get; }

        public ExpeditionEventKind Kind { get; }

        public ExpeditionObjectKind SourceKind { get; }

        public int SourceId { get; }

        public ExpeditionObjectKind TargetKind { get; }

        public int TargetId { get; }

        public int Value { get; }

        public float X { get; }

        public float Y { get; }

        public bool Equals(ExpeditionEvent other)
        {
            return Tick == other.Tick && Kind == other.Kind && SourceKind == other.SourceKind &&
                SourceId == other.SourceId && TargetKind == other.TargetKind && TargetId == other.TargetId &&
                Value == other.Value && X.Equals(other.X) && Y.Equals(other.Y);
        }

        public override bool Equals(object obj)
        {
            return obj is ExpeditionEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Tick.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Kind;
                hashCode = (hashCode * 397) ^ (int)SourceKind;
                hashCode = (hashCode * 397) ^ SourceId;
                hashCode = (hashCode * 397) ^ (int)TargetKind;
                hashCode = (hashCode * 397) ^ TargetId;
                hashCode = (hashCode * 397) ^ Value;
                hashCode = (hashCode * 397) ^ X.GetHashCode();
                hashCode = (hashCode * 397) ^ Y.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(ExpeditionEvent left, ExpeditionEvent right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ExpeditionEvent left, ExpeditionEvent right)
        {
            return !left.Equals(right);
        }
    }
}
