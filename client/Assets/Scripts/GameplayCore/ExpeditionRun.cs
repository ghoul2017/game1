using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GameplayCore
{
    /// <summary>
    /// 表示一局远征当前所处的唯一阶段。
    /// </summary>
    public enum ExpeditionPhase
    {
        Running = 0,
        Completed = 1,
        Failed = 2
    }

    /// <summary>
    /// 区分队伍自动行动当前锁定的对象种类。
    /// </summary>
    public enum ExpeditionTargetKind
    {
        None = 0,
        Enemy = 1,
        Resource = 2
    }

    /// <summary>
    /// 持有一局远征的全部权威局内状态，并仅向外公开只读数据。
    /// </summary>
    public sealed class ExpeditionRun
    {
        private readonly ExpeditionEnemy[] _enemies;
        private readonly ReadOnlyCollection<ExpeditionEnemy> _enemyView;
        private readonly ExpeditionMember[] _members;
        private readonly ReadOnlyCollection<ExpeditionMember> _memberView;
        private readonly ExpeditionResource[] _resources;
        private readonly ReadOnlyCollection<ExpeditionResource> _resourceView;
        private long _collectedResource;
        private bool _exitOpen;
        private int _kills;
        private ExpeditionPhase _phase;
        private float _squadX;
        private float _squadY;
        private int _targetId;
        private ExpeditionTargetKind _targetKind;
        private long _tick;

        private ExpeditionRun(ExpeditionSetup setup)
        {
            TicksPerSecond = setup.TicksPerSecond;
            MoveSpeedPerSecond = setup.MoveSpeedPerSecond;
            MovePerTick = setup.MoveSpeedPerSecond / setup.TicksPerSecond;
            MinimumX = setup.MinimumX;
            MaximumX = setup.MaximumX;
            MinimumY = setup.MinimumY;
            MaximumY = setup.MaximumY;
            TargetAcquireRadius = setup.TargetAcquireRadius;
            TargetReleaseRadius = setup.TargetAcquireRadius + setup.TargetReleaseMargin;
            EnemyThreatRadius = setup.EnemyThreatRadius;
            ExitX = setup.ExitX;
            ExitY = setup.ExitY;
            ExitRadius = setup.ExitRadius;
            _squadX = setup.InitialSquadX;
            _squadY = setup.InitialSquadY;
            _phase = ExpeditionPhase.Running;

            _members = CreateMembers(setup.Members);
            _enemies = CreateEnemies(setup.Enemies);
            _resources = CreateResources(setup.Resources);
            _memberView = Array.AsReadOnly(_members);
            _enemyView = Array.AsReadOnly(_enemies);
            _resourceView = Array.AsReadOnly(_resources);
        }

        public int TicksPerSecond { get; }

        public float MoveSpeedPerSecond { get; }

        public float MovePerTick { get; }

        public float MinimumX { get; }

        public float MaximumX { get; }

        public float MinimumY { get; }

        public float MaximumY { get; }

        public float TargetAcquireRadius { get; }

        public float TargetReleaseRadius { get; }

        public float EnemyThreatRadius { get; }

        public float ExitX { get; }

        public float ExitY { get; }

        public float ExitRadius { get; }

        public long Tick => _tick;

        public float SquadX => _squadX;

        public float SquadY => _squadY;

        public ExpeditionPhase Phase => _phase;

        public ExpeditionTargetKind TargetKind => _targetKind;

        public int TargetId => _targetId;

        public IReadOnlyList<ExpeditionMember> Members => _memberView;

        public IReadOnlyList<ExpeditionEnemy> Enemies => _enemyView;

        public IReadOnlyList<ExpeditionResource> Resources => _resourceView;

        public int Kills => _kills;

        public long CollectedResource => _collectedResource;

        public bool ExitOpen => _exitOpen;

        public bool HasEnded => _phase != ExpeditionPhase.Running;

        internal ExpeditionMember[] MutableMembers => _members;

        internal ExpeditionEnemy[] MutableEnemies => _enemies;

        internal ExpeditionResource[] MutableResources => _resources;

        public static ExpeditionRun Start(ExpeditionSetup setup)
        {
            ExpeditionSetupValidator.Validate(setup);
            return new ExpeditionRun(setup);
        }

        internal void Move(float deltaX, float deltaY)
        {
            _tick++;
            _squadX = Clamp((double)_squadX + deltaX, MinimumX, MaximumX);
            _squadY = Clamp((double)_squadY + deltaY, MinimumY, MaximumY);
        }

        internal void SetTarget(ExpeditionTargetKind kind, int id)
        {
            _targetKind = kind;
            _targetId = id;
        }

        internal void AddKill()
        {
            _kills++;
        }

        internal void AddResource(int amount)
        {
            _collectedResource += amount;
        }

        internal void OpenExit()
        {
            _exitOpen = true;
        }

        internal void Finish(ExpeditionPhase phase)
        {
            _phase = phase;
            _targetKind = ExpeditionTargetKind.None;
            _targetId = 0;
        }

        internal bool HasLivingMember()
        {
            for (int index = 0; index < _members.Length; index++)
            {
                if (_members[index].IsAlive)
                {
                    return true;
                }
            }

            return false;
        }

        internal bool HasLivingBoss()
        {
            for (int index = 0; index < _enemies.Length; index++)
            {
                if (_enemies[index].Kind == ExpeditionEnemyKind.Boss && _enemies[index].IsAlive)
                {
                    return true;
                }
            }

            return false;
        }

        internal ExpeditionEnemy FindEnemy(int id)
        {
            for (int index = 0; index < _enemies.Length; index++)
            {
                if (_enemies[index].Id == id)
                {
                    return _enemies[index];
                }
            }

            return null;
        }

        internal ExpeditionResource FindResource(int id)
        {
            for (int index = 0; index < _resources.Length; index++)
            {
                if (_resources[index].Id == id)
                {
                    return _resources[index];
                }
            }

            return null;
        }

        private static ExpeditionMember[] CreateMembers(ExpeditionMemberSetup[] setups)
        {
            ExpeditionMember[] result = new ExpeditionMember[setups.Length];
            for (int index = 0; index < setups.Length; index++)
            {
                result[index] = new ExpeditionMember(setups[index]);
            }

            Array.Sort(result, CompareMembers);
            return result;
        }

        private static ExpeditionEnemy[] CreateEnemies(ExpeditionEnemySetup[] setups)
        {
            ExpeditionEnemy[] result = new ExpeditionEnemy[setups.Length];
            for (int index = 0; index < setups.Length; index++)
            {
                result[index] = new ExpeditionEnemy(setups[index]);
            }

            Array.Sort(result, CompareEnemies);
            return result;
        }

        private static ExpeditionResource[] CreateResources(ExpeditionResourceSetup[] setups)
        {
            ExpeditionResource[] result = new ExpeditionResource[setups.Length];
            for (int index = 0; index < setups.Length; index++)
            {
                result[index] = new ExpeditionResource(setups[index]);
            }

            Array.Sort(result, CompareResources);
            return result;
        }

        private static int CompareMembers(ExpeditionMember left, ExpeditionMember right)
        {
            return left.Id.CompareTo(right.Id);
        }

        private static int CompareEnemies(ExpeditionEnemy left, ExpeditionEnemy right)
        {
            return left.Id.CompareTo(right.Id);
        }

        private static int CompareResources(ExpeditionResource left, ExpeditionResource right)
        {
            return left.Id.CompareTo(right.Id);
        }

        private static float Clamp(double value, float minimum, float maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            return value > maximum ? maximum : (float)value;
        }
    }
}
