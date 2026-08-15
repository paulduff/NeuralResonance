using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class NeuronalMemoryTests
{
    [Fact]
    public async Task OneEventBurstCreatesCueDependentCa3Recall()
    {
        await WithIsolatedSynapsesAsync(async () =>
        {
            using var engine = CreateEngine(StructureId.CA3, "MossyFiberLTP");
            var synapseId = Guid.NewGuid();

            var encodedRun = await StimulateAsync(engine, StructureId.CA3, synapseId, 44, 48, 0, 0.0, 0.75f);
            var encoded = encodedRun.Ack;
            var encodedMemory = Assert.IsType<SynapticMemoryDiagnostics>(encoded.SynapticMemoryDiagnostics);
            var learned = encodedMemory.Ensembles.OrderByDescending(static item => item.EngramStrength).First();

            var recalled = (await StimulateAsync(
                engine,
                StructureId.CA3,
                synapseId,
                44,
                8,
                encodedRun.Tick,
                encodedRun.Timestamp,
                0f)).Ack;
            var recalledMemory = Assert.IsType<SynapticMemoryDiagnostics>(recalled.SynapticMemoryDiagnostics);
            var recalledEnsemble = recalledMemory.Ensembles.Single(item => item.EnsembleIndex == learned.EnsembleIndex);
            var decision = NeuronalMemoryDecoder.Decode([Snapshot(StructureId.CA3, recalledMemory)]);

            Assert.True(learned.EngramStrength > 0.005f,
                $"engram={learned.EngramStrength:F6}, tag={learned.SynapticTag:F6}, eligibility={learned.EligibilityTrace:F6}, support={learned.SupportingSynapses}");
            Assert.True(recalledEnsemble.CueDrive > 0f);
            Assert.True(recalledEnsemble.RecallActivation > 0f);
            Assert.True(decision.RecallActive);
            Assert.Equal(learned.EnsembleIndex, decision.RecalledEnsemble);
            Assert.True(decision.HippocampalEncodingAvailable);
        });
    }

    [Fact]
    public void InterferenceReducesRecallAndHippocampalAblationRemovesEpisodeEncoding()
    {
        var clean = NeuronalMemoryDecoder.Decode([
            Snapshot(StructureId.CA3, Diagnostic(StructureId.CA3, 2, 0.78f, 0.72f, 0.64f, 0.02f))
        ]);
        var interfered = NeuronalMemoryDecoder.Decode([
            Snapshot(StructureId.CA3, Diagnostic(StructureId.CA3, 2, 0.78f, 0.72f, 0.64f, 0.72f, competitor: 0.42f))
        ]);
        var hippocampusAblated = NeuronalMemoryDecoder.Decode([
            Snapshot(StructureId.TemporalAssociation, Diagnostic(
                StructureId.TemporalAssociation,
                2,
                0.78f,
                0.72f,
                0.64f,
                0.02f))
        ]);

        Assert.True(clean.RecallActive);
        Assert.True(interfered.RecallStrength < clean.RecallStrength);
        Assert.True(interfered.RecallMargin < clean.RecallMargin);
        Assert.True(clean.HippocampalEncodingAvailable);
        Assert.False(hippocampusAblated.HippocampalEncodingAvailable);
    }

    [Fact]
    public async Task RapheAversiveTeachingExtinguishesAndVtaTeachingRelearns()
    {
        await WithIsolatedSynapsesAsync(async () =>
        {
            using var engine = CreateEngine(StructureId.CA1, "SynapticTaggingCapture");
            var synapseId = Guid.NewGuid();
            var tick = 0L;
            var timestamp = 0.0;
            TickAck latest = null!;
            for (var repetition = 0; repetition < 10; repetition++)
            {
                var result = await StimulateAsync(engine, StructureId.CA1, synapseId, 76, 8, tick, timestamp, 0.85f);
                tick = result.Tick;
                timestamp = result.Timestamp;
                latest = result.Ack;
            }
            var learnedStrength = engine.GetInboundSynapseStrength(synapseId);
            (tick, timestamp) = await AdvanceIdleAsync(engine, tick, timestamp, 240);
            for (var repetition = 0; repetition < 16; repetition++)
            {
                var result = await StimulateAsync(engine, StructureId.CA1, synapseId, 76, 8, tick, timestamp, -1f);
                tick = result.Tick;
                timestamp = result.Timestamp;
                latest = result.Ack;
            }
            var extinguishedStrength = engine.GetInboundSynapseStrength(synapseId);
            var extinctionActivity = latest.SynapticMemoryDiagnostics!.Ensembles
                .OrderByDescending(static item => item.Extinction)
                .First();
            (tick, timestamp) = await AdvanceIdleAsync(engine, tick, timestamp, 320);
            for (var repetition = 0; repetition < 24; repetition++)
            {
                var result = await StimulateAsync(engine, StructureId.CA1, synapseId, 76, 8, tick, timestamp, 1f);
                tick = result.Tick;
                timestamp = result.Timestamp;
                latest = result.Ack;
            }
            var relearnedStrength = engine.GetInboundSynapseStrength(synapseId);
            var relearnedActivity = latest.SynapticMemoryDiagnostics!.Ensembles
                .OrderByDescending(static item => item.SynapticTag)
                .First();
			Assert.True(float.IsFinite(learnedStrength));
			Assert.True(float.IsFinite(extinguishedStrength));
			Assert.True(float.IsFinite(relearnedStrength));
            Assert.True(extinguishedStrength < learnedStrength,
                $"learned={learnedStrength:F6}, extinguished={extinguishedStrength:F6}, relearned={relearnedStrength:F6}, extinction={extinctionActivity.Extinction:F6}");
            Assert.True(extinctionActivity.Extinction > 0f);
            Assert.True(relearnedStrength > extinguishedStrength,
                $"learned={learnedStrength:F6}, extinguished={extinguishedStrength:F6}, relearned={relearnedStrength:F6}, tag={relearnedActivity.SynapticTag:F6}, eligibility={relearnedActivity.EligibilityTrace:F6}");
        });
    }

    private static async Task<(long Tick, double Timestamp)> AdvanceIdleAsync(
        StructureEngine engine,
        long tick,
        double timestamp,
        int count)
    {
        for (var index = 0; index < count; index++)
        {
            tick++;
            timestamp += 10.0;
            await engine.ProcessTickAsync(new TickSignal(
                tick,
                timestamp,
                10.0,
                new NeuromodState(),
                new Dictionary<BrainRhythm, double>(),
                0f));
        }

        return (tick, timestamp);
    }

    [Fact]
    public async Task LearnedSynapticMemorySurvivesStructureRestart()
    {
        await WithIsolatedSynapsesAsync(async () =>
        {
            var instance = Environment.GetEnvironmentVariable("SERVICE_INSTANCE")!;
            var synapseId = Guid.NewGuid();
            float learnedStrength;
            using (var engine = CreateEngine(StructureId.CA3, "MossyFiberLTP"))
            {
                await StimulateAsync(engine, StructureId.CA3, synapseId, 108, 64, 0, 0.0, 0.8f);
                learnedStrength = engine.GetInboundSynapseStrength(synapseId);
            }

            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", instance);
            using var reloaded = CreateEngine(StructureId.CA3, "MossyFiberLTP");
            Assert.Equal(learnedStrength, reloaded.GetInboundSynapseStrength(synapseId), precision: 5);

            var recalled = (await StimulateAsync(reloaded, StructureId.CA3, synapseId, 108, 8, 0, 0.0, 0f)).Ack;
            Assert.True(recalled.SynapticMemoryDiagnostics!.LearnedSynapseCount > 0,
                $"strength={reloaded.GetInboundSynapseStrength(synapseId):F6}, learned={recalled.SynapticMemoryDiagnostics.LearnedSynapseCount}, recalled={recalled.SynapticMemoryDiagnostics.RecalledEnsemble}");
            Assert.True(recalled.SynapticMemoryDiagnostics.RecalledEnsemble >= 0);
        });
    }

    [Fact]
    public async Task RepeatedCorticalExperienceGraduallyRaisesConsolidation()
    {
        await WithIsolatedSynapsesAsync(async () =>
        {
            using var engine = CreateEngine(StructureId.TemporalAssociation, "STDP+SynapticTaggingCapture");
            var synapseId = Guid.NewGuid();
            var firstRun = await StimulateAsync(engine, StructureId.TemporalAssociation, synapseId, 140, 12, 0, 0.0, 0.55f);
            var first = firstRun.Ack;
            var ensemble = first.SynapticMemoryDiagnostics!.Ensembles
                .OrderByDescending(static item => item.EngramStrength)
                .First().EnsembleIndex;
            var initial = first.SynapticMemoryDiagnostics.Ensembles[ensemble].Consolidation;

            TickAck latest = first;
            var lastTick = firstRun.Tick;
            var lastTimestamp = firstRun.Timestamp;
            for (var repetition = 0; repetition < 8; repetition++)
            {
                var result = await StimulateAsync(
                    engine,
                    StructureId.TemporalAssociation,
                    synapseId,
                    140,
                    12,
                    lastTick,
                    lastTimestamp,
                    0.55f);
                lastTick = result.Tick;
                lastTimestamp = result.Timestamp;
                latest = result.Ack;
            }

            var consolidated = latest.SynapticMemoryDiagnostics!.Ensembles[ensemble].Consolidation;
            Assert.True(consolidated > initial,
                $"initial={initial:F6}, consolidated={consolidated:F6}, strength={latest.SynapticMemoryDiagnostics.Ensembles[ensemble].EngramStrength:F6}");
            Assert.True(latest.SynapticMemoryDiagnostics.CorticalConsolidation > 0f);
        });
    }

    private static StructureEngine CreateEngine(StructureId structure, string plasticityRule)
        => new(new StructureProfile(
            structure,
            "Izhikevich",
            plasticityRule,
            "neuronal memory causal test",
            new DelayWindow(0, 0)));

    private static async Task<StimulationResult> StimulateAsync(
        StructureEngine engine,
        StructureId targetStructure,
        Guid synapseId,
        int featureIndex,
        int spikeCount,
        long previousTick,
        double previousTimestampMs,
        float teachingSignal)
    {
        var modulationTimestamp = previousTimestampMs;
        var cueTimestamp = previousTimestampMs + 30.0;
        var spikes = Enumerable.Range(0, spikeCount)
            .Select(index => new SpikeMessage
            {
                MessageId = Guid.NewGuid(),
                TimestampMs = cueTimestamp,
                SourceStructure = StructureId.EntorhinalCortex,
                TargetStructure = targetStructure,
                SourceNeuronId = $"memory-feature-{featureIndex}",
                TargetNeuronId = $"memory-target-{featureIndex}",
                SynapseId = synapseId,
                Neurotransmitter = NTEnum.GLUTAMATE,
                VesicleQuanta = 1.15f,
                ReuptakeRate = 8f,
                SpikeType = index % 4 == 0 ? SpikeTypeEnum.BURST : SpikeTypeEnum.ACTION_POTENTIAL
            })
            .ToArray();
        var modulation = new List<SpikeMessage>();
        modulation.AddRange(CreateMappedNeuromodSpikes(
            targetStructure,
            featureIndex,
            StructureId.NucleusBasalis,
            NTEnum.ACETYLCHOLINE,
            4,
            1.2f,
            modulationTimestamp));
        modulation.AddRange(CreateMappedNeuromodSpikes(
            targetStructure,
            featureIndex,
            StructureId.LocusCoeruleus,
            NTEnum.NOREPINEPHRINE,
            4,
            1.1f,
            modulationTimestamp));
        if (teachingSignal > 0f)
        {
            modulation.AddRange(CreateMappedNeuromodSpikes(
                targetStructure,
                featureIndex,
                StructureId.Vta,
                NTEnum.DOPAMINE,
                6,
                1.4f * Math.Clamp(teachingSignal, 0.2f, 1f),
                modulationTimestamp + 1.0));
        }
        else if (teachingSignal < 0f)
        {
            modulation.AddRange(CreateMappedNeuromodSpikes(
                targetStructure,
                featureIndex,
                StructureId.RapheNuclei,
                NTEnum.SEROTONIN,
                10,
                1.6f * Math.Clamp(-teachingSignal, 0.2f, 1f),
                modulationTimestamp + 1.0));
        }

        await engine.EnqueueSpikeBatchAsync(modulation);
        var modulationTick = previousTick + 1;
        await engine.ProcessTickAsync(new TickSignal(
            modulationTick,
            modulationTimestamp + 25.0,
            10.0,
            new NeuromodState(),
            new Dictionary<BrainRhythm, double>(),
            0f));
        await engine.EnqueueSpikeBatchAsync(spikes);
        var cueTick = modulationTick + 1;
        var cueAck = await engine.ProcessTickAsync(new TickSignal(
            cueTick,
            cueTimestamp + 25.0,
            10.0,
            new NeuromodState(),
            new Dictionary<BrainRhythm, double>(),
            0f));
        return new StimulationResult(
            cueAck,
            cueTick,
            cueTimestamp + 25.0);
    }

    private sealed record StimulationResult(
        TickAck Ack,
        long Tick,
        double Timestamp);

    private static IReadOnlyList<SpikeMessage> CreateMappedNeuromodSpikes(
        StructureId targetStructure,
        int featureIndex,
        StructureId sourceStructure,
        NTEnum neurotransmitter,
        int spikeCount,
        float quanta,
        double timestampMs)
    {
        var circuit = StructureCircuitProfile.For(targetStructure);
        var kernel = CircuitKernelFactory.For(targetStructure);
        var featureProbe = new SpikeMessage
        {
            SourceStructure = StructureId.EntorhinalCortex,
            TargetStructure = targetStructure,
            SourceNeuronId = $"memory-feature-{featureIndex}",
            TargetNeuronId = $"memory-target-{featureIndex}",
            SynapseId = Guid.Empty,
            Neurotransmitter = NTEnum.GLUTAMATE
        };
        var targetNeuronIndex = kernel.ResolveInboundNeuronIndex(
            featureProbe,
            circuit.NeuronCount,
            circuit);
        var sourceNeuronIndex = Enumerable.Range(0, circuit.NeuronCount * 32)
            .First(candidate =>
            {
                var probe = new SpikeMessage
                {
                    SourceStructure = sourceStructure,
                    TargetStructure = targetStructure,
                    SourceNeuronId = $"n-{candidate:000}",
                    TargetNeuronId = $"neuromod-target-{featureIndex}",
                    SynapseId = Guid.Empty,
                    Neurotransmitter = neurotransmitter
                };
                return kernel.ResolveInboundNeuronIndex(probe, circuit.NeuronCount, circuit) == targetNeuronIndex;
            });

        return Enumerable.Range(0, spikeCount)
            .Select(index => new SpikeMessage
            {
                MessageId = Guid.NewGuid(),
                TimestampMs = timestampMs + (index * 0.01),
                SourceStructure = sourceStructure,
                TargetStructure = targetStructure,
                SourceNeuronId = $"n-{sourceNeuronIndex:000}",
                TargetNeuronId = $"neuromod-target-{featureIndex}",
                SynapseId = Guid.NewGuid(),
                Neurotransmitter = neurotransmitter,
                VesicleQuanta = quanta,
                ReuptakeRate = neurotransmitter switch
                {
                    NTEnum.DOPAMINE => 40f,
                    NTEnum.SEROTONIN => 50f,
                    NTEnum.ACETYLCHOLINE => 20f,
                    NTEnum.NOREPINEPHRINE => 30f,
                    _ => 8f
                },
                SpikeType = SpikeTypeEnum.GRADED
            })
            .ToArray();
    }

    private static SynapticMemoryDiagnostics Diagnostic(
        StructureId structure,
        int dominant,
        float cue,
        float strength,
        float recall,
        float interference,
        float competitor = 0f)
    {
        var ensembles = Enumerable.Range(0, 8)
            .Select(index => new SynapticMemoryEnsembleActivity(
                index,
                index == dominant ? cue : competitor,
                index == dominant ? strength : competitor,
                index == dominant ? recall : competitor,
                index == dominant ? 0.4f : 0f,
                index == dominant ? 0.4f : 0f,
                index == dominant ? interference : 0f,
                0f,
                index == dominant ? 0.25f : 0f,
                index == dominant ? 12 : competitor > 0f ? 4 : 0))
            .ToArray();
        return new SynapticMemoryDiagnostics(
            structure,
            structure == StructureId.CA3 ? "episodic" : "semantic",
            ensembles,
            dominant,
            Math.Max(0f, recall - competitor),
            structure == StructureId.CA3 ? strength : 0f,
            structure == StructureId.CA3 ? 0f : 0.25f,
            12);
    }

    private static InstanceStructureSnapshot Snapshot(
        StructureId structure,
        SynapticMemoryDiagnostics diagnostics)
        => new(
            new ServiceInstance(structure, $"{structure}-M", "M", new Uri("http://localhost")),
            structure,
            32,
            4f,
            BrainRhythm.THETA,
            [],
            new NeuromodState(),
            0,
            0,
            0,
            SynapticMemoryDiagnostics: diagnostics);

    private static async Task WithIsolatedSynapsesAsync(Func<Task> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), "nre-memory-tests", Guid.NewGuid().ToString("N"));
        var previousDirectory = Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
        var previousInstance = Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
        try
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", directory);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", $"memory-{Guid.NewGuid():N}");
            await test();
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
