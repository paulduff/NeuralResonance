using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

[Collection(NeuralResonanceEngine.DNNE.Tests.EnvironmentVariableTestCollection.Name)]
public sealed class StructureEngineDeterminismTests
{
    private static readonly object EnvironmentGate = new();

    [Fact]
    public void DueFeedbackIsProcessedEvenWhenAnEarlierArrivalHasALaterDeliveryTime()
    {
        WithEngine(16, engine =>
        {
            engine.EnqueueSpikeAsync(CreateSpike(timestampMs: 10_000, feedback: true)).GetAwaiter().GetResult();
            engine.EnqueueSpikeAsync(CreateSpike(timestampMs: 0, feedback: true)).GetAwaiter().GetResult();

            var ack = engine.ProcessTickAsync(CreateTick(tick: 1, timestampMs: 100)).GetAwaiter().GetResult();

            Assert.Equal(1, ack.FeedbackQueueDepth);
        });
    }

    [Fact]
    public void InboundQueueRejectsNewSpikesAtCapacity()
    {
        WithEngine(1, engine =>
        {
            engine.EnqueueSpikeAsync(CreateSpike(timestampMs: 0, feedback: false)).GetAwaiter().GetResult();

            Assert.Throws<StructureIngressOverloadException>(() =>
                engine.EnqueueSpikeAsync(CreateSpike(timestampMs: 1, feedback: false)).GetAwaiter().GetResult());
        });
    }

    [Fact]
    public void BatchAdmissionIsAtomicWhenCapacityIsInsufficient()
    {
        WithEngine(2, engine =>
        {
            engine.EnqueueSpikeAsync(CreateSpike(timestampMs: 0, feedback: false)).GetAwaiter().GetResult();
            var rejected = new[]
            {
                CreateSpike(timestampMs: 1, feedback: false),
                CreateSpike(timestampMs: 2, feedback: false)
            };

            Assert.Throws<StructureIngressOverloadException>(() =>
                engine.EnqueueSpikeBatchAsync(rejected).GetAwaiter().GetResult());

            // If the rejected batch had committed a prefix, this would overflow.
            engine.EnqueueSpikeAsync(rejected[0]).GetAwaiter().GetResult();
        });
    }

    [Fact]
    public void RetriedMessageIdIsAcknowledgedWithoutBeingQueuedTwice()
    {
        WithEngine(1, engine =>
        {
            var spike = CreateSpike(timestampMs: 0, feedback: false);
            engine.EnqueueSpikeAsync(spike).GetAwaiter().GetResult();

            Assert.Equal(1, engine.EnqueueSpikeBatchAsync(new[] { spike }).GetAwaiter().GetResult());
            var ack = engine.ProcessTickAsync(CreateTick(tick: 1, timestampMs: 100)).GetAwaiter().GetResult();
            Assert.Equal(1, ack.SpikeInCount);
        });
    }

    [Fact]
    public void HemisphereInstancesUseDifferentSynapseFiles()
    {
        lock (EnvironmentGate)
        {
            var directory = Path.Combine(Path.GetTempPath(), "nre-engine-tests", Guid.NewGuid().ToString("N"));
            var previousDirectory = Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
            var previousInstance = Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
            try
            {
                Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", directory);
                foreach (var instance in new[] { "PFC_L", "PFC_R" })
                {
                    Environment.SetEnvironmentVariable("SERVICE_INSTANCE", instance);
                    using var engine = new StructureEngine(new StructureProfile(
                        StructureId.Pfc,
                        "LIF",
                        "STDP",
                        "test profile",
                        new DelayWindow(2, 2)));
                }

                Assert.True(File.Exists(Path.Combine(directory, "PFC_L.synapses.json")));
                Assert.True(File.Exists(Path.Combine(directory, "PFC_R.synapses.json")));
            }
            finally
            {
                Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", previousDirectory);
                Environment.SetEnvironmentVariable("SERVICE_INSTANCE", previousInstance);
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }
    }

    [Fact]
    public void DuplicateAndStaleTicksAreRejectedBeforeTheyCanMutateState()
    {
        WithEngine(16, engine =>
        {
            engine.ProcessTickAsync(CreateTick(tick: 4, timestampMs: 40)).GetAwaiter().GetResult();

            Assert.Throws<StructureTickSequenceException>(() =>
                engine.ProcessTickAsync(CreateTick(tick: 4, timestampMs: 40)).GetAwaiter().GetResult());
            Assert.Throws<StructureTickSequenceException>(() =>
                engine.ProcessTickAsync(CreateTick(tick: 3, timestampMs: 30)).GetAwaiter().GetResult());
        });
    }

    private static void WithEngine(int maxInboundQueueDepth, Action<StructureEngine> action)
    {
        lock (EnvironmentGate)
        {
            var directory = Path.Combine(Path.GetTempPath(), "nre-engine-tests", Guid.NewGuid().ToString("N"));
            var previousDirectory = Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", directory);
            try
            {
                using var engine = new StructureEngine(new StructureProfile(
                    StructureId.Pfc,
                    "LIF",
                    "STDP",
                    "test profile",
                    new DelayWindow(2, 2),
                    maxInboundQueueDepth));
                action(engine);
            }
            finally
            {
                Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", previousDirectory);
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }
    }

    private static SpikeMessage CreateSpike(double timestampMs, bool feedback) => new()
    {
        MessageId = Guid.NewGuid(),
        SynapseId = Guid.NewGuid(),
        TimestampMs = timestampMs,
        SourceStructure = StructureId.Pfc,
        TargetStructure = StructureId.Pfc,
        SourceNeuronId = "test-source-1",
        TargetNeuronId = "test-target-1",
        Neurotransmitter = NTEnum.GLUTAMATE,
        VesicleQuanta = 1f,
        ReuptakeRate = 1f,
        SpikeType = SpikeTypeEnum.ACTION_POTENTIAL,
        IsFeedback = feedback
    };

    private static TickSignal CreateTick(long tick, double timestampMs) => new(
        tick,
        timestampMs,
        10,
        new NeuromodState(),
        new Dictionary<BrainRhythm, double>(),
        0);
}
