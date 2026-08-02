using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Components.Expedition;

public sealed class ExpeditionMetrics
{
    private const int MaximumTickSamples = 4096;
    private readonly Queue<double> _tickMilliseconds = new();

    public int ActiveRuns { get; internal set; }

    public int PeakActiveRuns { get; internal set; }

    public long AdvancedRuns { get; internal set; }

    public long SnapshotCount { get; internal set; }

    public long ResultNotificationCount { get; internal set; }

    public long RejectedInputCount { get; internal set; }

    public long ExpiredRunCount { get; internal set; }

    public long AllocatedBytes { get; internal set; }

    public void RecordTick(double elapsedMilliseconds)
    {
        if (_tickMilliseconds.Count >= MaximumTickSamples)
        {
            _tickMilliseconds.Dequeue();
        }

        _tickMilliseconds.Enqueue(elapsedMilliseconds);
    }

    public ExpeditionMetricsSnapshot Capture()
    {
        double[] sortedSamples = _tickMilliseconds.OrderBy(value => value).ToArray();
        return new ExpeditionMetricsSnapshot
        {
            ActiveRuns = ActiveRuns,
            PeakActiveRuns = PeakActiveRuns,
            AdvancedRuns = AdvancedRuns,
            SnapshotCount = SnapshotCount,
            ResultNotificationCount = ResultNotificationCount,
            RejectedInputCount = RejectedInputCount,
            ExpiredRunCount = ExpiredRunCount,
            AllocatedBytes = AllocatedBytes,
            TickP50Milliseconds = Percentile(sortedSamples, 0.50),
            TickP95Milliseconds = Percentile(sortedSamples, 0.95),
            TickP99Milliseconds = Percentile(sortedSamples, 0.99)
        };
    }

    private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
        {
            return 0.0;
        }

        int index = (int)Math.Ceiling(sortedValues.Count * percentile) - 1;
        index = Math.Clamp(index, 0, sortedValues.Count - 1);
        return sortedValues[index];
    }
}

public sealed class ExpeditionMetricsSnapshot
{
    public int ActiveRuns { get; init; }

    public int PeakActiveRuns { get; init; }

    public long AdvancedRuns { get; init; }

    public long SnapshotCount { get; init; }

    public long ResultNotificationCount { get; init; }

    public long RejectedInputCount { get; init; }

    public long ExpiredRunCount { get; init; }

    public long AllocatedBytes { get; init; }

    public double TickP50Milliseconds { get; init; }

    public double TickP95Milliseconds { get; init; }

    public double TickP99Milliseconds { get; init; }
}
