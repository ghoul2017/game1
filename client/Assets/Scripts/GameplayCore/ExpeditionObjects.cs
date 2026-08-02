using System;

namespace GameplayCore
{
    /// <summary>
    /// 保存一名队员在当前一局中的独立生命和自动攻击冷却。
    /// </summary>
    public sealed class ExpeditionMember
    {
        private int _attackCooldownTicksRemaining;
        private int _health;

        internal ExpeditionMember(ExpeditionMemberSetup setup)
        {
            Id = setup.Id;
            MaxHealth = setup.MaxHealth;
            AttackDamage = setup.AttackDamage;
            AttackRange = setup.AttackRange;
            AttackIntervalTicks = setup.AttackIntervalTicks;
            _health = setup.MaxHealth;
        }

        public int Id { get; }

        public int MaxHealth { get; }

        public int Health => _health;

        public int AttackDamage { get; }

        public float AttackRange { get; }

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

    /// <summary>
    /// 保存一个敌人在当前一局中的生命、行动冷却和 Boss 警告状态。
    /// </summary>
    public sealed class ExpeditionEnemy
    {
        private int _actionCooldownTicksRemaining;
        private int _health;
        private long _normalAttackCount;
        private bool _warningActive;
        private long _warningResolveTick;
        private float _warningX;
        private float _warningY;

        internal ExpeditionEnemy(ExpeditionEnemySetup setup)
        {
            Id = setup.Id;
            Kind = setup.Kind;
            X = setup.X;
            Y = setup.Y;
            MaxHealth = setup.MaxHealth;
            AttackDamage = setup.AttackDamage;
            AttackRange = setup.AttackRange;
            ActionIntervalTicks = setup.ActionIntervalTicks;
            BossWarningDelayTicks = setup.BossWarningDelayTicks;
            BossWarningRadius = setup.BossWarningRadius;
            _health = setup.MaxHealth;
        }

        public int Id { get; }

        public ExpeditionEnemyKind Kind { get; }

        public float X { get; }

        public float Y { get; }

        public int MaxHealth { get; }

        public int Health => _health;

        public int AttackDamage { get; }

        public float AttackRange { get; }

        public int ActionIntervalTicks { get; }

        public int ActionCooldownTicksRemaining => _actionCooldownTicksRemaining;

        public int BossWarningDelayTicks { get; }

        public float BossWarningRadius { get; }

        public bool IsAlive => _health > 0;

        public bool WarningActive => _warningActive;

        public long WarningResolveTick => _warningResolveTick;

        public float WarningX => _warningX;

        public float WarningY => _warningY;

        internal bool CanAct => IsAlive && !_warningActive && _actionCooldownTicksRemaining == 0;

        internal long NormalAttackCount => _normalAttackCount;

        internal void AdvanceCooldown()
        {
            if (_actionCooldownTicksRemaining > 0)
            {
                _actionCooldownTicksRemaining--;
            }
        }

        internal void StartNormalAttack()
        {
            _normalAttackCount++;
            _actionCooldownTicksRemaining = ActionIntervalTicks;
        }

        internal void StartWarning(long resolveTick, float x, float y)
        {
            _warningActive = true;
            _warningResolveTick = resolveTick;
            _warningX = x;
            _warningY = y;
            _actionCooldownTicksRemaining = ActionIntervalTicks;
        }

        internal void ResolveWarning()
        {
            ClearWarning();
        }

        internal void ClearWarningForRoomTransition()
        {
            ClearWarning();
        }

        internal int TakeDamage(int damage)
        {
            int actualDamage = Math.Min(_health, damage);
            _health -= actualDamage;
            if (!IsAlive)
            {
                ClearWarning();
            }

            return actualDamage;
        }

        private void ClearWarning()
        {
            _warningActive = false;
            _warningResolveTick = 0L;
            _warningX = 0.0f;
            _warningY = 0.0f;
        }
    }

    /// <summary>
    /// 保存一个资源点在当前一局中的剩余数量和采集冷却。
    /// </summary>
    public sealed class ExpeditionResource
    {
        private int _collectionCooldownTicksRemaining;
        private int _remainingAmount;

        internal ExpeditionResource(ExpeditionResourceSetup setup)
        {
            Id = setup.Id;
            X = setup.X;
            Y = setup.Y;
            InitialAmount = setup.Amount;
            CollectionAmount = setup.CollectionAmount;
            InteractionRange = setup.InteractionRange;
            CollectionIntervalTicks = setup.CollectionIntervalTicks;
            _remainingAmount = setup.Amount;
        }

        public int Id { get; }

        public float X { get; }

        public float Y { get; }

        public int InitialAmount { get; }

        public int RemainingAmount => _remainingAmount;

        public int CollectionAmount { get; }

        public float InteractionRange { get; }

        public int CollectionIntervalTicks { get; }

        public int CollectionCooldownTicksRemaining => _collectionCooldownTicksRemaining;

        public bool IsDepleted => _remainingAmount == 0;

        internal bool CanCollect => !IsDepleted && _collectionCooldownTicksRemaining == 0;

        internal void AdvanceCooldown()
        {
            if (_collectionCooldownTicksRemaining > 0)
            {
                _collectionCooldownTicksRemaining--;
            }
        }

        internal int Collect()
        {
            int collected = Math.Min(_remainingAmount, CollectionAmount);
            _remainingAmount -= collected;
            _collectionCooldownTicksRemaining = CollectionIntervalTicks;
            return collected;
        }
    }
}
