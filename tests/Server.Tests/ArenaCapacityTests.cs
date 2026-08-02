using System;
using System.Reflection;
using Game.Components.Arena;
using GameplayCore;
using Xunit;

namespace Server.Tests;

public sealed class ArenaCapacityTests
{
    [Fact]
    public void OldestReplayIsEvictedButRequestResultRemainsIdempotentAtCapacity()
    {
        ArenaComponent arena = new ArenaComponent(() => 0L);
        for (int index = 1; index <= ArenaComponent.MaximumBattleCount; index++)
        {
            ArenaBattleRecord record = index == 1
                ? CreateCompletedRecord(index, index, $"request-{index}")
                : CreateFailedRecord(index, index, $"request-{index}");
            arena.AddPending(record);
        }

        Assert.Equal(ArenaComponent.MaximumBattleCount, arena.BattleCount);
        Assert.True(arena.TryGetByRequest(1L, "request-1", out ArenaBattleRecord originalBeforeEviction));
        Assert.NotNull(originalBeforeEviction.Execution);
        Assert.True(arena.HasCapacity(1_000L));

        ArenaBattleRecord replacement = CreateRecord(1_000L, 1_000L, "replacement");
        arena.AddPending(replacement);

        Assert.Equal(ArenaComponent.MaximumBattleCount, arena.BattleCount);
        Assert.False(arena.TryGetByBattleId(1L, out _));
        Assert.True(arena.TryGetByRequest(1L, "request-1", out ArenaBattleRecord originalRequest));
        Assert.Equal(1L, originalRequest.BattleId);
        Assert.Null(originalRequest.Execution);
        Assert.True(arena.TryGetByBattleId(replacement.BattleId, out ArenaBattleRecord actual));
        Assert.Same(replacement, actual);
    }

    [Fact]
    public void ExpiredRequestIndexReleasesCapacityWithoutPermanentGlobalLockout()
    {
        long nowMilliseconds = 0L;
        ArenaComponent arena = new ArenaComponent(() => nowMilliseconds);
        for (int index = 1; index <= ArenaComponent.MaximumIdempotencyCount; index++)
        {
            ArenaBattleRecord record = CreateFailedRecord(index, index, $"request-{index}");
            arena.AddPending(record);
        }

        Assert.False(arena.HasCapacity(ArenaComponent.MaximumIdempotencyCount + 1L));
        Assert.Equal(ArenaComponent.MaximumIdempotencyCount, arena.IdempotencyCount);
        Assert.True(arena.TryGetByRequest(1L, "request-1", out ArenaBattleRecord first));
        Assert.Equal(1L, first.BattleId);
        Assert.True(arena.TryGetByRequest(
            ArenaComponent.MaximumIdempotencyCount,
            $"request-{ArenaComponent.MaximumIdempotencyCount}",
            out ArenaBattleRecord last));
        Assert.Equal(ArenaComponent.MaximumIdempotencyCount, last.BattleId);

        nowMilliseconds = ArenaComponent.IdempotencyRetentionMilliseconds;

        Assert.True(arena.HasCapacity(ArenaComponent.MaximumIdempotencyCount + 1L));
        Assert.Equal(0, arena.IdempotencyCount);
        Assert.False(arena.TryGetByRequest(1L, "request-1", out _));
    }

    [Fact]
    public void OneRoleCannotConsumeEveryGlobalIdempotencySlot()
    {
        ArenaComponent arena = new ArenaComponent(() => 0L);
        for (int index = 1; index <= ArenaComponent.MaximumIdempotencyCountPerRole; index++)
        {
            arena.AddPending(CreateFailedRecord(index, 1L, $"request-{index}"));
        }

        Assert.False(arena.HasCapacity(1L));
        Assert.True(arena.HasCapacity(2L));
        Assert.Equal(ArenaComponent.MaximumIdempotencyCountPerRole, arena.IdempotencyCount);
    }

    private static ArenaBattleRecord CreateCompletedRecord(long battleId, long roleId, string requestId)
    {
        ArenaBattleRecord record = CreateRecord(battleId, roleId, requestId);
        ArenaSquadSnapshot attacker = new ArenaSquadSnapshot(
            "attacker",
            1,
            1,
            new[] { new ArenaUnitSnapshot(1, 1001, 0, 10, 10, 1) });
        ArenaSquadSnapshot defender = new ArenaSquadSnapshot(
            "defender",
            1,
            1,
            new[] { new ArenaUnitSnapshot(2, 2001, 0, 10, 1, 1) });
        ArenaBattleExecution execution = ArenaBattleRunner.Run(
            new ArenaBattleSetup(1u, 10, 10, 32, 1, attacker, defender));
        MethodInfo complete = typeof(ArenaBattleRecord).GetMethod(
            "Complete",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new MissingMethodException(typeof(ArenaBattleRecord).FullName, "Complete");
        complete.Invoke(record, new object[] { execution });
        return record;
    }

    private static ArenaBattleRecord CreateFailedRecord(long battleId, long roleId, string requestId)
    {
        ArenaBattleRecord record = CreateRecord(battleId, roleId, requestId);
        MethodInfo fail = typeof(ArenaBattleRecord).GetMethod(
            "Fail",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new MissingMethodException(typeof(ArenaBattleRecord).FullName, "Fail");
        fail.Invoke(record, new object[] { "controlled failure" });
        return record;
    }

    private static ArenaBattleRecord CreateRecord(long battleId, long roleId, string requestId)
    {
        object instance = Activator.CreateInstance(
            typeof(ArenaBattleRecord),
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new object[] { battleId, roleId, requestId, 1u },
            null) ?? throw new InvalidOperationException("无法创建竞技场测试记录");
        return (ArenaBattleRecord)instance;
    }
}
