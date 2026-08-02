using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace GameplayCore
{
    public enum ExpeditionRoomKind
    {
        Start = 0,
        Combat = 1,
        Resource = 2,
        Mixed = 3,
        Boss = 4,
        Exit = 5
    }

    public enum ExpeditionRoomProgress
    {
        Hidden = 0,
        Discovered = 1,
        Cleared = 2
    }

    public enum ExpeditionRoomCompletionKind
    {
        None = 0,
        Combat = 1,
        Resource = 2,
        MixedAll = 3,
        Boss = 4,
        Exit = 5
    }

    public enum ExpeditionDoorDirection
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3
    }

    public readonly struct ExpeditionRoomExit
    {
        public ExpeditionRoomExit(ExpeditionDoorDirection direction, int targetRoomId)
        {
            Direction = direction;
            TargetRoomId = targetRoomId;
        }

        public ExpeditionDoorDirection Direction { get; }

        public int TargetRoomId { get; }
    }

    public sealed class ExpeditionRoomDefinition
    {
        private readonly ExpeditionEnemySetup[] _enemySetups;
        private readonly ReadOnlyCollection<ExpeditionRoomExit> _exitView;
        private readonly ExpeditionResourceSetup[] _resourceSetups;

        public ExpeditionRoomDefinition(
            int id,
            ExpeditionRoomKind kind,
            ExpeditionRoomCompletionKind completionKind,
            bool isOptional,
            int completionResourceReward,
            ExpeditionRoomExit[] exits,
            ExpeditionEnemySetup[] enemies,
            ExpeditionResourceSetup[] resources)
        {
            Id = id;
            Kind = kind;
            CompletionKind = completionKind;
            IsOptional = isOptional;
            CompletionResourceReward = completionResourceReward;

            ExpeditionRoomExit[] exitCopy = exits == null
                ? throw new ArgumentNullException(nameof(exits))
                : (ExpeditionRoomExit[])exits.Clone();
            _exitView = Array.AsReadOnly(exitCopy);
            _enemySetups = CloneEnemies(enemies);
            _resourceSetups = CloneResources(resources);
        }

        public int Id { get; }

        public ExpeditionRoomKind Kind { get; }

        public ExpeditionRoomCompletionKind CompletionKind { get; }

        public bool IsOptional { get; }

        public int CompletionResourceReward { get; }

        public IReadOnlyList<ExpeditionRoomExit> Exits => _exitView;

        internal int EnemyCount => _enemySetups.Length;

        internal int ResourceCount => _resourceSetups.Length;

        internal int InitialResourceAmount
        {
            get
            {
                int total = 0;
                for (int index = 0; index < _resourceSetups.Length; index++)
                {
                    total = checked(total + _resourceSetups[index].Amount);
                }

                return total;
            }
        }

        internal ExpeditionEnemy[] CreateEnemies()
        {
            ExpeditionEnemy[] result = new ExpeditionEnemy[_enemySetups.Length];
            for (int index = 0; index < _enemySetups.Length; index++)
            {
                result[index] = new ExpeditionEnemy(_enemySetups[index]);
            }

            Array.Sort(result, CompareEnemies);
            return result;
        }

        internal ExpeditionResource[] CreateResources()
        {
            ExpeditionResource[] result = new ExpeditionResource[_resourceSetups.Length];
            for (int index = 0; index < _resourceSetups.Length; index++)
            {
                result[index] = new ExpeditionResource(_resourceSetups[index]);
            }

            Array.Sort(result, CompareResources);
            return result;
        }

        internal IEnumerable<int> EnumerateEntityIds()
        {
            for (int index = 0; index < _enemySetups.Length; index++)
            {
                yield return _enemySetups[index].Id;
            }

            for (int index = 0; index < _resourceSetups.Length; index++)
            {
                yield return _resourceSetups[index].Id;
            }
        }

        internal int CountBosses()
        {
            int count = 0;
            for (int index = 0; index < _enemySetups.Length; index++)
            {
                if (_enemySetups[index].Kind == ExpeditionEnemyKind.Boss)
                {
                    count++;
                }
            }

            return count;
        }

        internal void ValidateContent(ExpeditionSetup setup)
        {
            for (int index = 0; index < _enemySetups.Length; index++)
            {
                ExpeditionSetupValidator.ValidateRoomEnemy(setup, _enemySetups[index]);
            }

            for (int index = 0; index < _resourceSetups.Length; index++)
            {
                ExpeditionSetupValidator.ValidateRoomResource(setup, _resourceSetups[index]);
            }
        }

        internal string GetContentSignature()
        {
            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < _enemySetups.Length; index++)
            {
                ExpeditionEnemySetup enemy = _enemySetups[index];
                builder.Append('E').Append(enemy.Id).Append(':').Append((int)enemy.Kind).Append(':')
                    .Append(BitConverter.SingleToInt32Bits(enemy.X)).Append(':')
                    .Append(BitConverter.SingleToInt32Bits(enemy.Y)).Append(':')
                    .Append(enemy.MaxHealth).Append(':').Append(enemy.AttackDamage).Append(':')
                    .Append(BitConverter.SingleToInt32Bits(enemy.AttackRange)).Append(':')
                    .Append(enemy.ActionIntervalTicks).Append(':').Append(enemy.BossWarningDelayTicks).Append(':')
                    .Append(BitConverter.SingleToInt32Bits(enemy.BossWarningRadius)).Append(';');
            }

            for (int index = 0; index < _resourceSetups.Length; index++)
            {
                ExpeditionResourceSetup resource = _resourceSetups[index];
                builder.Append('R').Append(resource.Id).Append(':')
                    .Append(BitConverter.SingleToInt32Bits(resource.X)).Append(':')
                    .Append(BitConverter.SingleToInt32Bits(resource.Y)).Append(':')
                    .Append(resource.Amount).Append(':').Append(resource.CollectionAmount).Append(':')
                    .Append(BitConverter.SingleToInt32Bits(resource.InteractionRange)).Append(':')
                    .Append(resource.CollectionIntervalTicks).Append(';');
            }

            return builder.ToString();
        }

        private static ExpeditionEnemySetup[] CloneEnemies(ExpeditionEnemySetup[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            ExpeditionEnemySetup[] result = new ExpeditionEnemySetup[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                ExpeditionEnemySetup value = source[index] ??
                    throw new ArgumentException("房间敌人不能包含空值", nameof(source));
                result[index] = new ExpeditionEnemySetup
                {
                    Id = value.Id,
                    Kind = value.Kind,
                    X = value.X,
                    Y = value.Y,
                    MaxHealth = value.MaxHealth,
                    AttackDamage = value.AttackDamage,
                    AttackRange = value.AttackRange,
                    ActionIntervalTicks = value.ActionIntervalTicks,
                    BossWarningDelayTicks = value.BossWarningDelayTicks,
                    BossWarningRadius = value.BossWarningRadius
                };
            }

            return result;
        }

        private static ExpeditionResourceSetup[] CloneResources(ExpeditionResourceSetup[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            ExpeditionResourceSetup[] result = new ExpeditionResourceSetup[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                ExpeditionResourceSetup value = source[index] ??
                    throw new ArgumentException("房间资源不能包含空值", nameof(source));
                result[index] = new ExpeditionResourceSetup
                {
                    Id = value.Id,
                    X = value.X,
                    Y = value.Y,
                    Amount = value.Amount,
                    CollectionAmount = value.CollectionAmount,
                    InteractionRange = value.InteractionRange,
                    CollectionIntervalTicks = value.CollectionIntervalTicks
                };
            }

            return result;
        }

        private static int CompareEnemies(ExpeditionEnemy left, ExpeditionEnemy right)
        {
            return left.Id.CompareTo(right.Id);
        }

        private static int CompareResources(ExpeditionResource left, ExpeditionResource right)
        {
            return left.Id.CompareTo(right.Id);
        }
    }

    public sealed class ExpeditionRoomGraph
    {
        private readonly ExpeditionRoomDefinition[] _rooms;
        private readonly ReadOnlyCollection<ExpeditionRoomDefinition> _roomView;

        public ExpeditionRoomGraph(uint seed, int upgradeCost, ExpeditionRoomDefinition[] rooms)
        {
            Seed = seed == 0u ? DeterministicRandom.Mix(0u, 0u) : seed;
            UpgradeCost = upgradeCost;
            _rooms = rooms == null
                ? throw new ArgumentNullException(nameof(rooms))
                : (ExpeditionRoomDefinition[])rooms.Clone();
            Array.Sort(_rooms, CompareRooms);
            Validate();
            _roomView = Array.AsReadOnly(_rooms);
        }

        public uint Seed { get; }

        public int UpgradeCost { get; }

        public int StartRoomId { get; private set; }

        public int BossRoomId { get; private set; }

        public int ExitRoomId { get; private set; }

        public IReadOnlyList<ExpeditionRoomDefinition> Rooms => _roomView;

        public ExpeditionRoomDefinition FindRoom(int id)
        {
            for (int index = 0; index < _rooms.Length; index++)
            {
                if (_rooms[index].Id == id)
                {
                    return _rooms[index];
                }
            }

            return null;
        }

        public string GetSignature()
        {
            StringBuilder builder = new StringBuilder(512);
            builder.Append(Seed).Append('|').Append(UpgradeCost);
            for (int roomIndex = 0; roomIndex < _rooms.Length; roomIndex++)
            {
                ExpeditionRoomDefinition room = _rooms[roomIndex];
                builder.Append('|').Append(room.Id).Append(':').Append((int)room.Kind).Append(':')
                    .Append((int)room.CompletionKind).Append(':').Append(room.IsOptional ? 1 : 0).Append(':')
                    .Append(room.CompletionResourceReward).Append(':').Append(room.GetContentSignature());
                for (int exitIndex = 0; exitIndex < room.Exits.Count; exitIndex++)
                {
                    ExpeditionRoomExit exit = room.Exits[exitIndex];
                    builder.Append((int)exit.Direction).Append('>').Append(exit.TargetRoomId).Append(',');
                }
            }

            return builder.ToString();
        }

        public ulong GetSignatureHash()
        {
            string signature = GetSignature();
            ulong hash = 14695981039346656037UL;
            for (int index = 0; index < signature.Length; index++)
            {
                char value = signature[index];
                hash ^= (byte)value;
                hash *= 1099511628211UL;
                hash ^= (byte)(value >> 8);
                hash *= 1099511628211UL;
            }

            return hash;
        }

        private void Validate()
        {
            if (_rooms.Length < 6 || _rooms.Length > 10)
            {
                throw new ArgumentException("房间总数必须在 6 到 10 之间", nameof(_rooms));
            }

            if (UpgradeCost <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(UpgradeCost), "升级价格必须是正数");
            }

            int startCount = 0;
            int bossRoomCount = 0;
            int exitCount = 0;
            int optionalCount = 0;
            int bossEntityCount = 0;
            int guaranteedReward = 0;
            bool optionalResourceFound = false;
            HashSet<int> roomIds = new HashSet<int>();
            HashSet<int> entityIds = new HashSet<int>();

            for (int index = 0; index < _rooms.Length; index++)
            {
                ExpeditionRoomDefinition room = _rooms[index] ??
                    throw new ArgumentException("房间定义不能包含空值", nameof(_rooms));
                ValidateRoom(room, roomIds, entityIds);

                if (room.Kind == ExpeditionRoomKind.Start)
                {
                    startCount++;
                    StartRoomId = room.Id;
                }
                else if (room.Kind == ExpeditionRoomKind.Boss)
                {
                    bossRoomCount++;
                    BossRoomId = room.Id;
                }
                else if (room.Kind == ExpeditionRoomKind.Exit)
                {
                    exitCount++;
                    ExitRoomId = room.Id;
                }

                if (room.IsOptional)
                {
                    optionalCount++;
                    optionalResourceFound |= room.InitialResourceAmount > 0 || room.CompletionResourceReward > 0;
                }
                else
                {
                    guaranteedReward = checked(guaranteedReward + room.CompletionResourceReward);
                }

                bossEntityCount += room.CountBosses();
            }

            if (startCount != 1 || bossRoomCount != 1 || exitCount != 1 || bossEntityCount != 1)
            {
                throw new ArgumentException("房间图必须且只能包含一个起点、Boss 房、出口房和 Boss 实体", nameof(_rooms));
            }

            if (optionalCount < 1 || optionalCount > 2 || !optionalResourceFound)
            {
                throw new ArgumentException("房间图必须包含一到两个可选支路，且至少一个支路提供资源", nameof(_rooms));
            }

            if (guaranteedReward < UpgradeCost)
            {
                throw new ArgumentException("主线完成奖励必须足以支付首次升级", nameof(_rooms));
            }

            ValidateConnections();
            RequireReachable(StartRoomId, false, _rooms.Length);
            RequireReachable(StartRoomId, true, _rooms.Length - optionalCount);

            if (!CanReach(BossRoomId, ExitRoomId, true))
            {
                throw new ArgumentException("Boss 房必须能沿主路线到达出口房", nameof(_rooms));
            }
        }

        private static void ValidateRoom(
            ExpeditionRoomDefinition room,
            HashSet<int> roomIds,
            HashSet<int> entityIds)
        {
            if (room.Id <= 0 || !roomIds.Add(room.Id))
            {
                throw new ArgumentException("房间 ID 必须是唯一正数", nameof(room));
            }

            if (room.CompletionResourceReward < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(room), "房间完成奖励不能为负数");
            }

            if (room.IsOptional && (room.Kind == ExpeditionRoomKind.Start ||
                room.Kind == ExpeditionRoomKind.Boss || room.Kind == ExpeditionRoomKind.Exit))
            {
                throw new ArgumentException("起点、Boss 和出口不能是可选房间", nameof(room));
            }

            ValidateRoomContentShape(room);

            foreach (int entityId in room.EnumerateEntityIds())
            {
                if (entityId <= 0 || !entityIds.Add(entityId))
                {
                    throw new ArgumentException("局内实体 ID 必须在整局唯一且为正数", nameof(room));
                }
            }
        }

        private static void ValidateRoomContentShape(ExpeditionRoomDefinition room)
        {
            ExpeditionRoomCompletionKind expectedCompletion;
            switch (room.Kind)
            {
                case ExpeditionRoomKind.Start:
                    expectedCompletion = ExpeditionRoomCompletionKind.None;
                    break;
                case ExpeditionRoomKind.Combat:
                    expectedCompletion = ExpeditionRoomCompletionKind.Combat;
                    break;
                case ExpeditionRoomKind.Resource:
                    expectedCompletion = ExpeditionRoomCompletionKind.Resource;
                    break;
                case ExpeditionRoomKind.Mixed:
                    expectedCompletion = ExpeditionRoomCompletionKind.MixedAll;
                    break;
                case ExpeditionRoomKind.Boss:
                    expectedCompletion = ExpeditionRoomCompletionKind.Boss;
                    break;
                case ExpeditionRoomKind.Exit:
                    expectedCompletion = ExpeditionRoomCompletionKind.Exit;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(room), "房间类型无效");
            }

            if (room.CompletionKind != expectedCompletion)
            {
                throw new ArgumentException("房间类型与完成条件不匹配", nameof(room));
            }

            int bossCount = room.CountBosses();
            bool validContent;
            switch (room.Kind)
            {
                case ExpeditionRoomKind.Start:
                case ExpeditionRoomKind.Exit:
                    validContent = room.EnemyCount == 0 && room.ResourceCount == 0;
                    break;
                case ExpeditionRoomKind.Combat:
                    validContent = room.EnemyCount > 0 && room.ResourceCount == 0 && bossCount == 0;
                    break;
                case ExpeditionRoomKind.Resource:
                    validContent = room.EnemyCount == 0 && room.ResourceCount > 0;
                    break;
                case ExpeditionRoomKind.Mixed:
                    validContent = room.EnemyCount > 0 && room.ResourceCount > 0 && bossCount == 0;
                    break;
                case ExpeditionRoomKind.Boss:
                    validContent = room.EnemyCount == 1 && room.ResourceCount == 0 && bossCount == 1;
                    break;
                default:
                    validContent = false;
                    break;
            }

            if (!validContent)
            {
                throw new ArgumentException("房间内容与房间类型不匹配", nameof(room));
            }
        }

        private void ValidateConnections()
        {
            for (int roomIndex = 0; roomIndex < _rooms.Length; roomIndex++)
            {
                ExpeditionRoomDefinition room = _rooms[roomIndex];
                HashSet<ExpeditionDoorDirection> directions = new HashSet<ExpeditionDoorDirection>();
                HashSet<int> targets = new HashSet<int>();

                for (int exitIndex = 0; exitIndex < room.Exits.Count; exitIndex++)
                {
                    ExpeditionRoomExit exit = room.Exits[exitIndex];
                    ExpeditionRoomDefinition target = FindRoom(exit.TargetRoomId);
                    if (target == null || target.Id == room.Id)
                    {
                        throw new ArgumentException("房门不能自环或引用不存在的房间", nameof(_rooms));
                    }

                    if (!directions.Add(exit.Direction) || !targets.Add(exit.TargetRoomId))
                    {
                        throw new ArgumentException("同一房间不能重复使用门槽或目标", nameof(_rooms));
                    }

                    ExpeditionDoorDirection reverse = Opposite(exit.Direction);
                    bool reciprocal = false;
                    for (int targetExitIndex = 0; targetExitIndex < target.Exits.Count; targetExitIndex++)
                    {
                        ExpeditionRoomExit targetExit = target.Exits[targetExitIndex];
                        if (targetExit.Direction == reverse && targetExit.TargetRoomId == room.Id)
                        {
                            reciprocal = true;
                            break;
                        }
                    }

                    if (!reciprocal)
                    {
                        throw new ArgumentException("房门必须双向一致", nameof(_rooms));
                    }
                }
            }
        }

        private void RequireReachable(int startRoomId, bool ignoreOptional, int expectedCount)
        {
            HashSet<int> visited = new HashSet<int>();
            Queue<int> queue = new Queue<int>();
            visited.Add(startRoomId);
            queue.Enqueue(startRoomId);

            while (queue.Count > 0)
            {
                ExpeditionRoomDefinition room = FindRoom(queue.Dequeue());
                for (int index = 0; index < room.Exits.Count; index++)
                {
                    ExpeditionRoomDefinition target = FindRoom(room.Exits[index].TargetRoomId);
                    if (ignoreOptional && target.IsOptional)
                    {
                        continue;
                    }

                    if (visited.Add(target.Id))
                    {
                        queue.Enqueue(target.Id);
                    }
                }
            }

            if (visited.Count != expectedCount)
            {
                throw new ArgumentException("房间图必须全图连通，且主路线不能依赖可选支路", nameof(_rooms));
            }
        }

        private bool CanReach(int startRoomId, int targetRoomId, bool ignoreOptional)
        {
            HashSet<int> visited = new HashSet<int>();
            Queue<int> queue = new Queue<int>();
            visited.Add(startRoomId);
            queue.Enqueue(startRoomId);

            while (queue.Count > 0)
            {
                ExpeditionRoomDefinition room = FindRoom(queue.Dequeue());
                if (room.Id == targetRoomId)
                {
                    return true;
                }

                for (int index = 0; index < room.Exits.Count; index++)
                {
                    ExpeditionRoomDefinition next = FindRoom(room.Exits[index].TargetRoomId);
                    if (ignoreOptional && next.IsOptional)
                    {
                        continue;
                    }

                    if (visited.Add(next.Id))
                    {
                        queue.Enqueue(next.Id);
                    }
                }
            }

            return false;
        }

        internal static ExpeditionDoorDirection Opposite(ExpeditionDoorDirection direction)
        {
            switch (direction)
            {
                case ExpeditionDoorDirection.North:
                    return ExpeditionDoorDirection.South;
                case ExpeditionDoorDirection.East:
                    return ExpeditionDoorDirection.West;
                case ExpeditionDoorDirection.South:
                    return ExpeditionDoorDirection.North;
                case ExpeditionDoorDirection.West:
                    return ExpeditionDoorDirection.East;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), "房门方向无效");
            }
        }

        private static int CompareRooms(ExpeditionRoomDefinition left, ExpeditionRoomDefinition right)
        {
            return left.Id.CompareTo(right.Id);
        }
    }

    public static class ExpeditionRoomGraphGenerator
    {
        public static ExpeditionRoomGraph Generate(
            uint seed,
            int roomCount,
            int upgradeCost,
            ExpeditionSetup template)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            ExpeditionSetupValidator.Validate(template);
            if (roomCount < 6 || roomCount > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(roomCount), "房间数必须在 6 到 10 之间");
            }

            if (upgradeCost <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(upgradeCost), "升级价格必须是正数");
            }

            uint normalizedSeed = seed == 0u ? DeterministicRandom.Mix(0u, 0u) : seed;
            DeterministicRandom random = new DeterministicRandom(normalizedSeed);
            int branchCount = roomCount >= 8 ? 2 : 1;
            int mainRoomCount = roomCount - branchCount;
            List<ExpeditionRoomExit>[] exits = CreateExitLists(roomCount);
            ConnectMainRoute(exits, mainRoomCount);
            ConnectBranches(exits, mainRoomCount, branchCount, random);

            ExpeditionRoomDefinition[] rooms = new ExpeditionRoomDefinition[roomCount];
            for (int roomId = 1; roomId <= roomCount; roomId++)
            {
                bool optional = roomId > mainRoomCount;
                ExpeditionRoomKind kind = GetRoomKind(roomId, mainRoomCount, optional, random);
                ExpeditionRoomCompletionKind completionKind = GetCompletionKind(kind);
                int completionReward = kind == ExpeditionRoomKind.Boss
                    ? upgradeCost
                    : optional ? Math.Max(1, upgradeCost / 3) : 0;
                rooms[roomId - 1] = new ExpeditionRoomDefinition(
                    roomId,
                    kind,
                    completionKind,
                    optional,
                    completionReward,
                    exits[roomId - 1].ToArray(),
                    CreateEnemies(template, roomId, kind),
                    CreateResources(template, roomId, kind, optional));
            }

            return new ExpeditionRoomGraph(normalizedSeed, upgradeCost, rooms);
        }

        private static List<ExpeditionRoomExit>[] CreateExitLists(int roomCount)
        {
            List<ExpeditionRoomExit>[] result = new List<ExpeditionRoomExit>[roomCount];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = new List<ExpeditionRoomExit>();
            }

            return result;
        }

        private static void ConnectMainRoute(List<ExpeditionRoomExit>[] exits, int mainRoomCount)
        {
            for (int roomId = 1; roomId < mainRoomCount; roomId++)
            {
                exits[roomId - 1].Add(new ExpeditionRoomExit(ExpeditionDoorDirection.North, roomId + 1));
                exits[roomId].Add(new ExpeditionRoomExit(ExpeditionDoorDirection.South, roomId));
            }
        }

        private static void ConnectBranches(
            List<ExpeditionRoomExit>[] exits,
            int mainRoomCount,
            int branchCount,
            DeterministicRandom random)
        {
            int[] parents = new int[branchCount];
            for (int branchIndex = 0; branchIndex < branchCount; branchIndex++)
            {
                int parent;
                do
                {
                    parent = 2 + random.NextInt(mainRoomCount - 3);
                }
                while (Contains(parents, branchIndex, parent));

                parents[branchIndex] = parent;
                int branchRoomId = mainRoomCount + branchIndex + 1;
                ExpeditionDoorDirection direction = random.NextInt(2) == 0
                    ? ExpeditionDoorDirection.East
                    : ExpeditionDoorDirection.West;
                exits[parent - 1].Add(new ExpeditionRoomExit(direction, branchRoomId));
                exits[branchRoomId - 1].Add(new ExpeditionRoomExit(
                    ExpeditionRoomGraph.Opposite(direction),
                    parent));
            }

        }

        private static bool Contains(int[] values, int count, int value)
        {
            for (int index = 0; index < count; index++)
            {
                if (values[index] == value)
                {
                    return true;
                }
            }

            return false;
        }

        private static ExpeditionRoomKind GetRoomKind(
            int roomId,
            int mainRoomCount,
            bool optional,
            DeterministicRandom random)
        {
            if (roomId == 1)
            {
                return ExpeditionRoomKind.Start;
            }

            if (optional)
            {
                return ExpeditionRoomKind.Resource;
            }

            if (roomId == mainRoomCount - 1)
            {
                return ExpeditionRoomKind.Boss;
            }

            if (roomId == mainRoomCount)
            {
                return ExpeditionRoomKind.Exit;
            }

            if (roomId == 2)
            {
                return ExpeditionRoomKind.Combat;
            }

            int choice = random.NextInt(3);
            return choice == 0
                ? ExpeditionRoomKind.Combat
                : choice == 1 ? ExpeditionRoomKind.Resource : ExpeditionRoomKind.Mixed;
        }

        private static ExpeditionRoomCompletionKind GetCompletionKind(ExpeditionRoomKind kind)
        {
            switch (kind)
            {
                case ExpeditionRoomKind.Start:
                    return ExpeditionRoomCompletionKind.None;
                case ExpeditionRoomKind.Combat:
                    return ExpeditionRoomCompletionKind.Combat;
                case ExpeditionRoomKind.Resource:
                    return ExpeditionRoomCompletionKind.Resource;
                case ExpeditionRoomKind.Mixed:
                    return ExpeditionRoomCompletionKind.MixedAll;
                case ExpeditionRoomKind.Boss:
                    return ExpeditionRoomCompletionKind.Boss;
                case ExpeditionRoomKind.Exit:
                    return ExpeditionRoomCompletionKind.Exit;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), "房间类型无效");
            }
        }

        private static ExpeditionEnemySetup[] CreateEnemies(
            ExpeditionSetup template,
            int roomId,
            ExpeditionRoomKind kind)
        {
            if (kind != ExpeditionRoomKind.Combat && kind != ExpeditionRoomKind.Mixed &&
                kind != ExpeditionRoomKind.Boss)
            {
                return Array.Empty<ExpeditionEnemySetup>();
            }

            ExpeditionEnemySetup source = FindEnemyTemplate(template, kind == ExpeditionRoomKind.Boss);
            int count = kind == ExpeditionRoomKind.Combat ? 2 : 1;
            ExpeditionEnemySetup[] result = new ExpeditionEnemySetup[count];
            float centerX = (template.MinimumX + template.MaximumX) * 0.5f;
            float centerY = (template.MinimumY + template.MaximumY) * 0.5f;
            for (int index = 0; index < count; index++)
            {
                result[index] = new ExpeditionEnemySetup
                {
                    Id = roomId * 1000 + index + 1,
                    Kind = source.Kind,
                    X = centerX + (index == 0 ? -1.0f : 1.0f),
                    Y = centerY + index * 1.5f,
                    MaxHealth = source.MaxHealth,
                    AttackDamage = source.AttackDamage,
                    AttackRange = source.AttackRange,
                    ActionIntervalTicks = source.ActionIntervalTicks,
                    BossWarningDelayTicks = source.BossWarningDelayTicks,
                    BossWarningRadius = source.BossWarningRadius
                };
            }

            return result;
        }

        private static ExpeditionResourceSetup[] CreateResources(
            ExpeditionSetup template,
            int roomId,
            ExpeditionRoomKind kind,
            bool optional)
        {
            if (kind != ExpeditionRoomKind.Resource && kind != ExpeditionRoomKind.Mixed)
            {
                return Array.Empty<ExpeditionResourceSetup>();
            }

            float centerX = (template.MinimumX + template.MaximumX) * 0.5f;
            float centerY = (template.MinimumY + template.MaximumY) * 0.5f;
            float availableWidth = template.MaximumX - template.MinimumX;
            float span = Math.Min(4.0f, Math.Max(0.0f, availableWidth - 2.0f));
            ExpeditionResourceSetup[] result = new ExpeditionResourceSetup[template.Resources.Length];
            for (int index = 0; index < result.Length; index++)
            {
                ExpeditionResourceSetup source = template.Resources[index];
                float x = result.Length <= 1
                    ? centerX
                    : centerX - span * 0.5f + span * index / (result.Length - 1);
                result[index] = new ExpeditionResourceSetup
                {
                    Id = roomId * 1000 + 501 + index,
                    X = x,
                    Y = centerY + 2.0f,
                    Amount = optional ? source.Amount : Math.Max(source.CollectionAmount, source.Amount / 2),
                    CollectionAmount = source.CollectionAmount,
                    InteractionRange = source.InteractionRange,
                    CollectionIntervalTicks = source.CollectionIntervalTicks
                };
            }

            return result;
        }

        private static ExpeditionEnemySetup FindEnemyTemplate(ExpeditionSetup template, bool boss)
        {
            for (int index = 0; index < template.Enemies.Length; index++)
            {
                ExpeditionEnemySetup enemy = template.Enemies[index];
                if ((enemy.Kind == ExpeditionEnemyKind.Boss) == boss)
                {
                    return enemy;
                }
            }

            throw new ArgumentException("开局模板缺少所需敌人类型", nameof(template));
        }
    }

    public sealed class ExpeditionRoomPublicState
    {
        internal ExpeditionRoomPublicState(
            int id,
            ExpeditionRoomKind kind,
            ExpeditionRoomProgress progress,
            bool isCurrent)
        {
            Id = id;
            Kind = kind;
            Progress = progress;
            IsCurrent = isCurrent;
        }

        public int Id { get; }

        public ExpeditionRoomKind Kind { get; }

        public ExpeditionRoomProgress Progress { get; }

        public bool IsCurrent { get; }
    }

    public readonly struct ExpeditionVisibleExit
    {
        internal ExpeditionVisibleExit(
            ExpeditionDoorDirection direction,
            int targetRoomId,
            bool isOpen,
            bool hasDangerHint,
            float x,
            float y)
        {
            Direction = direction;
            TargetRoomId = targetRoomId;
            IsOpen = isOpen;
            HasDangerHint = hasDangerHint;
            X = x;
            Y = y;
        }

        public ExpeditionDoorDirection Direction { get; }

        public int TargetRoomId { get; }

        public bool IsOpen { get; }

        public bool HasDangerHint { get; }

        public float X { get; }

        public float Y { get; }
    }

    internal sealed class ExpeditionRoomRuntimeState
    {
        private readonly ReadOnlyCollection<ExpeditionEnemy> _enemyView;
        private readonly ReadOnlyCollection<ExpeditionResource> _resourceView;

        internal ExpeditionRoomRuntimeState(ExpeditionRoomDefinition definition)
        {
            Definition = definition;
            Enemies = definition.CreateEnemies();
            Resources = definition.CreateResources();
            _enemyView = Array.AsReadOnly(Enemies);
            _resourceView = Array.AsReadOnly(Resources);
            Progress = ExpeditionRoomProgress.Hidden;
        }

        internal ExpeditionRoomDefinition Definition { get; }

        internal ExpeditionEnemy[] Enemies { get; }

        internal ExpeditionResource[] Resources { get; }

        internal IReadOnlyList<ExpeditionEnemy> EnemyView => _enemyView;

        internal IReadOnlyList<ExpeditionResource> ResourceView => _resourceView;

        internal ExpeditionRoomProgress Progress { get; set; }

        internal bool DangerHintIssued { get; set; }
    }
}
