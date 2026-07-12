using System.Collections.Concurrent;
using NeuralResonanceEngine.Protocol;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class DispatchQueueRuntimeTests
{
    [Fact]
    public void DrainTargets_MergesEachTargetAndSchedulesLargestBurstFirst()
    {
        var queues = new ConcurrentDictionary<string, ConcurrentQueue<QueuedDispatchBatch>>(StringComparer.OrdinalIgnoreCase);
        var metrics = new DispatchQueueMetrics();

        Assert.True(DispatchQueueRuntime.TryEnqueue(queues, "PFC_L", Batch("a", 1), metrics, 8, 16));
        Assert.True(DispatchQueueRuntime.TryEnqueue(queues, "PFC_L", Batch("b", 2), metrics, 8, 16));
        Assert.True(DispatchQueueRuntime.TryEnqueue(queues, "M1_R", Batch("c", 4), metrics, 8, 16));

        var targets = DispatchQueueRuntime.DrainTargets(queues, out var flushedBatches);
        try
        {
            Assert.Equal(3, flushedBatches);
            Assert.Equal(2, targets.Count);
            Assert.Equal("M1_R", targets[0].TargetInstanceKey);
            Assert.Equal(4, targets[0].MergedSpikes.Count);
            Assert.Equal("PFC_L", targets[1].TargetInstanceKey);
            Assert.Equal(3, targets[1].MergedSpikes.Count);
            Assert.Equal(new[] { "a-0", "b-0", "b-1" }, targets[1].MergedSpikes.Select(s => s.SourceNeuronId));
        }
        finally
        {
            DispatchQueueRuntime.ReturnTargets(targets);
        }
    }

    [Fact]
    public void TryEnqueue_RejectsOverloadWithoutGrowingCounters()
    {
        var queues = new ConcurrentDictionary<string, ConcurrentQueue<QueuedDispatchBatch>>(StringComparer.OrdinalIgnoreCase);
        var metrics = new DispatchQueueMetrics();

        Assert.True(DispatchQueueRuntime.TryEnqueue(queues, "PFC_L", Batch("a", 2), metrics, 1, 2));
        Assert.False(DispatchQueueRuntime.TryEnqueue(queues, "M1_R", Batch("b", 1), metrics, 1, 2));

        Assert.Equal(1, metrics.QueuedBatches);
        Assert.Equal(2, metrics.QueuedSpikes);
        Assert.Equal(1, metrics.DroppedBatches);
        Assert.Equal(1, metrics.DroppedSpikes);
    }

    [Fact]
    public void AdaptivePolicy_ExpandsWithinConfiguredBoundAndReportsPressure()
    {
        var quiet = TransportRuntimeStats.Empty;
        var pressure = DispatchQueueRuntime.ComputePressure(quiet);
        var limits = DispatchQueueRuntime.ComputeLimits(10, 100, 1, 1, 1, 1, 96, 2);

        Assert.Equal(0, pressure);
        Assert.InRange(limits.MaxQueueBatches, 10, 20);
        Assert.InRange(limits.MaxQueueSpikes, 100, 200);
        Assert.Equal(64, DispatchQueueRuntime.ComputeBatchChunkSize(512, 32, 128));
    }

    [Fact]
    public void TransportCapabilities_RetryGrpcAfterCooldownAndDropRemovedTargets()
    {
        var capabilities = new TransportCapabilityCache();

        Assert.Equal(6, capabilities.RecordGrpcFailure("PFC_L", immediateDisable: true, nowMs: 1_000));
        Assert.False(capabilities.ShouldAttemptGrpc("PFC_L", nowMs: 15_999));
        Assert.True(capabilities.ShouldAttemptGrpc("PFC_L", nowMs: 16_000));

        capabilities.RecordGrpcFailure("old-target", immediateDisable: true, nowMs: 1_000);
        capabilities.PruneTo(["PFC_L"]);

        Assert.True(capabilities.ShouldAttemptGrpc("old-target", nowMs: 1_001));
    }

    private static QueuedDispatchBatch Batch(string source, int count) => new(
        "source-instance",
        "L",
        "R",
        Enumerable.Range(0, count).Select(index => new SpikeMessage { SourceNeuronId = $"{source}-{index}" }).ToArray());
}
