using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class NeuronalPerceptionTests
{
    private static readonly SemaphoreSlim EnvironmentGate = new(1, 1);

    [Fact]
    public void FeatureBandsKeepIdentityAcrossLocalJitterAndPathwayProjection()
    {
        const int centre = 46;
        var ensemble = PerceptEnsembleTopology.EnsembleForNeuron(centre);

        Assert.Equal(ensemble, PerceptEnsembleTopology.EnsembleForNeuron(centre - 1));
        Assert.Equal(ensemble, PerceptEnsembleTopology.EnsembleForNeuron(centre + 1));

        var v4 = PerceptEnsembleTopology.Project(centre, 384, 97);
        var temporal = PerceptEnsembleTopology.Project(v4, 320, 113);
        var perirhinal = PerceptEnsembleTopology.Project(temporal, 256, 5);
        var entorhinal = PerceptEnsembleTopology.Project(perirhinal, 320, 3);

        Assert.Equal(ensemble, PerceptEnsembleTopology.EnsembleForNeuron(v4));
        Assert.Equal(ensemble, PerceptEnsembleTopology.EnsembleForNeuron(temporal));
        Assert.Equal(ensemble, PerceptEnsembleTopology.EnsembleForNeuron(perirhinal));
        Assert.Equal(ensemble, PerceptEnsembleTopology.EnsembleForNeuron(entorhinal));
    }

    [Fact]
    public void BoundPerceptSurvivesModerateNoiseAndViewpointChange()
    {
        var reference = NeuronalPerceptionDecoder.Decode(CreateCircuit(3, 1.00f));
        var changedView = NeuronalPerceptionDecoder.Decode(CreateCircuit(3, 0.82f, competingNoise: 0.11f));

        Assert.True(reference.Active);
        Assert.True(changedView.Active);
        Assert.Equal(3, reference.DominantEnsemble);
        Assert.Equal(reference.DominantEnsemble, changedView.DominantEnsemble);
        Assert.True(changedView.Confidence >= 0.35);
    }

    [Fact]
    public void BindingPathwayAblationRemovesPerceptAuthority()
    {
        var intact = NeuronalPerceptionDecoder.Decode(CreateCircuit(5, 0.95f));
        var ablated = NeuronalPerceptionDecoder.Decode(CreateCircuit(5, 0.95f)
            .Where(static snapshot => snapshot.StructureId is not (
                StructureId.V4 or
                StructureId.InferotemporalCortex or
                StructureId.TemporalAssociation or
                StructureId.Pfc))
            .ToArray());

        Assert.True(intact.Active);
        Assert.False(ablated.Active);
        Assert.Equal(-1, ablated.DominantEnsemble);
    }

    [Fact]
    public void PerceptionRuntimeHasNoSemanticAnnotationSurface()
    {
        var runtime = new NeuronalPerceptionRuntime();
        runtime.Update(42, CreateCircuit(2, 0.92f));
        var snapshot = runtime.GetSnapshot();

        Assert.Equal(2, snapshot.Percept.DominantEnsemble);
        Assert.Equal(2, snapshot.Interpretation.DominantEnsemble);
        Assert.True(snapshot.Interpretation.ReadOnly);
        Assert.False(snapshot.Interpretation.CanCreatePercepts);
        Assert.False(snapshot.Interpretation.CanCreateMemories);
        Assert.Null(typeof(NeuronalPerceptionRuntime).GetMethod("TryAttachLanguageAnnotation"));
        Assert.Null(typeof(NeuronalPerceptionRuntime).Assembly.GetType("PerceptLanguageAnnotation"));
        var interpretationProperties = typeof(NeuronalPerceptInterpretation)
            .GetProperties()
            .Select(static property => property.Name)
            .ToArray();
        Assert.DoesNotContain("ObjectId", interpretationProperties);
        Assert.DoesNotContain("Label", interpretationProperties);
        Assert.DoesNotContain("LanguageAnnotationAttached", interpretationProperties);
    }

    [Fact]
    public async Task RecurrentBindingProducesNoveltyThenShortObjectPermanence()
    {
        await EnvironmentGate.WaitAsync();
        var directory = Path.Combine(Path.GetTempPath(), "nre-percept-tests", Guid.NewGuid().ToString("N"));
        var previousDirectory = Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
        var previousInstance = Environment.GetEnvironmentVariable("SERVICE_INSTANCE");

        try
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", directory);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", $"percept-{Guid.NewGuid():N}");
            using var engine = new StructureEngine(new StructureProfile(
                StructureId.V4,
                "LIF",
                "STDP",
                "recurrent percept binding test",
                new DelayWindow(1, 2)));

            var spikes = Enumerable.Range(0, 72)
                .Select(index => new SpikeMessage
                {
                    MessageId = Guid.NewGuid(),
                    TimestampMs = index * 0.01,
                    SourceStructure = StructureId.V2,
                    TargetStructure = StructureId.V4,
                    SourceNeuronId = $"feature-{44 + (index % 3) - 1}",
                    TargetNeuronId = $"feature-target-{44 + (index % 3) - 1}",
                    SynapseId = Guid.NewGuid(),
                    Neurotransmitter = NTEnum.GLUTAMATE,
                    VesicleQuanta = 2.4f,
                    ReuptakeRate = 8f,
                    SpikeType = index % 8 == 0 ? SpikeTypeEnum.BURST : SpikeTypeEnum.ACTION_POTENTIAL
                })
                .ToArray();
            await engine.EnqueueSpikeBatchAsync(spikes);

            var first = await engine.ProcessTickAsync(Tick(1, 10.0));
            var firstPercept = first.PerceptEnsembleDiagnostics!;
            var dominant = firstPercept.DominantEnsemble;
            var firstActivity = firstPercept.Ensembles.Single(item => item.EnsembleIndex == dominant);

            var second = await engine.ProcessTickAsync(Tick(2, 20.0));
            var secondPercept = second.PerceptEnsembleDiagnostics!;
            var secondActivity = secondPercept.Ensembles.Single(item => item.EnsembleIndex == dominant);

            Assert.Equal(PerceptEnsembleTopology.EnsembleForNeuron(44), dominant);
            Assert.True(firstActivity.Novelty > 0f);
            Assert.True(secondPercept.Persistence > 0f);
            Assert.True(secondActivity.RecurrentBinding > 0f);
            Assert.True(secondActivity.Novelty < firstActivity.Novelty);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", previousDirectory);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", previousInstance);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
            EnvironmentGate.Release();
        }
    }

    private static TickSignal Tick(long tick, double timestamp)
        => new(
            tick,
            timestamp,
            10.0,
            new NeuromodState
            {
                AcetylcholineLevel = 0.65f,
                NorepinephrineLevel = 0.50f,
                DopamineLevel = 0.30f,
                SerotoninLevel = 0.30f
            },
            new Dictionary<BrainRhythm, double>(),
            0f);

    private static IReadOnlyList<InstanceStructureSnapshot> CreateCircuit(
        int dominant,
        float gain,
        float competingNoise = 0.04f)
        =>
        [
            Snapshot(StructureId.V1, dominant, gain, competingNoise, visual: 0.78f),
            Snapshot(StructureId.Mt, dominant, gain, competingNoise, motion: 0.62f),
            Snapshot(StructureId.V4, dominant, gain, competingNoise, visual: 0.82f, binding: 0.76f),
            Snapshot(StructureId.InferotemporalCortex, dominant, gain, competingNoise, visual: 0.74f, binding: 0.84f),
            Snapshot(StructureId.Pulvinar, dominant, gain, competingNoise, salience: 0.72f),
            Snapshot(StructureId.PerirhinalCortex, dominant, gain, competingNoise, familiarity: 0.64f),
            Snapshot(StructureId.EntorhinalCortex, dominant, gain, competingNoise, hippocampal: 0.68f)
        ];

    private static InstanceStructureSnapshot Snapshot(
        StructureId structure,
        int dominant,
        float gain,
        float noise,
        float visual = 0f,
        float motion = 0f,
        float auditory = 0f,
        float somatic = 0f,
        float binding = 0f,
        float salience = 0f,
        float familiarity = 0f,
        float hippocampal = 0f)
    {
        var ensembles = Enumerable.Range(0, 8)
            .Select(index =>
            {
                var scale = index == dominant ? gain : noise;
                return new PerceptEnsembleActivity(
                    index,
                    visual * scale,
                    motion * scale,
                    auditory * scale,
                    somatic * scale,
                    binding * scale,
                    salience * scale,
                    familiarity * scale,
                    hippocampal * scale,
                    index == dominant ? 0.22f * gain : 0.01f,
                    index == dominant ? 0.78f * gain : 0.04f);
            })
            .ToArray();
        var diagnostics = new PerceptEnsembleDiagnostics(
            structure,
            ensembles,
            dominant,
            0.5f * gain,
            binding * gain);
        return new InstanceStructureSnapshot(
            new ServiceInstance(structure, $"{structure}-M", "M", new Uri("http://localhost")),
            structure,
            32,
            4f,
            BrainRhythm.GAMMA,
            [],
            new NeuromodState(),
            0,
            0,
            0,
            PerceptEnsembleDiagnostics: diagnostics);
    }
}
