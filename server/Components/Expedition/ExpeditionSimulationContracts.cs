using System;
using System.Collections.Generic;
using Shared.Common.Protos;

namespace Game.Components.Expedition;

/// <summary>
/// 隔离服务端宿主与并行演进的 GameplayCore API；具体适配器只负责创建和投影真实规则局。
/// </summary>
public interface IExpeditionSimulationFactory
{
    IAuthoritativeExpeditionSimulation Create(ExpeditionSimulationCreateContext context);
}

public interface IAuthoritativeExpeditionSimulation
{
    long Tick { get; }

    bool HasEnded { get; }

    int Phase { get; }

    long GrossResource { get; }

    void Advance(
        float directionX,
        float directionY,
        int interactionTargetId,
        ICollection<ExpeditionEventData> outputEvents);

    ExpeditionSnapshotData CaptureSnapshot(
        long runId,
        long runIndex,
        long lastProcessedInputSequence,
        IReadOnlyList<ExpeditionEventData> events);
}

public sealed class ExpeditionSimulationCreateContext
{
    public long UserId { get; init; }

    public long RoleId { get; init; }

    public long RunId { get; init; }

    public long RunIndex { get; init; }

    public uint Seed { get; init; }

    public int FacilityLevel { get; init; }
}

public interface IExpeditionClock
{
    long UtcNowMilliseconds { get; }
}

public sealed class SystemExpeditionClock : IExpeditionClock
{
    public static SystemExpeditionClock Instance { get; } = new SystemExpeditionClock();

    private SystemExpeditionClock()
    {
    }

    public long UtcNowMilliseconds => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

public sealed class ExpeditionAuthoritativeResult
{
    public long RunId { get; init; }

    public int Phase { get; init; }

    public long GrossResource { get; init; }

    public long EndTick { get; init; }
}
