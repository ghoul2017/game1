using Game.Components.Expedition;
using Xunit;

namespace Server.Tests;

[Collection("ExpeditionCapacity")]
public sealed class ExpeditionCapacityProbeTests
{
    private readonly ITestOutputHelper _output;

    public ExpeditionCapacityProbeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void RealGameplayCoreWorkloadStaysWithinSingleWriterBudget()
    {
        int[] instanceCounts = { 1, 10, 50 };
        for (int index = 0; index < instanceCounts.Length; index++)
        {
            ExpeditionCapacityProbeResult result = ExpeditionCapacityProbe.Run(
                new GameplayCoreExpeditionSimulationFactory(),
                instanceCounts[index],
                warmupTickCount: 120,
                measuredTickCount: 100);

            _output.WriteLine(
                $"instances={result.InstanceCount} " +
                $"p50={result.TickP50Milliseconds:F3}ms " +
                $"p95={result.TickP95Milliseconds:F3}ms " +
                $"p99={result.TickP99Milliseconds:F3}ms " +
                $"managed/run={result.ApproximateManagedBytesPerRun}B " +
                $"alloc/runTick={result.AllocatedBytesPerRunTick}B " +
                $"snapshot/avg={result.AverageSnapshotPayloadBytes:F1}B " +
                $"snapshot/max={result.MaximumSnapshotPayloadBytes}B " +
                $"packet/avg={result.AverageSnapshotPacketBytes:F1}B " +
                $"packet/max={result.MaximumSnapshotPacketBytes}B " +
                $"down/run={result.EstimatedDownstreamBytesPerSecondPerRun:F1}Bps " +
                $"latestQueue/run<={result.LatestSnapshotQueueUpperBoundBytesPerRun}B " +
                $"events={result.SemanticEventCount}");

            Assert.True(result.TickP95Milliseconds < ExpeditionComponent.TickIntervalMilliseconds);
            Assert.Equal(0, result.OverBudgetTickCount);
            Assert.InRange(result.MaximumSnapshotPayloadBytes, 1, 256 * 1024);
            Assert.Equal(result.MaximumSnapshotPacketBytes, result.LatestSnapshotQueueUpperBoundBytesPerRun);
            Assert.InRange(result.AllocatedBytesPerRunTick, 0L, 1024L * 1024L);
        }
    }
}
