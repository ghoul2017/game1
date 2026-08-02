using System;
using System.Collections.Generic;
using System.Diagnostics;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Components.Expedition;

/// <summary>
/// 以固定实例数、种子和 Tick 数测量单进程远征调度。调用方负责在独立测试进程中运行，避免线上 GC 干扰。
/// </summary>
public static class ExpeditionCapacityProbe
{
    private const string SnapshotMessageName = "Expedition.SnapshotNtf";

    public static ExpeditionCapacityProbeResult Run(
        IExpeditionSimulationFactory factory,
        int instanceCount,
        int warmupTickCount = 120,
        int measuredTickCount = 100)
    {
        ValidateArguments(factory, instanceCount, warmupTickCount, measuredTickCount);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long managedBefore = GC.GetTotalMemory(true);
        long privateBefore = Process.GetCurrentProcess().PrivateMemorySize64;
        List<ProbeInstance> instances = CreateInstances(factory, instanceCount);
        long managedAfterCreation = GC.GetTotalMemory(true);
        long privateAfterCreation = Process.GetCurrentProcess().PrivateMemorySize64;

        for (int tick = 0; tick < warmupTickCount; tick++)
        {
            AdvanceAll(instances, captureSnapshots: tick % ExpeditionComponent.SnapshotIntervalTicks == 0, null);
        }

        double[] durations = new double[measuredTickCount];
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        SnapshotMeasurement measurement = new SnapshotMeasurement();
        int overBudgetTickCount = 0;
        double maximumBacklogMilliseconds = 0.0;

        for (int tick = 0; tick < measuredTickCount; tick++)
        {
            long startedAt = Stopwatch.GetTimestamp();
            bool captureSnapshots = tick % ExpeditionComponent.SnapshotIntervalTicks == 0;
            AdvanceAll(instances, captureSnapshots, measurement);
            double elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            durations[tick] = elapsedMilliseconds;
            if (elapsedMilliseconds > ExpeditionComponent.TickIntervalMilliseconds)
            {
                overBudgetTickCount++;
                maximumBacklogMilliseconds = Math.Max(
                    maximumBacklogMilliseconds,
                    elapsedMilliseconds - ExpeditionComponent.TickIntervalMilliseconds);
            }
        }

        long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
        Array.Sort(durations);
        long managedDelta = Math.Max(0L, managedAfterCreation - managedBefore);
        long privateDelta = Math.Max(0L, privateAfterCreation - privateBefore);
        double averageSnapshotBytes = measurement.SnapshotCount == 0L
            ? 0.0
            : (double)measurement.TotalSnapshotBytes / measurement.SnapshotCount;
        double averageSnapshotPacketBytes = measurement.SnapshotCount == 0L
            ? 0.0
            : (double)measurement.TotalSnapshotPacketBytes / measurement.SnapshotCount;

        return new ExpeditionCapacityProbeResult
        {
            InstanceCount = instanceCount,
            WarmupTickCount = warmupTickCount,
            MeasuredTickCount = measuredTickCount,
            TickP50Milliseconds = Percentile(durations, 0.50),
            TickP95Milliseconds = Percentile(durations, 0.95),
            TickP99Milliseconds = Percentile(durations, 0.99),
            TickMaximumMilliseconds = durations[durations.Length - 1],
            OverBudgetTickCount = overBudgetTickCount,
            MaximumBacklogMilliseconds = maximumBacklogMilliseconds,
            AllocatedBytes = Math.Max(0L, allocatedAfter - allocatedBefore),
            ApproximateManagedBytesPerRun = managedDelta / instanceCount,
            ApproximatePrivateBytesPerRun = privateDelta / instanceCount,
            SnapshotCount = measurement.SnapshotCount,
            AverageSnapshotPayloadBytes = averageSnapshotBytes,
            MaximumSnapshotPayloadBytes = measurement.MaximumSnapshotBytes,
            AverageSnapshotPacketBytes = averageSnapshotPacketBytes,
            MaximumSnapshotPacketBytes = measurement.MaximumSnapshotPacketBytes,
            EstimatedDownstreamBytesPerSecondPerRun = averageSnapshotPacketBytes * 5.0,
            LatestSnapshotQueueUpperBoundBytesPerRun = measurement.MaximumSnapshotPacketBytes,
            SemanticEventCount = measurement.SemanticEventCount,
            EndedRunCount = CountEndedRuns(instances)
        };
    }

    private static List<ProbeInstance> CreateInstances(IExpeditionSimulationFactory factory, int instanceCount)
    {
        List<ProbeInstance> instances = new List<ProbeInstance>(instanceCount);
        for (int index = 0; index < instanceCount; index++)
        {
            long sequence = index + 1L;
            ExpeditionSimulationCreateContext context = new ExpeditionSimulationCreateContext
            {
                UserId = sequence,
                RoleId = sequence,
                RunId = sequence,
                RunIndex = sequence,
                Seed = unchecked(0xB11A0001u + (uint)index),
                FacilityLevel = index % 2
            };
            instances.Add(new ProbeInstance(context, factory.Create(context)));
        }

        return instances;
    }

    private static void AdvanceAll(
        IReadOnlyList<ProbeInstance> instances,
        bool captureSnapshots,
        SnapshotMeasurement? measurement)
    {
        for (int index = 0; index < instances.Count; index++)
        {
            ProbeInstance instance = instances[index];
            instance.Events.Clear();
            if (!instance.Simulation.HasEnded)
            {
                instance.Simulation.Advance(0.0f, 1.0f, 0, instance.Events);
            }

            if (!captureSnapshots || measurement == null)
            {
                continue;
            }

            ExpeditionSnapshotData snapshot = instance.Simulation.CaptureSnapshot(
                instance.Context.RunId,
                instance.Context.RunIndex,
                0L,
                instance.Events);
            byte[] payload = new ExpeditionSnapshotNtf { Snapshot = snapshot }.Serialize();
            byte[] packet = PacketCodec.Encode(SnapshotMessageName, payload);
            measurement.SnapshotCount++;
            measurement.TotalSnapshotBytes += payload.Length;
            measurement.MaximumSnapshotBytes = Math.Max(measurement.MaximumSnapshotBytes, payload.Length);
            measurement.TotalSnapshotPacketBytes += packet.Length;
            measurement.MaximumSnapshotPacketBytes = Math.Max(
                measurement.MaximumSnapshotPacketBytes,
                packet.Length);
            measurement.SemanticEventCount += instance.Events.Count;
        }
    }

    private static int CountEndedRuns(IReadOnlyList<ProbeInstance> instances)
    {
        int count = 0;
        for (int index = 0; index < instances.Count; index++)
        {
            if (instances[index].Simulation.HasEnded)
            {
                count++;
            }
        }

        return count;
    }

    private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        int index = (int)Math.Ceiling(sortedValues.Count * percentile) - 1;
        index = Math.Max(0, Math.Min(sortedValues.Count - 1, index));
        return sortedValues[index];
    }

    private static void ValidateArguments(
        IExpeditionSimulationFactory factory,
        int instanceCount,
        int warmupTickCount,
        int measuredTickCount)
    {
        if (factory == null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        if (instanceCount != 1 && instanceCount != 10 && instanceCount != 50)
        {
            throw new ArgumentOutOfRangeException(nameof(instanceCount), "容量探针只允许测量 1、10、50 局");
        }

        if (warmupTickCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(warmupTickCount));
        }

        if (measuredTickCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(measuredTickCount));
        }
    }

    private sealed class ProbeInstance
    {
        public ProbeInstance(
            ExpeditionSimulationCreateContext context,
            IAuthoritativeExpeditionSimulation simulation)
        {
            Context = context;
            Simulation = simulation ?? throw new InvalidOperationException("容量探针工厂返回了空仿真实例");
        }

        public ExpeditionSimulationCreateContext Context { get; }

        public IAuthoritativeExpeditionSimulation Simulation { get; }

        public List<ExpeditionEventData> Events { get; } = new List<ExpeditionEventData>();
    }

    private sealed class SnapshotMeasurement
    {
        public long SnapshotCount { get; set; }

        public long TotalSnapshotBytes { get; set; }

        public int MaximumSnapshotBytes { get; set; }

        public long TotalSnapshotPacketBytes { get; set; }

        public int MaximumSnapshotPacketBytes { get; set; }

        public long SemanticEventCount { get; set; }
    }
}

public sealed class ExpeditionCapacityProbeResult
{
    public int InstanceCount { get; init; }

    public int WarmupTickCount { get; init; }

    public int MeasuredTickCount { get; init; }

    public double TickP50Milliseconds { get; init; }

    public double TickP95Milliseconds { get; init; }

    public double TickP99Milliseconds { get; init; }

    public double TickMaximumMilliseconds { get; init; }

    public int OverBudgetTickCount { get; init; }

    public double MaximumBacklogMilliseconds { get; init; }

    public long AllocatedBytes { get; init; }

    public long ApproximateManagedBytesPerRun { get; init; }

    public long ApproximatePrivateBytesPerRun { get; init; }

    public long SnapshotCount { get; init; }

    public double AverageSnapshotPayloadBytes { get; init; }

    public int MaximumSnapshotPayloadBytes { get; init; }

    public double AverageSnapshotPacketBytes { get; init; }

    public int MaximumSnapshotPacketBytes { get; init; }

    public double EstimatedDownstreamBytesPerSecondPerRun { get; init; }

    /// <summary>SendLatest 每连接只保留一个普通快照，因此上界是一份最大快照。</summary>
    public int LatestSnapshotQueueUpperBoundBytesPerRun { get; init; }

    public long SemanticEventCount { get; init; }

    public int EndedRunCount { get; init; }

    public long AllocatedBytesPerRunTick => AllocatedBytes / Math.Max(1L, (long)InstanceCount * MeasuredTickCount);
}
