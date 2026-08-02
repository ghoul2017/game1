using Game.Components.Expedition;
using Shared.Common.Protos;
using Xunit;

namespace Server.Tests;

public sealed class ExpeditionRunHostTests
{
    [Fact]
    public void InputValidationRejectsWrongOwnerRunReplayAndForgedValues()
    {
        FakeExpeditionSimulation simulation = new FakeExpeditionSimulation();
        ExpeditionRunHost host = CreateHost(simulation);

        Assert.Equal(
            ExpeditionInputRejectReason.OwnerMismatch,
            host.TryAcceptInput(99L, CreateInput(10L, 1L, 1000, 0), 0L));
        Assert.Equal(
            ExpeditionInputRejectReason.RunMismatch,
            host.TryAcceptInput(20L, CreateInput(11L, 1L, 1000, 0), 0L));
        Assert.Equal(
            ExpeditionInputRejectReason.InvalidSequence,
            host.TryAcceptInput(20L, CreateInput(10L, 0L, 1000, 0), 0L));
        Assert.Equal(
            ExpeditionInputRejectReason.InvalidDirection,
            host.TryAcceptInput(20L, CreateInput(10L, 1L, 1000, 1000), 0L));

        ExpeditionInputNtf invalidTarget = CreateInput(10L, 1L, 0, 0);
        invalidTarget.InteractionTargetId = -1;
        Assert.Equal(
            ExpeditionInputRejectReason.InvalidInteractionTarget,
            host.TryAcceptInput(20L, invalidTarget, 0L));

        Assert.Equal(
            ExpeditionInputRejectReason.None,
            host.TryAcceptInput(20L, CreateInput(10L, 1L, 600, 800), 0L));
        Assert.Equal(
            ExpeditionInputRejectReason.StaleSequence,
            host.TryAcceptInput(20L, CreateInput(10L, 1L, 0, 0), 0L));
        Assert.Equal(6L, host.RejectedInputCount);
    }

    [Fact]
    public void InputRateIsBoundedPerWindow()
    {
        ExpeditionRunHost host = CreateHost(new FakeExpeditionSimulation());
        for (long sequence = 1L; sequence <= 30L; sequence++)
        {
            Assert.Equal(
                ExpeditionInputRejectReason.None,
                host.TryAcceptInput(20L, CreateInput(10L, sequence, 0, 0), 100L));
        }

        Assert.Equal(
            ExpeditionInputRejectReason.RateLimited,
            host.TryAcceptInput(20L, CreateInput(10L, 31L, 0, 0), 100L));
        Assert.Equal(
            ExpeditionInputRejectReason.None,
            host.TryAcceptInput(20L, CreateInput(10L, 31L, 0, 0), 1000L));
    }

    [Fact]
    public void DetachedRunContinuesWithZeroInputUntilRetentionExpires()
    {
        FakeExpeditionSimulation simulation = new FakeExpeditionSimulation();
        ExpeditionRunHost host = CreateHost(simulation);
        ExpeditionInputNtf input = CreateInput(10L, 1L, 1000, 0);
        input.InteractionTargetId = 7;
        Assert.Equal(ExpeditionInputRejectReason.None, host.TryAcceptInput(20L, input, 10L));

        host.Detach(20L, 500L);
        Assert.True(host.Advance(120L));
        Assert.Equal(1L, simulation.Tick);
        Assert.Equal(0.0f, simulation.LastDirectionX);
        Assert.Equal(0.0f, simulation.LastDirectionY);
        Assert.Equal(0, simulation.LastInteractionTargetId);
        Assert.False(host.IsExpired(519L));
        Assert.True(host.IsExpired(520L));
    }

    [Fact]
    public void TerminalResultIsCapturedOnlyFromSimulation()
    {
        FakeExpeditionSimulation simulation = new FakeExpeditionSimulation(1L, 75L, 2);
        ExpeditionRunHost host = CreateHost(simulation);

        Assert.True(host.Advance(100L));
        ExpeditionAuthoritativeResult result = Assert.IsType<ExpeditionAuthoritativeResult>(host.TerminalResult);
        Assert.Equal(10L, result.RunId);
        Assert.Equal(2, result.Phase);
        Assert.Equal(75L, result.GrossResource);
        Assert.Equal(1L, result.EndTick);
        Assert.False(host.Advance(200L));
        Assert.Same(result, host.TerminalResult);
    }

    [Fact]
    public void EventBatchesUseReliableMonotonicSequencePerRun()
    {
        ExpeditionRunHost host = CreateHost(new FakeExpeditionSimulation());
        Assert.Null(host.CreateEventBatch(System.Array.Empty<ExpeditionEventData>()));

        ExpeditionEventData firstEvent = new ExpeditionEventData { Tick = 1L, Kind = 2 };
        ExpeditionEventBatchNtf first = Assert.IsType<ExpeditionEventBatchNtf>(
            host.CreateEventBatch(new[] { firstEvent }));
        ExpeditionEventBatchNtf second = Assert.IsType<ExpeditionEventBatchNtf>(
            host.CreateEventBatch(new[] { new ExpeditionEventData { Tick = 2L, Kind = 3 } }));

        Assert.Equal(host.RunId, first.RunId);
        Assert.Equal(1L, first.EventSequence);
        Assert.Same(firstEvent, Assert.Single(first.Events));
        Assert.Equal(2L, second.EventSequence);
    }

    private static ExpeditionRunHost CreateHost(FakeExpeditionSimulation simulation)
    {
        return new ExpeditionRunHost(1L, 20L, 10L, 1L, 100L, 123u, simulation, 0L);
    }

    private static ExpeditionInputNtf CreateInput(
        long runId,
        long sequence,
        int directionX,
        int directionY)
    {
        return new ExpeditionInputNtf
        {
            RunId = runId,
            InputSequence = sequence,
            DirectionX = directionX,
            DirectionY = directionY
        };
    }
}
