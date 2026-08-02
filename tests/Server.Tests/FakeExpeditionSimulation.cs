using System.Collections.Generic;
using Game.Components.Expedition;
using Shared.Common.Protos;

namespace Server.Tests;

internal sealed class FakeExpeditionSimulation : IAuthoritativeExpeditionSimulation
{
    private readonly long _endAfterTick;

    internal FakeExpeditionSimulation(long endAfterTick = long.MaxValue, long grossResource = 0L, int phase = 1)
    {
        _endAfterTick = endAfterTick;
        GrossResource = grossResource;
        TerminalPhase = phase;
    }

    public long Tick { get; private set; }

    public bool HasEnded => Tick >= _endAfterTick;

    public int Phase => HasEnded ? TerminalPhase : 0;

    public long GrossResource { get; }

    public int TerminalPhase { get; }

    public float LastDirectionX { get; private set; }

    public float LastDirectionY { get; private set; }

    public int LastInteractionTargetId { get; private set; }

    public void Advance(
        float directionX,
        float directionY,
        int interactionTargetId,
        ICollection<ExpeditionEventData> outputEvents)
    {
        Tick++;
        LastDirectionX = directionX;
        LastDirectionY = directionY;
        LastInteractionTargetId = interactionTargetId;
        outputEvents.Add(new ExpeditionEventData { Tick = Tick, Kind = 1 });
    }

    public ExpeditionSnapshotData CaptureSnapshot(
        long runId,
        long runIndex,
        long lastProcessedInputSequence,
        IReadOnlyList<ExpeditionEventData> events)
    {
        ExpeditionSnapshotData snapshot = new ExpeditionSnapshotData
        {
            RunId = runId,
            RunIndex = runIndex,
            ServerTick = Tick,
            LastProcessedInputSequence = lastProcessedInputSequence,
            Phase = Phase,
            SquadX = LastDirectionX,
            SquadY = LastDirectionY
        };
        for (int index = 0; index < events.Count; index++)
        {
            snapshot.Events.Add(events[index]);
        }

        return snapshot;
    }
}

internal sealed class FakeExpeditionSimulationFactory : IExpeditionSimulationFactory
{
    private readonly long _endAfterTick;
    private readonly long _grossResource;
    private readonly int _phase;

    internal FakeExpeditionSimulationFactory(
        long endAfterTick = long.MaxValue,
        long grossResource = 0L,
        int phase = 1)
    {
        _endAfterTick = endAfterTick;
        _grossResource = grossResource;
        _phase = phase;
    }

    internal FakeExpeditionSimulation? LatestSimulation { get; private set; }

    public IAuthoritativeExpeditionSimulation Create(ExpeditionSimulationCreateContext context)
    {
        LatestSimulation = new FakeExpeditionSimulation(_endAfterTick, _grossResource, _phase);
        return LatestSimulation;
    }
}

internal sealed class FakeExpeditionClock : IExpeditionClock
{
    public long UtcNowMilliseconds { get; set; }
}
