using System.Collections.Concurrent;
using System.Threading;
using NeuralResonanceEngine.Protocol;

// Admission and accounting for per-target dispatch queues. Keeping this separate
// from the scheduler makes the overload policy reusable and directly testable.
internal static class DispatchQueueRuntime
{
    public static bool TryEnqueue(
        ConcurrentDictionary<string, ConcurrentQueue<QueuedDispatchBatch>> queuesByTarget,
        string targetInstanceKey,
        QueuedDispatchBatch batch,
        DispatchQueueMetrics metrics,
        int maxQueueBatches,
        int maxQueueSpikes)
    {
        if (batch.Spikes.Count == 0)
        {
            return false;
        }

        var queuedBatches = Interlocked.Increment(ref metrics.QueuedBatches);
        UpdatePeak(ref metrics.PeakQueuedBatches, queuedBatches);
        var queuedSpikes = Interlocked.Add(ref metrics.QueuedSpikes, batch.Spikes.Count);
        UpdatePeak(ref metrics.PeakQueuedSpikes, queuedSpikes);
        if (queuedBatches > maxQueueBatches || queuedSpikes > maxQueueSpikes)
        {
            Interlocked.Decrement(ref metrics.QueuedBatches);
            Interlocked.Add(ref metrics.QueuedSpikes, -batch.Spikes.Count);
            Interlocked.Increment(ref metrics.DroppedBatches);
            Interlocked.Add(ref metrics.DroppedSpikes, batch.Spikes.Count);
            return false;
        }

        var queue = queuesByTarget.GetOrAdd(targetInstanceKey, _ => new ConcurrentQueue<QueuedDispatchBatch>());
        queue.Enqueue(batch);
        return true;
    }

    public static List<DispatchFlushTarget> DrainTargets(
        ConcurrentDictionary<string, ConcurrentQueue<QueuedDispatchBatch>> queuesByTarget,
        out int flushedBatches)
    {
        flushedBatches = 0;
        var targets = new List<DispatchFlushTarget>(queuesByTarget.Count);

        try
        {
            foreach (var targetEntry in queuesByTarget)
            {
                var batches = ListPool<QueuedDispatchBatch>.Rent();
                var mergedCapacity = 0;
                while (targetEntry.Value.TryDequeue(out var batch))
                {
                    if (batch.Spikes.Count <= 0)
                    {
                        continue;
                    }

                    batches.Add(batch);
                    mergedCapacity += batch.Spikes.Count;
                }

                if (batches.Count == 0 || mergedCapacity <= 0)
                {
                    ListPool<QueuedDispatchBatch>.Return(batches);
                    continue;
                }

                var mergedSpikes = ListPool<SpikeMessage>.Rent(mergedCapacity);
                foreach (var batch in batches)
                {
                    mergedSpikes.AddRange(batch.Spikes);
                }

                if (mergedSpikes.Count == 0)
                {
                    ListPool<QueuedDispatchBatch>.Return(batches);
                    ListPool<SpikeMessage>.Return(mergedSpikes);
                    continue;
                }

                flushedBatches += batches.Count;
                targets.Add(new DispatchFlushTarget(targetEntry.Key, batches, mergedSpikes));
            }

            targets.Sort(static (a, b) => b.MergedSpikes.Count.CompareTo(a.MergedSpikes.Count));
            return targets;
        }
        catch
        {
            ReturnTargets(targets);
            throw;
        }
    }

    public static void ReturnTargets(IReadOnlyList<DispatchFlushTarget> targets)
    {
        foreach (var target in targets)
        {
            if (target.SourceBatches is List<QueuedDispatchBatch> sourceBatches)
            {
                ListPool<QueuedDispatchBatch>.Return(sourceBatches);
            }

            if (target.MergedSpikes is List<SpikeMessage> mergedSpikes)
            {
                ListPool<SpikeMessage>.Return(mergedSpikes);
            }
        }
    }

    public static int ComputeBatchChunkSize(
        int configuredChunkSize,
        int effectivePerServiceBudget,
        int effectivePerTickBudget)
    {
        var baseline = Math.Max(32, effectivePerServiceBudget * 2);
        var cappedByTickBudget = Math.Max(32, effectivePerTickBudget / 4);
        return Math.Clamp(Math.Min(configuredChunkSize, Math.Min(2048, Math.Max(baseline, cappedByTickBudget))), 32, 4096);
    }

    public static double ComputePressure(TransportRuntimeStats transport)
    {
        var queuePressureSignal = transport.DispatchQueueDroppedSpikes
                                  + (transport.DispatchQueueDispatchErrors * 24.0)
                                  + (transport.SpontaneousDispatchErrors * 6.0);
        var queuePressureDenominator = Math.Max(32.0, transport.DispatchQueueQueuedSpikes + transport.DispatchedSpikes + 1.0);
        return Math.Clamp(queuePressureSignal / queuePressureDenominator, 0.0, 1.0);
    }

    public static (int MaxQueueBatches, int MaxQueueSpikes) ComputeLimits(
        int baseMaxBatches,
        int baseMaxSpikes,
        double adaptivePressure,
        double queuePressure,
        int previousDroppedBatches,
        int previousDroppedSpikes,
        int activityCount,
        double maxGrowthScale)
    {
        var pressureScale = 1.0 + Math.Clamp((adaptivePressure * 0.55) + (queuePressure * 0.95), 0.0, 1.75);
        var dropScale = previousDroppedBatches > 0 || previousDroppedSpikes > 0 ? 1.35 : 1.0;
        var activityScale = activityCount <= 0
            ? 1.0
            : Math.Clamp(1.0 + (activityCount / 96.0), 1.0, 1.40);
        var combinedScale = Math.Clamp(pressureScale * dropScale * activityScale, 1.0, Math.Max(1.0, maxGrowthScale));

        var scaledMaxBatches = Math.Max(baseMaxBatches, (int)Math.Round(baseMaxBatches * combinedScale));
        var scaledMaxSpikes = Math.Max(baseMaxSpikes, (int)Math.Round(baseMaxSpikes * combinedScale));
        return (scaledMaxBatches, scaledMaxSpikes);
    }

    private static void UpdatePeak(ref int peakField, int candidate)
    {
        while (true)
        {
            var current = Volatile.Read(ref peakField);
            if (candidate <= current)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref peakField, candidate, current) == current)
            {
                return;
            }
        }
    }
}
