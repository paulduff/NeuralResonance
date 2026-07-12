using NeuralResonanceEngine.Protocol;

internal sealed record QueuedDispatchBatch(
    string SourceInstanceKey,
    string SourceHemisphere,
    string TargetHemisphere,
    IReadOnlyList<SpikeMessage> Spikes,
    string? ReplayEngramKey = null);

internal sealed class DispatchQueueMetrics
{
    public int QueuedBatches;
    public int QueuedSpikes;
    public int PeakQueuedBatches;
    public int PeakQueuedSpikes;
    public int DroppedBatches;
    public int DroppedSpikes;
}

internal sealed record DispatchFlushTarget(
    string TargetInstanceKey,
    IReadOnlyList<QueuedDispatchBatch> SourceBatches,
    IReadOnlyList<SpikeMessage> MergedSpikes);

internal sealed record DispatchFlushResult(
    int FlushedBatches,
    int DeliveredSpikes,
    int DispatchErrors,
    string? LastError,
    int ActiveTargets,
    int MaxTargetBurstSpikes)
{
    public static DispatchFlushResult Empty { get; } = new(0, 0, 0, null, 0, 0);
}
