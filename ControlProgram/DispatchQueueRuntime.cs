using System.Collections.Concurrent;
using System.Threading;

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
