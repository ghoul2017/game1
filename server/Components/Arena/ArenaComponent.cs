using System;
using System.Collections.Generic;
using GameplayCore;
using Shared.Core;
using Shared.Core.Entities;
using Shared.Core.ECS;

namespace Game.Components.Arena;

internal readonly struct ArenaRequestKey : IEquatable<ArenaRequestKey>
{
    internal ArenaRequestKey(long roleId, string requestId)
    {
        RoleId = roleId;
        RequestId = requestId;
    }

    internal long RoleId { get; }

    internal string RequestId { get; }

    public bool Equals(ArenaRequestKey other)
    {
        return RoleId == other.RoleId && string.Equals(RequestId, other.RequestId, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return obj is ArenaRequestKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (RoleId.GetHashCode() * 397) ^ StringComparer.Ordinal.GetHashCode(RequestId);
        }
    }
}

[ComponentOf(typeof(Module))]
public sealed class ArenaComponent : Component
{
    public const int MaximumBattleCount = 128;
    public const int MaximumIdempotencyCount = 4_096;
    public const int MaximumIdempotencyCountPerRole = 64;
    public const long ReplayRetentionMilliseconds = 5L * 60L * 1_000L;
    public const long IdempotencyRetentionMilliseconds = 10L * 60L * 1_000L;

    internal readonly Dictionary<long, ArenaBattleRecord> BattlesById = new();
    internal readonly Dictionary<ArenaRequestKey, ArenaBattleRecord> BattlesByRequest = new();
    internal readonly Queue<long> BattleOrder = new();
    internal readonly Queue<ArenaRequestKey> RequestOrder = new();
    internal readonly Dictionary<long, int> RequestCountsByRole = new();

    private readonly Func<long> _nowMilliseconds;

    public ArenaComponent()
        : this(static () => Environment.TickCount64)
    {
    }

    public ArenaComponent(Func<long> nowMilliseconds)
    {
        _nowMilliseconds = nowMilliseconds ?? throw new ArgumentNullException(nameof(nowMilliseconds));
    }

    public ArenaSquadSnapshot FixedDefender { get; } = ArenaSnapshotMapper.CreateFixedDefender();

    public int BattleCount => BattlesById.Count;

    public int IdempotencyCount => BattlesByRequest.Count;

    public long SimulationRunCount { get; internal set; }

    internal long GetNowMilliseconds()
    {
        return _nowMilliseconds();
    }
}
