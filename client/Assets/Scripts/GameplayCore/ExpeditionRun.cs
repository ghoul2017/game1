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
        private readonly ExpeditionRoomRuntimeState[] _roomStates;
        private readonly uint _seed;
        private ExpeditionRoomRuntimeState _currentRoomState;
        private ReadOnlyCollection<ExpeditionRoomPublicState> _discoveredRoomView;
        private ExpeditionEnemy[] _enemies;
        private ReadOnlyCollection<ExpeditionEnemy> _enemyView;
        private readonly ExpeditionMember[] _members;
        private readonly ReadOnlyCollection<ExpeditionMember> _memberView;
        private ExpeditionResource[] _resources;
        private ReadOnlyCollection<ExpeditionResource> _resourceView;
        private ReadOnlyCollection<ExpeditionVisibleExit> _visibleExitView;
        private long _collectedResource;
        private bool _exitOpen;
        private int _kills;
        private ExpeditionPhase _phase;
        private ExpeditionResult _result;
        private float _squadX;
        private float _squadY;
        private int _targetId;
        private ExpeditionTargetKind _targetKind;
        private long _tick;

        private ExpeditionRun(ExpeditionSetup setup)
        {
            RunId = setup.RunId;
            _seed = setup.Seed;
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
            _memberView = Array.AsReadOnly(_members);

            if (setup.RoomGraph == null)
            {
                _enemies = CreateEnemies(setup.Enemies);
                _resources = CreateResources(setup.Resources);
                _enemyView = Array.AsReadOnly(_enemies);
                _resourceView = Array.AsReadOnly(_resources);
                _discoveredRoomView = Array.AsReadOnly(Array.Empty<ExpeditionRoomPublicState>());
                _visibleExitView = Array.AsReadOnly(Array.Empty<ExpeditionVisibleExit>());
                return;
            }

            _roomStates = CreateRoomStates(setup.RoomGraph);
            _currentRoomState = FindRoomState(setup.RoomGraph.StartRoomId);
            _currentRoomState.Progress = ExpeditionRoomProgress.Cleared;
            SetCurrentRoomCollections();
            RefreshRoomProjection();
        }

        public long RunId { get; }

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

        public bool UsesRoomGraph => _roomStates != null;

        public int CurrentRoomId => _currentRoomState == null ? 0 : _currentRoomState.Definition.Id;

        public ExpeditionRoomKind CurrentRoomKind => _currentRoomState == null
            ? ExpeditionRoomKind.Start
            : _currentRoomState.Definition.Kind;

        public IReadOnlyList<ExpeditionRoomPublicState> DiscoveredRooms => _discoveredRoomView;

        public IReadOnlyList<ExpeditionVisibleExit> VisibleExits => _visibleExitView;

        public ExpeditionResult Result => _result;

        internal ExpeditionMember[] MutableMembers => _members;

        internal ExpeditionEnemy[] MutableEnemies => _enemies;

        internal ExpeditionResource[] MutableResources => _resources;

        internal ExpeditionRoomProgress CurrentRoomProgress => _currentRoomState == null
            ? ExpeditionRoomProgress.Cleared
            : _currentRoomState.Progress;

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
            _collectedResource = checked(_collectedResource + amount);
        }

        internal void OpenExit()
        {
            _exitOpen = true;
        }

        internal void Finish(ExpeditionPhase phase)
        {
            if (phase == ExpeditionPhase.Running)
            {
                throw new ArgumentOutOfRangeException(nameof(phase), "远征终态不能是 Running");
            }

            if (HasEnded)
            {
                throw new InvalidOperationException("远征只能进入一次终态");
            }

            _phase = phase;
            _targetKind = ExpeditionTargetKind.None;
            _targetId = 0;
            _result = new ExpeditionResult(RunId, _seed, phase, _tick, _kills, _collectedResource);
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

        internal bool IsBossRoomCleared()
        {
            if (_roomStates == null)
            {
                return !HasLivingBoss();
            }

            for (int index = 0; index < _roomStates.Length; index++)
            {
                ExpeditionRoomRuntimeState state = _roomStates[index];
                if (state.Definition.Kind == ExpeditionRoomKind.Boss)
                {
                    return state.Progress == ExpeditionRoomProgress.Cleared;
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

        internal bool TryTransitionRoom(out int previousRoomId, out int currentRoomId, out bool discovered)
        {
            previousRoomId = 0;
            currentRoomId = 0;
            discovered = false;
            if (_currentRoomState == null || _currentRoomState.Progress != ExpeditionRoomProgress.Cleared)
            {
                return false;
            }

            ExpeditionRoomDefinition definition = _currentRoomState.Definition;
            for (int index = 0; index < definition.Exits.Count; index++)
            {
                ExpeditionRoomExit exit = definition.Exits[index];
                GetDoorPosition(exit.Direction, out float doorX, out float doorY);
                if (!IsWithin(_squadX, _squadY, doorX, doorY, GetDoorTriggerRadius()))
                {
                    continue;
                }

                previousRoomId = definition.Id;
                ExpeditionRoomRuntimeState next = FindRoomState(exit.TargetRoomId);
                discovered = next.Progress == ExpeditionRoomProgress.Hidden;
                if (discovered)
                {
                    next.Progress = ExpeditionRoomProgress.Discovered;
                }

                ClearCurrentRoomTransientState();
                _currentRoomState = next;
                currentRoomId = next.Definition.Id;
                SetCurrentRoomCollections();
                PlaceSquadInside(ExpeditionRoomGraph.Opposite(exit.Direction));
                RefreshRoomProjection();
                return true;
            }

            return false;
        }

        internal bool IsCurrentRoomComplete()
        {
            if (_currentRoomState == null || _currentRoomState.Progress == ExpeditionRoomProgress.Cleared)
            {
                return false;
            }

            switch (_currentRoomState.Definition.CompletionKind)
            {
                case ExpeditionRoomCompletionKind.None:
                    return true;
                case ExpeditionRoomCompletionKind.Combat:
                    return AllEnemiesDead(false);
                case ExpeditionRoomCompletionKind.Resource:
                    return AllResourcesDepleted();
                case ExpeditionRoomCompletionKind.MixedAll:
                    return AllEnemiesDead(false) && AllResourcesDepleted();
                case ExpeditionRoomCompletionKind.Boss:
                    return AllEnemiesDead(true);
                case ExpeditionRoomCompletionKind.Exit:
                    return false;
                default:
                    throw new InvalidOperationException("当前房间完成条件无效");
            }
        }

        internal int ClearCurrentRoom()
        {
            if (_currentRoomState == null || _currentRoomState.Progress == ExpeditionRoomProgress.Cleared)
            {
                return 0;
            }

            _currentRoomState.Progress = ExpeditionRoomProgress.Cleared;
            int reward = _currentRoomState.Definition.CompletionResourceReward;
            if (reward > 0)
            {
                AddResource(reward);
            }

            RefreshRoomProjection();
            return reward;
        }

        internal bool TryIssueDangerHint(out int targetRoomId)
        {
            targetRoomId = 0;
            if (_currentRoomState == null || _currentRoomState.DangerHintIssued ||
                _currentRoomState.Progress != ExpeditionRoomProgress.Cleared)
            {
                return false;
            }

            ExpeditionRoomDefinition definition = _currentRoomState.Definition;
            for (int index = 0; index < definition.Exits.Count; index++)
            {
                ExpeditionRoomDefinition target = FindRoomState(definition.Exits[index].TargetRoomId).Definition;
                if (target.Kind != ExpeditionRoomKind.Boss)
                {
                    continue;
                }

                _currentRoomState.DangerHintIssued = true;
                targetRoomId = target.Id;
                RefreshRoomProjection();
                return true;
            }

            return false;
        }

        internal bool IsInExitRoom()
        {
            return _currentRoomState != null && _currentRoomState.Definition.Kind == ExpeditionRoomKind.Exit;
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

        private static ExpeditionRoomRuntimeState[] CreateRoomStates(ExpeditionRoomGraph graph)
        {
            ExpeditionRoomRuntimeState[] result = new ExpeditionRoomRuntimeState[graph.Rooms.Count];
            for (int index = 0; index < graph.Rooms.Count; index++)
            {
                result[index] = new ExpeditionRoomRuntimeState(graph.Rooms[index]);
            }

            return result;
        }

        private ExpeditionRoomRuntimeState FindRoomState(int id)
        {
            for (int index = 0; index < _roomStates.Length; index++)
            {
                if (_roomStates[index].Definition.Id == id)
                {
                    return _roomStates[index];
                }
            }

            return null;
        }

        private void SetCurrentRoomCollections()
        {
            _enemies = _currentRoomState.Enemies;
            _resources = _currentRoomState.Resources;
            _enemyView = (ReadOnlyCollection<ExpeditionEnemy>)_currentRoomState.EnemyView;
            _resourceView = (ReadOnlyCollection<ExpeditionResource>)_currentRoomState.ResourceView;
        }

        private void RefreshRoomProjection()
        {
            List<ExpeditionRoomPublicState> discovered = new List<ExpeditionRoomPublicState>();
            for (int index = 0; index < _roomStates.Length; index++)
            {
                ExpeditionRoomRuntimeState state = _roomStates[index];
                if (state.Progress == ExpeditionRoomProgress.Hidden)
                {
                    continue;
                }

                discovered.Add(new ExpeditionRoomPublicState(
                    state.Definition.Id,
                    state.Definition.Kind,
                    state.Progress,
                    ReferenceEquals(state, _currentRoomState)));
            }

            _discoveredRoomView = discovered.AsReadOnly();

            List<ExpeditionVisibleExit> exits = new List<ExpeditionVisibleExit>();
            ExpeditionRoomDefinition definition = _currentRoomState.Definition;
            for (int index = 0; index < definition.Exits.Count; index++)
            {
                ExpeditionRoomExit exit = definition.Exits[index];
                ExpeditionRoomDefinition target = FindRoomState(exit.TargetRoomId).Definition;
                GetDoorPosition(exit.Direction, out float x, out float y);
                exits.Add(new ExpeditionVisibleExit(
                    exit.Direction,
                    exit.TargetRoomId,
                    _currentRoomState.Progress == ExpeditionRoomProgress.Cleared,
                    target.Kind == ExpeditionRoomKind.Boss && _currentRoomState.DangerHintIssued,
                    x,
                    y));
            }

            _visibleExitView = exits.AsReadOnly();
        }

        private void ClearCurrentRoomTransientState()
        {
            _targetKind = ExpeditionTargetKind.None;
            _targetId = 0;
            for (int index = 0; index < _enemies.Length; index++)
            {
                _enemies[index].ClearWarningForRoomTransition();
            }
        }

        private void PlaceSquadInside(ExpeditionDoorDirection entryDirection)
        {
            float horizontalPadding = Math.Min(1.0f, (MaximumX - MinimumX) * 0.25f);
            float verticalPadding = Math.Min(1.0f, (MaximumY - MinimumY) * 0.25f);
            float centerX = (MinimumX + MaximumX) * 0.5f;
            float centerY = (MinimumY + MaximumY) * 0.5f;

            switch (entryDirection)
            {
                case ExpeditionDoorDirection.North:
                    _squadX = centerX;
                    _squadY = MaximumY - verticalPadding;
                    break;
                case ExpeditionDoorDirection.East:
                    _squadX = MaximumX - horizontalPadding;
                    _squadY = centerY;
                    break;
                case ExpeditionDoorDirection.South:
                    _squadX = centerX;
                    _squadY = MinimumY + verticalPadding;
                    break;
                case ExpeditionDoorDirection.West:
                    _squadX = MinimumX + horizontalPadding;
                    _squadY = centerY;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(entryDirection), "房门方向无效");
            }
        }

        private void GetDoorPosition(ExpeditionDoorDirection direction, out float x, out float y)
        {
            x = (MinimumX + MaximumX) * 0.5f;
            y = (MinimumY + MaximumY) * 0.5f;
            switch (direction)
            {
                case ExpeditionDoorDirection.North:
                    y = MaximumY;
                    return;
                case ExpeditionDoorDirection.East:
                    x = MaximumX;
                    return;
                case ExpeditionDoorDirection.South:
                    y = MinimumY;
                    return;
                case ExpeditionDoorDirection.West:
                    x = MinimumX;
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), "房门方向无效");
            }
        }

        private float GetDoorTriggerRadius()
        {
            return Math.Min(0.75f, Math.Min(MaximumX - MinimumX, MaximumY - MinimumY) * 0.1f);
        }

        private bool AllEnemiesDead(bool requireBoss)
        {
            bool bossFound = false;
            for (int index = 0; index < _enemies.Length; index++)
            {
                bossFound |= _enemies[index].Kind == ExpeditionEnemyKind.Boss;
                if (_enemies[index].IsAlive)
                {
                    return false;
                }
            }

            return !requireBoss || bossFound;
        }

        private bool AllResourcesDepleted()
        {
            for (int index = 0; index < _resources.Length; index++)
            {
                if (!_resources[index].IsDepleted)
                {
                    return false;
                }
            }

            return true;
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

        private static bool IsWithin(float firstX, float firstY, float secondX, float secondY, float radius)
        {
            double deltaX = (double)firstX - secondX;
            double deltaY = (double)firstY - secondY;
            return deltaX * deltaX + deltaY * deltaY <= (double)radius * radius;
        }
    }
}
