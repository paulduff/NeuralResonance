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

    [Fact]
    public void Synaptic_Homeostasis_Retains_The_Most_Reinforced_Inbound_Connections()
    {
        var weakOld = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var weakRecent = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var tagged = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var repeated = Guid.Parse("00000000-0000-0000-0000-000000000004");
        var synapses = new Dictionary<Guid, SynapseState>
        {
            [weakOld] = CreateSynapse(weakOld, updateCount: 1, timestampMs: 10),
            [weakRecent] = CreateSynapse(weakRecent, updateCount: 1, timestampMs: 20),
            [tagged] = CreateSynapse(tagged, updateCount: 1, timestampMs: 5, tag: 0.8f),
            [repeated] = CreateSynapse(repeated, updateCount: 9, timestampMs: 1)
        };

        var removed = SynapsePersistenceStore.PruneInboundSynapses(synapses, maximumCount: 2);

        Assert.Equal(2, removed);
        Assert.Equal(2, synapses.Count);
        Assert.Contains(tagged, synapses.Keys);
        Assert.Contains(repeated, synapses.Keys);
        Assert.DoesNotContain(weakOld, synapses.Keys);
        Assert.DoesNotContain(weakRecent, synapses.Keys);
    }

    [Fact]
    public void Sensory_Synapse_Limit_Is_Bounded_And_Configurable()
    {
        lock (EnvironmentGate)
        {
            var previous = Environment.GetEnvironmentVariable("NRE_SENSORY_SYNAPSE_MAX_INBOUND");
            try
            {
                Environment.SetEnvironmentVariable("NRE_SENSORY_SYNAPSE_MAX_INBOUND", "4096");
                using var store = new SynapsePersistenceStore(StructureId.V1);

                Assert.Equal(4096, store.MaxInboundSynapseCount);
            }
            finally
            {
                Environment.SetEnvironmentVariable("NRE_SENSORY_SYNAPSE_MAX_INBOUND", previous);
            }
        }
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

    private static SynapseState CreateSynapse(
        Guid id,
        int updateCount,
        double timestampMs,
        float tag = 0f)
        => new(id, NTEnum.GLUTAMATE, 1f, 1f)
        {
            UpdateCount = updateCount,
            LastUpdateTimestampMs = timestampMs,
            SynapticTagTrace = tag
        };
}
