using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class NeuronalMotorControlTests
{
    [Fact]
    public void BilateralMotorPopulationsProduceForwardDrive()
    {
        var runtime = Decode(CreateMotorCircuit(leftRate: 20.0f, rightRate: 20.0f));

        Assert.True(runtime.Active);
        Assert.True(runtime.ForwardDrive > 0.10);
        Assert.InRange(Math.Abs(runtime.TurnDrive), 0.0, 0.001);
        Assert.Equal(1.0, runtime.MotorCircuitCoverage, 6);
    }

    [Fact]
    public void LateralizedMotorPopulationProducesDifferentialTurn()
    {
        var runtime = Decode(CreateMotorCircuit(leftRate: 4.0f, rightRate: 22.0f));

        Assert.True(runtime.Active);
        Assert.True(runtime.RightDrive > runtime.LeftDrive);
        Assert.True(runtime.TurnDrive > 0.05);
    }

    [Fact]
    public void OutputNucleusInhibitionCausallySuppressesMotorDrive()
    {
        var disinhibited = Decode(CreateMotorCircuit(20.0f, 20.0f, outputInhibition: 0.0f, thalamicDisinhibition: 1.0f));
        var inhibited = Decode(CreateMotorCircuit(20.0f, 20.0f, outputInhibition: 1.0f, thalamicDisinhibition: 0.0f));

        Assert.True(disinhibited.ForwardDrive > inhibited.ForwardDrive * 2.0);
        Assert.True(disinhibited.SelectionGate > inhibited.SelectionGate);
    }

    [Fact]
    public void MotorPopulationAblationDropsCoverageAndAuthority()
    {
        var complete = Decode(CreateMotorCircuit(20.0f, 20.0f));
        var ablated = Decode(CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.Instance.HemisphereNormalized != "R")
            .ToArray());

        Assert.True(complete.Active);
        Assert.False(ablated.Active);
        Assert.True(ablated.MotorCircuitCoverage < complete.MotorCircuitCoverage);
    }

    [Fact]
    public void TimeSlicedPopulationWindowRetainsFreshOppositeHemisphere()
    {
        var all = CreateMotorCircuit(20.0f, 20.0f);
        var leftTick = all.Where(snapshot => snapshot.Instance.HemisphereNormalized != "R").ToArray();
        var rightTick = all.Where(snapshot => snapshot.Instance.HemisphereNormalized != "L").ToArray();
        var window = new NeuronalMotorPopulationWindow();

        var first = window.UpdateAndGet(10, leftTick, maxAgeTicks: 8);
        var second = window.UpdateAndGet(11, rightTick, maxAgeTicks: 8);

        Assert.False(Decode(first).Active);
        Assert.True(Decode(second).Active);
        Assert.Equal(1.0, Decode(second).MotorCircuitCoverage, 6);
    }

    [Fact]
    public void SleepSilencesDecodedMotorOutput()
    {
        var runtime = Decode(CreateMotorCircuit(20.0f, 20.0f), sleeping: true);

        Assert.False(runtime.Active);
        Assert.Equal(0.0, runtime.LeftDrive);
        Assert.Equal(0.0, runtime.RightDrive);
    }

    [Fact]
    public void DecoderSignatureContainsNoSymbolicActionState()
    {
        var decode = typeof(NeuronalMotorPopulationDecoder)
            .GetMethods()
            .Single(method => method.Name == nameof(NeuronalMotorPopulationDecoder.Decode));

        Assert.DoesNotContain(
            decode.GetParameters(),
            parameter => parameter.ParameterType == typeof(IntentionalActionLoopRuntime));
    }

    [Fact]
    public void MotorControlSettingsExposeNoLegacyMode()
    {
        Assert.DoesNotContain(
            typeof(NeuronalMotorControlSettings).GetProperties(),
            property => property.Name == "Mode");
    }

    [Fact]
    public void RuntimeExposesNoMotorModeMutationMethod()
    {
        Assert.DoesNotContain(
            typeof(NeuronalMotorControlState).GetMethods(),
            method => method.Name == "TryApplyMode");
    }

    [Fact]
    public void NeuronalBridgeAddsPopulationEventsOncePerNeuronalTick()
    {
        var state = CreateAvatarState(active: true, tick: 12, confidence: 0.8, left: 0.5, right: 0.75);
        var original = new[] { new AvatarDispatchSpike("V1", "L", 100, "visual:edge") };

        var first = AvatarNeuronalMotorBridge.Compose(state, original, -1, out var cursor, out _);
        var second = AvatarNeuronalMotorBridge.Compose(state, original, cursor, out var secondCursor, out _);

        Assert.True(first.Count > original.Length);
        Assert.Contains(first, spike => spike.SourceNeuronId.StartsWith("population:", StringComparison.Ordinal));
        Assert.Single(second);
        Assert.Equal(cursor, secondCursor);
    }

    [Fact]
    public void NeuronalBridgeRemovesAllSemanticMotorAndToolSignals()
    {
        var state = CreateAvatarState(active: true, tick: 18, confidence: 0.9, left: 0.6, right: 0.6);
        var original = new[]
        {
            new AvatarDispatchSpike("M1", "L", 100, "L:motor_forward_18_0"),
            new AvatarDispatchSpike("PremotorCortex", "R", 101, "tool_build.forward"),
            new AvatarDispatchSpike("V1", "L", 102, "visual:edge")
        };

        var composed = AvatarNeuronalMotorBridge.Compose(state, original, -1, out _, out _);

        Assert.DoesNotContain(composed, spike => spike.SourceNeuronId.Contains("motor_forward", StringComparison.Ordinal));
        Assert.DoesNotContain(composed, spike => spike.SourceNeuronId.Contains("tool_build", StringComparison.Ordinal));
        Assert.Contains(composed, spike => spike.SourceNeuronId.Equals("visual:edge", StringComparison.Ordinal));
        Assert.Contains(composed, spike => spike.SourceNeuronId.StartsWith("population:", StringComparison.Ordinal));
    }

    [Fact]
    public void LowConfidenceNeuronalOutputDoesNotFallBackToSymbolicMovement()
    {
        var state = CreateAvatarState(active: false, tick: 20, confidence: 0.2, left: 1.0, right: 1.0);
        var original = new[] { new AvatarDispatchSpike("M1", "L", 100, "motor_forward") };

        var composed = AvatarNeuronalMotorBridge.Compose(state, original, -1, out _, out _);

        Assert.Empty(composed);
    }

    [Fact]
    public void PopulationCodeDrivesOnlyItsEncodedHemisphere()
    {
        var summary = AvatarMotorCatalog.SummarizeMotorDrive(new[]
        {
            new AvatarDispatchSpike("SpinalCordMotor", "L", 100, "population:l:excitatory:1:0"),
            new AvatarDispatchSpike("SpinalCordMotor", "R", 101, "population:r:inhibitory:1:0")
        });

        Assert.True(summary.LeftInput > 0.0);
        Assert.True(summary.RightInput < 0.0);
        Assert.Equal(2, summary.MotorEvents);
    }

    private static NeuronalMotorRuntime Decode(
        IReadOnlyList<InstanceStructureSnapshot> snapshots,
        bool sleeping = false)
        => NeuronalMotorPopulationDecoder.Decode(
            tick: 1,
            snapshots,
            sleeping,
            new NeuronalMotorControlSnapshot(CreateSettings()),
            NeuronalMotorRuntime.Default);

    private static NeuronalMotorControlSettings CreateSettings()
        => NeuronalMotorControlSettings.Normalize(new NeuronalMotorControlSettings(
            BaselineRateHz: 1.5,
            SaturationRateHz: 25.0,
            SmoothingAlpha: 1.0,
            PopulationSnapshotMaxAgeTicks: 96,
            MinimumCircuitCoverage: 0.45,
            MinimumOutputConfidence: 0.35,
            MaxPopulationEventsPerSide: 12));

    private static IReadOnlyList<InstanceStructureSnapshot> CreateMotorCircuit(
        float leftRate,
        float rightRate,
        float outputInhibition = 0.0f,
        float thalamicDisinhibition = 1.0f)
    {
        var snapshots = new List<InstanceStructureSnapshot>();
        var structures = new[]
        {
            StructureId.PremotorCortex,
            StructureId.Sma,
            StructureId.M1,
            StructureId.MotorThalamus,
            StructureId.ReticularFormation,
            StructureId.SpinalCordMotor
        };
        foreach (var structure in structures)
        {
            snapshots.Add(Snapshot(structure, "L", leftRate));
            snapshots.Add(Snapshot(structure, "R", rightRate));
        }

        snapshots.Add(Snapshot(
            StructureId.GPi,
            "L",
            8.0f,
            basalGanglia: new BasalGangliaDiagnostics(
                "selection",
                DirectPathwayActivation: thalamicDisinhibition,
                IndirectPathwayActivation: outputInhibition,
                HyperdirectPathwayActivation: outputInhibition,
                OutputNucleusInhibition: outputInhibition,
                ThalamicDisinhibition: thalamicDisinhibition,
                DopamineModulation: 0.5f,
                ActionSelectionBias: thalamicDisinhibition)));
        snapshots.Add(Snapshot(
            StructureId.DeepCerebellarNuclei,
            "M",
            10.0f,
            cerebellar: new CerebellarDiagnostics(
                "stable",
                MossyFiberDrive: 0.7f,
                ClimbingFiberError: 0.1f,
                PurkinjeInhibition: 0.3f,
                DeepNucleusOutput: 0.8f,
                VermisStabilization: 0.8f,
                CorrectionGain: 0.8f,
                PredictionError: 0.1f)));
        snapshots.Add(Snapshot(
            StructureId.VestibularNuclei,
            "M",
            10.0f,
            postural: new VestibuloReticularDiagnostics(
                "stable",
                VestibularDrive: 0.7f,
                ReticularArousal: 0.7f,
                VermisBalanceCorrection: 0.8f,
                SpinalMotorTone: 0.8f,
                PostureStability: 0.9f,
                BalanceError: 0.1f)));
        return snapshots;
    }

    private static InstanceStructureSnapshot Snapshot(
        StructureId structure,
        string hemisphere,
        float rate,
        BasalGangliaDiagnostics? basalGanglia = null,
        CerebellarDiagnostics? cerebellar = null,
        VestibuloReticularDiagnostics? postural = null)
    {
        var instance = new ServiceInstance(
            structure,
            $"{structure}-{hemisphere}",
            hemisphere,
            new Uri($"http://localhost:{5000 + (int)structure}"));
        return new InstanceStructureSnapshot(
            instance,
            structure,
            ActiveNeuronCount: rate > 0.0f ? 8 : 0,
            MeanFiringRateHz: rate,
            DominantRhythm: BrainRhythm.BETA,
            TopActiveNeurons: [],
            NeuromodLocal: new NeuromodState(),
            SpikeInCount: 0,
            SpikeOutCount: 0,
            FeedbackQueueDepth: 0,
            BasalGangliaDiagnostics: basalGanglia,
            CerebellarDiagnostics: cerebellar,
            VestibuloReticularDiagnostics: postural);
    }

    private static JsonElement CreateAvatarState(
        bool active,
        long tick,
        double confidence,
        double left,
        double right)
        => JsonSerializer.SerializeToElement(new
        {
            neuronalMotor = new
            {
                active,
                sleeping = false,
                tick,
                sequence = tick,
                leftDrive = left,
                rightDrive = right,
                confidence,
                minimumOutputConfidence = 0.45,
                maxPopulationEventsPerSide = 8
            }
        });
}
