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

            var encoded = await StimulateAsync(engine, StructureId.CA3, synapseId, 44, 48, 1, 20.0, 0.75f);
            var encodedMemory = Assert.IsType<SynapticMemoryDiagnostics>(encoded.SynapticMemoryDiagnostics);
            var learned = encodedMemory.Ensembles.OrderByDescending(static item => item.EngramStrength).First();

            var recalled = await StimulateAsync(engine, StructureId.CA3, synapseId, 44, 8, 2, 60.0, 0f);
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
    public async Task NegativePredictionErrorExtinguishesAndPositiveTeachingRelearns()
    {
        await WithIsolatedSynapsesAsync(async () =>
        {
            using var engine = CreateEngine(StructureId.CA1, "SynapticTaggingCapture");
            var synapseId = Guid.NewGuid();

            await StimulateAsync(engine, StructureId.CA1, synapseId, 76, 56, 1, 20.0, 0.85f);
            var learnedStrength = engine.GetInboundSynapseStrength(synapseId);
            var extinguished = await StimulateAsync(engine, StructureId.CA1, synapseId, 76, 72, 2, 60.0, -1f);
            var extinguishedStrength = engine.GetInboundSynapseStrength(synapseId);
            var extinctionActivity = extinguished.SynapticMemoryDiagnostics!.Ensembles
                .OrderByDescending(static item => item.Extinction)
                .First();
            await StimulateAsync(engine, StructureId.CA1, synapseId, 76, 112, 3, 100.0, 1f);
            var relearnedStrength = engine.GetInboundSynapseStrength(synapseId);

            Assert.True(extinguishedStrength < learnedStrength,
                $"learned={learnedStrength:F6}, extinguished={extinguishedStrength:F6}, relearned={relearnedStrength:F6}, extinction={extinctionActivity.Extinction:F6}");
            Assert.True(extinctionActivity.Extinction > 0f);
            Assert.True(relearnedStrength > extinguishedStrength,
                $"learned={learnedStrength:F6}, extinguished={extinguishedStrength:F6}, relearned={relearnedStrength:F6}");
        });
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
                await StimulateAsync(engine, StructureId.CA3, synapseId, 108, 64, 1, 20.0, 0.8f);
                learnedStrength = engine.GetInboundSynapseStrength(synapseId);
            }

            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", instance);
            using var reloaded = CreateEngine(StructureId.CA3, "MossyFiberLTP");
            Assert.Equal(learnedStrength, reloaded.GetInboundSynapseStrength(synapseId), precision: 5);

            var recalled = await StimulateAsync(reloaded, StructureId.CA3, synapseId, 108, 8, 1, 1000.0, 0f);
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
            var first = await StimulateAsync(engine, StructureId.TemporalAssociation, synapseId, 140, 12, 1, 20.0, 0.55f);
            var ensemble = first.SynapticMemoryDiagnostics!.Ensembles
                .OrderByDescending(static item => item.EngramStrength)
                .First().EnsembleIndex;
            var initial = first.SynapticMemoryDiagnostics.Ensembles[ensemble].Consolidation;

            TickAck latest = first;
            for (var tick = 2; tick <= 9; tick++)
            {
                latest = await StimulateAsync(engine, StructureId.TemporalAssociation, synapseId, 140, 12, tick, tick * 40.0, 0.55f);
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

    private static async Task<TickAck> StimulateAsync(
        StructureEngine engine,
        StructureId targetStructure,
        Guid synapseId,
        int featureIndex,
        int spikeCount,
        long tick,
        double timestampMs,
        float rewardPredictionError)
    {
        var spikes = Enumerable.Range(0, spikeCount)
            .Select(index => new SpikeMessage
            {
                MessageId = Guid.NewGuid(),
                TimestampMs = timestampMs,
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
        await engine.EnqueueSpikeBatchAsync(spikes);
        return await engine.ProcessTickAsync(new TickSignal(
            tick,
            timestampMs + 25.0,
            10.0,
            new NeuromodState
            {
                AcetylcholineLevel = 0.82f,
                NorepinephrineLevel = 0.72f,
                DopamineLevel = rewardPredictionError > 0f ? 0.75f : 0.28f,
                SerotoninLevel = 0.30f
            },
            new Dictionary<BrainRhythm, double>(),
            rewardPredictionError));
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
