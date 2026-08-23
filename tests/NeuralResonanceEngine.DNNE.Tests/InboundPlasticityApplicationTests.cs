using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class InboundPlasticityApplicationTests
{
    private static readonly SemaphoreSlim EnvironmentGate = new(1, 1);

    [Theory]
    [InlineData(1f, 4f, 2f)]
    [InlineData(4f, 1f, 2f)]
    [InlineData(0.05f, 5f, 0.5f)]
    [InlineData(5f, 5f, 5f)]
    public void EffectiveStrengthCombinesPreAndPostsynapticContributions(
        float presynaptic,
        float postsynaptic,
        float expected)
    {
        Assert.Equal(
            expected,
            StructureEngine.CombineInboundSynapticStrength(presynaptic, postsynaptic),
            precision: 5);
    }

    [Fact]
    public void NonFiniteSynapticAndNeuromodulatoryValuesAreContained()
    {
        Assert.Equal(1f, StructureEngine.CombineInboundSynapticStrength(float.NaN, float.PositiveInfinity));

        var neuromod = NeuromodState.Clamp(new NeuromodState
        {
            DopamineLevel = float.NaN,
            SerotoninLevel = float.PositiveInfinity,
            AcetylcholineLevel = float.NegativeInfinity,
            NorepinephrineLevel = 0.4f
        });

        Assert.Equal(0f, neuromod.DopamineLevel);
        Assert.Equal(0f, neuromod.SerotoninLevel);
        Assert.Equal(0f, neuromod.AcetylcholineLevel);
        Assert.Equal(0.4f, neuromod.NorepinephrineLevel);
    }

    [Fact]
    public void SpikeIngressRejectsNonFiniteBiologicalValues()
    {
        SpikeMessage spike = CreateSpike(Guid.NewGuid(), 10);
        spike.VesicleQuanta = float.PositiveInfinity;

        Assert.False(SpikeProtocol.validate_spike(spike, out string error));
        Assert.Contains("finite", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RepeatedCoincidentInputChangesPersistedInboundStrength()
    {
        await EnvironmentGate.WaitAsync();
        string directory = Path.Combine(Path.GetTempPath(), "nre-plasticity-tests", Guid.NewGuid().ToString("N"));
        string? previousDirectory = Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
        string? previousInstance = Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
        var synapseId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        try
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", directory);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", $"plasticity-{Guid.NewGuid():N}");
            using var engine = new StructureEngine(new StructureProfile(
                StructureId.InferotemporalCortex,
                "Izhikevich",
                "STDP+SynapticTaggingCapture",
                "plasticity application test",
                new DelayWindow(2, 5)));

            float firstStrength = 0f;
            for (int epoch = 1; epoch <= 18; epoch++)
            {
                double timestamp = epoch * 20;
                await engine.EnqueueSpikeAsync(CreateSpike(synapseId, timestamp));
                await engine.ProcessTickAsync(CreateTick(epoch, timestamp + 12));
                if (epoch == 1)
                {
                    firstStrength = engine.GetInboundSynapseStrength(synapseId);
                }
            }

            float learnedStrength = engine.GetInboundSynapseStrength(synapseId);
            Assert.InRange(firstStrength, 0.05f, 5f);
            Assert.InRange(learnedStrength, 0.05f, 5f);
            Assert.True(
                MathF.Abs(learnedStrength - firstStrength) > 0.0001f,
                $"Expected repeated coincident input to change synaptic strength; first={firstStrength:F6}, learned={learnedStrength:F6}.");

            float effective = StructureEngine.CombineInboundSynapticStrength(1f, learnedStrength);
            Assert.NotEqual(1f, effective);
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

    [Fact]
    public void DenseBurstCannotExceedInitialBiologicalPlasticityBudget()
    {
        var synapse = new SynapseState(Guid.NewGuid(), NTEnum.GLUTAMATE, 1f, 1f);
        float total = 0f;

        for (var index = 0; index < 100_000; index++)
        {
            total += MathF.Abs(PlasticityRules.ApplyCadenceInvariantBudget(
                synapse,
                rawDelta: 0.05f,
                biologicalTimestampMs: 100));
        }

        Assert.Equal(PlasticityRules.InitialPlasticityBudgetQuanta, total, precision: 6);
        Assert.Equal(0f, synapse.PlasticityBudgetQuanta, precision: 6);
    }

    [Fact]
    public void PlasticityBudgetRefillsFromBiologicalTimeNotHostCallVolume()
    {
        var sparse = new SynapseState(Guid.NewGuid(), NTEnum.GLUTAMATE, 1f, 1f);
        var dense = new SynapseState(Guid.NewGuid(), NTEnum.GLUTAMATE, 1f, 1f);

        var sparseApplied = ApplyTimedTrain(sparse, callsPerTimestamp: 1);
        var denseApplied = ApplyTimedTrain(dense, callsPerTimestamp: 50);

        Assert.Equal(sparseApplied, denseApplied, precision: 6);
        Assert.InRange(
            denseApplied,
            PlasticityRules.InitialPlasticityBudgetQuanta,
            PlasticityRules.InitialPlasticityBudgetQuanta +
                PlasticityRules.PlasticityRefillQuantaPerBiologicalSecond + 0.0001f);
    }

    private static float ApplyTimedTrain(SynapseState synapse, int callsPerTimestamp)
    {
        float applied = 0f;
        for (var timestamp = 0; timestamp <= 1000; timestamp += 10)
        {
            for (var call = 0; call < callsPerTimestamp; call++)
            {
                applied += MathF.Abs(PlasticityRules.ApplyCadenceInvariantBudget(
                    synapse,
                    rawDelta: 0.05f,
                    biologicalTimestampMs: timestamp));
            }
        }

        return applied;
    }

    private static SpikeMessage CreateSpike(Guid synapseId, double timestampMs) => new()
    {
        MessageId = Guid.NewGuid(),
        TimestampMs = timestampMs,
        SourceStructure = StructureId.V4,
        TargetStructure = StructureId.InferotemporalCortex,
        SourceNeuronId = "visual-object-173",
        TargetNeuronId = "it-object-173",
        SynapseId = synapseId,
        Neurotransmitter = NTEnum.GLUTAMATE,
        VesicleQuanta = 1f,
        ReuptakeRate = 1f,
        SpikeType = SpikeTypeEnum.ACTION_POTENTIAL
    };

    private static TickSignal CreateTick(long tick, double timestampMs) => new(
        tick,
        timestampMs,
        10,
        new NeuromodState
        {
            DopamineLevel = 0.62f,
            AcetylcholineLevel = 0.58f,
            NorepinephrineLevel = 0.24f,
            SerotoninLevel = 0.30f
        },
        new Dictionary<BrainRhythm, double>(),
        0.42f);
}
