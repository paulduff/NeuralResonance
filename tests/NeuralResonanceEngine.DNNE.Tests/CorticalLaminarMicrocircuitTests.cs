using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class CorticalLaminarMicrocircuitTests
{
    [Fact]
    public void AtlasBackedCortexUsesThirtyFiveLaminarAreas()
    {
        var corticalStructures = Enum.GetValues<StructureId>()
            .Where(CorticalLaminarTopology.IsCorticalStructure)
            .ToArray();

        Assert.Equal(35, corticalStructures.Length);
        Assert.Contains(StructureId.V1, corticalStructures);
        Assert.Contains(StructureId.Pfc, corticalStructures);
        Assert.Contains(StructureId.EntorhinalCortex, corticalStructures);
        Assert.Contains(StructureId.M1, corticalStructures);
        Assert.DoesNotContain(StructureId.CorpusCallosum, corticalStructures);
    }

    [Fact]
    public void CorticalPopulationCyclePreservesEightyTwentyExcitatoryInhibitoryBalance()
    {
        var counts = Enumerable.Range(0, 384)
            .GroupBy(CorticalLaminarTopology.PopulationForNeuron)
            .ToDictionary(static group => group.Key, static group => group.Count());

        Assert.Equal(20, counts[CorticalPopulation.Layer1Modulatory]);
        Assert.Equal(117, counts[CorticalPopulation.Layer23Intratelencephalic]);
        Assert.Equal(57, counts[CorticalPopulation.Layer4Input]);
        Assert.Equal(57, counts[CorticalPopulation.Layer5PyramidalTract]);
        Assert.Equal(57, counts[CorticalPopulation.Layer6Corticothalamic]);
        Assert.Equal(38, counts[CorticalPopulation.PvInterneuron]);
        Assert.Equal(19, counts[CorticalPopulation.SstInterneuron]);
        Assert.Equal(19, counts[CorticalPopulation.VipInterneuron]);
        Assert.Equal(76, counts.Where(static item => CorticalLaminarTopology.IsInhibitory(item.Key)).Sum(static item => item.Value));
    }

    [Fact]
    public void CorticalSubtypesKeepStableIdsAndUsePopulationTransmitters()
    {
        var profile = StructureCircuitProfile.For(StructureId.Pfc);
        var pyramidal = new ModelNeuron(1, "LIF", profile);
        var pv = new ModelNeuron(16, "LIF", profile);
        var sst = new ModelNeuron(18, "LIF", profile);
        var vip = new ModelNeuron(19, "LIF", profile);

        Assert.Equal("n-001", pyramidal.Id);
        Assert.Equal(NTEnum.GLUTAMATE, pyramidal.PreferredNt);
        Assert.Equal(NTEnum.GABA, pv.PreferredNt);
        Assert.Equal(NTEnum.GABA, sst.PreferredNt);
        Assert.Equal(NTEnum.GABA, vip.PreferredNt);
    }

    [Fact]
    public void CorticalAfferentsRespectInputFeedbackAndModulatoryLayers()
    {
        var circuit = StructureCircuitProfile.For(StructureId.Pfc);
        var thalamic = ProjectAfferent(Spike(StructureId.MediodorsalThalamus, StructureId.Pfc, feedback: false), circuit.NeuronCount);
        var cortical = ProjectAfferent(Spike(StructureId.Ppc, StructureId.Pfc, feedback: false), circuit.NeuronCount);
        var feedback = ProjectAfferent(Spike(StructureId.Ppc, StructureId.Pfc, feedback: true), circuit.NeuronCount);
        var modulatory = ProjectAfferent(Spike(StructureId.NucleusBasalis, StructureId.Pfc, feedback: false), circuit.NeuronCount);

        Assert.Equal(CorticalPopulation.Layer4Input, CorticalLaminarTopology.PopulationForNeuron(thalamic));
        Assert.Equal(CorticalPopulation.Layer23Intratelencephalic, CorticalLaminarTopology.PopulationForNeuron(cortical));
        Assert.Contains(
            CorticalLaminarTopology.PopulationForNeuron(feedback),
            new[] { CorticalPopulation.Layer1Modulatory, CorticalPopulation.Layer6Corticothalamic });
        Assert.Equal(CorticalPopulation.Layer1Modulatory, CorticalLaminarTopology.PopulationForNeuron(modulatory));
    }

    private static int ProjectAfferent(SpikeMessage spike, int neuronCount)
    {
        const int sourceIndex = 41;
        var population = CorticalLaminarTopology.ResolveAfferentPopulation(spike, sourceIndex);
        return CorticalLaminarTopology.ProjectPreservingEnsemble(
            sourceIndex,
            neuronCount,
            population,
            PerceptEnsembleTopology.EnsembleForNeuron(sourceIndex),
            199);
    }

    [Fact]
    public async Task FiringCorticalPopulationCreatesPlasticLocalCollateralAndLiveDiagnostics()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nre-cortical-tests", Guid.NewGuid().ToString("N"));
        var previousDirectory = Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
        Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", directory);
        try
        {
            using var engine = new StructureEngine(new StructureProfile(
                StructureId.Pfc,
                "LIF",
                "STDP",
                "laminar test",
                new DelayWindow(2, 2),
                512));
            var drive = Enumerable.Range(0, 128)
                .Select(index => new SpikeMessage
                {
                    MessageId = Guid.NewGuid(),
                    SynapseId = Guid.NewGuid(),
                    SourceStructure = StructureId.Ppc,
                    TargetStructure = StructureId.Pfc,
                    SourceNeuronId = "cortical-drive-1",
                    TargetNeuronId = "target-1",
                    Neurotransmitter = NTEnum.GLUTAMATE,
                    VesicleQuanta = 5f,
                    ReuptakeRate = 8f,
                    SpikeType = SpikeTypeEnum.ACTION_POTENTIAL,
                    TimestampMs = index * 0.001,
                    IsFeedback = false
                })
                .ToArray();

            await engine.EnqueueSpikeBatchAsync(drive);
            var steps = new List<StructureStepResult>();
            for (var tick = 1; tick <= 12; tick++)
            {
                steps.Add(await engine.ProcessStepAsync(Tick(tick, 90 + (tick * 10)), 8));
            }

            Assert.Contains(steps, static step => step.Ack.SpikeCount > 0);
            Assert.Contains(steps, static step => step.Ack.FeedbackQueueDepth > 0);
            Assert.Contains(
                steps.SelectMany(static step => step.OutboundSpikes),
                static spike => spike.TargetStructure != spike.SourceStructure);
            var diagnostics = Assert.IsType<CorticalLaminarDiagnostics>(steps[^1].Ack.CorticalLaminarDiagnostics);
            Assert.Equal(CorticalLaminarTopology.PopulationCount, diagnostics.Populations.Count);
            Assert.Equal(384, diagnostics.Populations.Sum(static population => population.NeuronCount));
            Assert.Contains(diagnostics.Populations, static population => population.ActiveNeuronCount > 0);
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

    private static SpikeMessage Spike(StructureId source, StructureId target, bool feedback) => new()
    {
        MessageId = Guid.Parse("71111111-1111-1111-1111-111111111111"),
        SynapseId = Guid.Parse("72222222-2222-2222-2222-222222222222"),
        SourceStructure = source,
        TargetStructure = target,
        SourceNeuronId = "afferent-41",
        TargetNeuronId = string.Empty,
        Neurotransmitter = NTEnum.GLUTAMATE,
        VesicleQuanta = 1f,
        ReuptakeRate = 8f,
        SpikeType = SpikeTypeEnum.ACTION_POTENTIAL,
        IsFeedback = feedback
    };

    private static TickSignal Tick(long tick, double timestampMs) => new(
        tick,
        timestampMs,
        10,
        new NeuromodState(),
        new Dictionary<BrainRhythm, double>(),
        0f);
}
