using System;
using GameplayCore;

namespace Game.Components.Arena;

public sealed class ArenaBattleRecord
{
    internal ArenaBattleRecord(long battleId, long roleId, string requestId, uint seed)
    {
        if (battleId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(battleId));
        }

        if (roleId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(roleId));
        }

        BattleId = battleId;
        RoleId = roleId;
        RequestId = requestId ?? throw new ArgumentNullException(nameof(requestId));
        Seed = seed;
    }

    public long BattleId { get; }

    public long RoleId { get; }

    public string RequestId { get; }

    public uint Seed { get; }

    public ArenaBattleExecution? Execution { get; private set; }

    public ArenaBattleOutcome Outcome { get; private set; }

    public ArenaBattleEndReason EndReason { get; private set; }

    public long FinalTick { get; private set; }

    public ulong EventHash { get; private set; }

    public string ErrorMessage { get; private set; } = string.Empty;

    public bool HasFinished { get; private set; }

    internal long RegisteredAtMilliseconds { get; private set; } = -1L;

    internal void MarkRegistered(long nowMilliseconds)
    {
        if (nowMilliseconds < 0L)
        {
            throw new ArgumentOutOfRangeException(nameof(nowMilliseconds));
        }

        if (RegisteredAtMilliseconds >= 0L)
        {
            throw new InvalidOperationException("竞技场战斗记录已经注册");
        }

        RegisteredAtMilliseconds = nowMilliseconds;
    }

    internal void Complete(ArenaBattleExecution execution)
    {
        if (HasFinished)
        {
            throw new InvalidOperationException("竞技场战斗记录已经进入终态");
        }

        Execution = execution ?? throw new ArgumentNullException(nameof(execution));
        Outcome = execution.Result.Outcome;
        EndReason = execution.Result.EndReason;
        FinalTick = execution.Result.FinalTick;
        EventHash = execution.Replay.ComputeEventHash();
        HasFinished = true;
    }

    internal void Fail(string errorMessage)
    {
        if (HasFinished)
        {
            throw new InvalidOperationException("竞技场战斗记录已经进入终态");
        }

        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "竞技场战斗模拟失败" : errorMessage;
        HasFinished = true;
    }

    internal void ReleaseReplay()
    {
        if (!HasFinished)
        {
            throw new InvalidOperationException("竞技场战斗尚未进入终态");
        }

        Execution = null;
    }
}
