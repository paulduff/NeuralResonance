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
    public void SelectedActionLaneIsNotVetoedByGlobalOutputNucleusAverage()
    {
        var circuit = CreateMotorCircuit(
                20.0f,
                20.0f,
                outputInhibition: 1.0f,
                thalamicDisinhibition: 0.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateActionCircuit(
                selectedChannel: 0,
                selectedOutputInhibition: 0.04f,
                selectedThalamicRelay: 0.85f))
            .ToArray();

        var runtime = Decode(circuit);

        Assert.True(runtime.Active);
        Assert.Equal(0.04, runtime.OutputInhibition, 6);
        Assert.Equal(0.96, runtime.SelectionGate, 6);
        Assert.True(runtime.ForwardDrive > 0.10);
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
    public void DecoderHasNoHostSleepVeto()
    {
        var decode = typeof(NeuronalMotorPopulationDecoder)
            .GetMethods()
            .Single(method => method.Name == nameof(NeuronalMotorPopulationDecoder.Decode));
        var runtime = Decode(CreateMotorCircuit(20.0f, 20.0f));

        Assert.DoesNotContain(decode.GetParameters(), parameter => parameter.ParameterType == typeof(bool));
        Assert.True(runtime.Active);
    }

    [Fact]
    public void DecoderSignatureContainsNoSymbolicActionState()
    {
        var decode = typeof(NeuronalMotorPopulationDecoder)
            .GetMethods()
            .Single(method => method.Name == nameof(NeuronalMotorPopulationDecoder.Decode));

        Assert.DoesNotContain(
            decode.GetParameters(),
            parameter => parameter.ParameterType.Name == "IntentionalActionLoopRuntime");
        Assert.Null(typeof(SimulationState).Assembly.GetType("IntentionalActionLoopRuntime"));
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
    public void FifthActionLaneProducesLeftShoulderFlexionWithoutLocomotion()
    {
        var circuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateActionCircuit(NeuronalActionSelectionDecoder.LeftShoulderFlexionChannel))
            .ToArray();

        var runtime = Decode(circuit);

        Assert.True(runtime.Active);
        Assert.Equal(NeuronalActionSelectionDecoder.LeftShoulderFlexionChannel, runtime.SelectedActionChannel);
        Assert.True(runtime.ManipulatorDrive > 0.10);
        Assert.True(runtime.LeftShoulderSagittalDrive > 0.10);
        Assert.Equal(0.0, runtime.RightShoulderSagittalDrive, 6);
        Assert.Equal(0.0, runtime.ForwardDrive, 6);
        Assert.Equal(0.0, runtime.TurnDrive, 6);
    }

    [Theory]
    [InlineData(NeuronalActionSelectionDecoder.LeftShoulderFlexionChannel, 0, 1)]
    [InlineData(NeuronalActionSelectionDecoder.LeftShoulderExtensionChannel, 0, -1)]
    [InlineData(NeuronalActionSelectionDecoder.RightShoulderFlexionChannel, 1, 1)]
    [InlineData(NeuronalActionSelectionDecoder.RightShoulderExtensionChannel, 1, -1)]
    [InlineData(NeuronalActionSelectionDecoder.LeftShoulderAbductionChannel, 2, 1)]
    [InlineData(NeuronalActionSelectionDecoder.LeftShoulderAdductionChannel, 2, -1)]
    [InlineData(NeuronalActionSelectionDecoder.RightShoulderAbductionChannel, 3, 1)]
    [InlineData(NeuronalActionSelectionDecoder.RightShoulderAdductionChannel, 3, -1)]
    [InlineData(NeuronalActionSelectionDecoder.LeftElbowFlexionChannel, 4, 1)]
    [InlineData(NeuronalActionSelectionDecoder.LeftElbowExtensionChannel, 4, -1)]
    [InlineData(NeuronalActionSelectionDecoder.RightElbowFlexionChannel, 5, 1)]
    [InlineData(NeuronalActionSelectionDecoder.RightElbowExtensionChannel, 5, -1)]
    public void OpposingArmLanesRecruitOnlyTheirAnatomicalEffector(
        int selectedChannel,
        int expectedDriveIndex,
        int expectedSign)
    {
        var circuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateActionCircuit(selectedChannel))
            .ToArray();

        var runtime = Decode(circuit);
        var drives = new[]
        {
            runtime.LeftShoulderSagittalDrive,
            runtime.RightShoulderSagittalDrive,
            runtime.LeftShoulderCoronalDrive,
            runtime.RightShoulderCoronalDrive,
            runtime.LeftElbowDrive,
            runtime.RightElbowDrive
        };

        Assert.True(runtime.Active);
        Assert.Equal(selectedChannel, runtime.SelectedActionChannel);
        Assert.Equal(expectedSign, Math.Sign(drives[expectedDriveIndex]));
        Assert.True(Math.Abs(drives[expectedDriveIndex]) > 0.10);
        Assert.Single(drives, drive => Math.Abs(drive) > 0.001);
        Assert.Equal(0.0, runtime.ForwardDrive, 6);
        Assert.Equal(0.0, runtime.TurnDrive, 6);
    }

    [Theory]
    [InlineData(NeuronalActionSelectionDecoder.StandChannel)]
    [InlineData(NeuronalActionSelectionDecoder.CrouchChannel)]
    [InlineData(NeuronalActionSelectionDecoder.SitChannel)]
    [InlineData(NeuronalActionSelectionDecoder.LieChannel)]
    public void PostureActionLanesRecruitOnlyTheirPosturalDrive(int selectedChannel)
    {
        var circuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateActionCircuit(selectedChannel))
            .ToArray();

        var runtime = Decode(circuit);
        var postureDrives = new Dictionary<int, double>
        {
            [NeuronalActionSelectionDecoder.StandChannel] = runtime.StandDrive,
            [NeuronalActionSelectionDecoder.CrouchChannel] = runtime.CrouchDrive,
            [NeuronalActionSelectionDecoder.SitChannel] = runtime.SitDrive,
            [NeuronalActionSelectionDecoder.LieChannel] = runtime.LieDrive
        };

        Assert.True(runtime.Active);
        Assert.Equal(selectedChannel, runtime.SelectedActionChannel);
        Assert.True(postureDrives[selectedChannel] > 0.10);
        Assert.All(postureDrives.Where(pair => pair.Key != selectedChannel), pair =>
            Assert.Equal(0.0, pair.Value, 6));
        Assert.Equal(0.0, runtime.ForwardDrive, 6);
        Assert.Equal(0.0, runtime.TurnDrive, 6);
        Assert.Equal(0.0, runtime.ManipulatorDrive, 6);
    }

    [Fact]
    public void BilateralSpinalRightingPopulationCanRecruitStandWithoutActionSelection()
    {
        var circuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreatePrimaryRightingReflex(leftDrive: 0.85f, rightDrive: 0.80f))
            .Concat(CreateSpinalRightingReflex(leftDrive: 0.80f, rightDrive: 0.75f))
            .ToArray();

        var runtime = Decode(circuit);

        Assert.True(runtime.Active);
        Assert.Equal(-1, runtime.SelectedActionChannel);
        Assert.True(runtime.StandDrive > 0.10);
        Assert.Equal(0.0, runtime.CrouchDrive, 6);
        Assert.Equal(0.0, runtime.SitDrive, 6);
        Assert.Equal(0.0, runtime.LieDrive, 6);
        Assert.Contains("righting=bilateral", runtime.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void UnilateralSpinalRightingPopulationHasNoMotorAuthority()
    {
        var circuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreatePrimaryRightingReflex(leftDrive: 0.85f, rightDrive: 0.80f))
            .Concat(CreateSpinalRightingReflex(leftDrive: 0.80f, rightDrive: null))
            .ToArray();

        var runtime = Decode(circuit);

        Assert.False(runtime.Active);
        Assert.Equal(0.0, runtime.StandDrive, 6);
        Assert.Contains("righting=incomplete", runtime.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void ReverberatingSpinalRightingPopulationReleasesWithoutCurrentAfferentDrive()
    {
        var circuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateSpinalRightingReflex(leftDrive: 0.90f, rightDrive: 0.90f))
            .ToArray();

        var runtime = Decode(circuit);

        Assert.False(runtime.Active);
        Assert.Equal(0.0, runtime.StandDrive, 6);
        Assert.Contains("righting=incomplete", runtime.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectedFloorPostureNeurallyInhibitsRightingReflex()
    {
        var circuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateActionCircuit(NeuronalActionSelectionDecoder.LieChannel))
            .Concat(CreatePrimaryRightingReflex(leftDrive: 0.95f, rightDrive: 0.95f))
            .Concat(CreateSpinalRightingReflex(leftDrive: 0.90f, rightDrive: 0.90f))
            .ToArray();

        var runtime = Decode(circuit);

        Assert.True(runtime.Active);
        Assert.Equal(NeuronalActionSelectionDecoder.LieChannel, runtime.SelectedActionChannel);
        Assert.Equal(0.0, runtime.StandDrive, 6);
        Assert.True(runtime.LieDrive > 0.10);
    }

    [Fact]
    public void NeuronalBridgeAddsManipulatorEffectorPopulation()
    {
        var state = CreateAvatarState(
            active: true,
            tick: 13,
            confidence: 0.8,
            left: 0.0,
            right: 0.0,
            manipulator: 0.75);

        var composed = AvatarNeuronalMotorBridge.Compose(state, [], -1, out _, out var decoded);

        Assert.Equal(0.75, decoded.ManipulatorDrive, 6);
        Assert.Contains(
            composed,
            spike => spike.SourceNeuronId.StartsWith("effector:manipulator:excitatory:", StringComparison.Ordinal));
        Assert.DoesNotContain(
            composed,
            spike => spike.SourceNeuronId.StartsWith("population:", StringComparison.Ordinal));
    }

    [Fact]
    public void SuperiorColliculusTopographyProducesNeuronalHeadOrientation()
    {
        var superiorColliculus = Snapshot(
            StructureId.SuperiorColliculus,
            "M",
            20.0f,
            topActiveNeurons:
            [
                new NeuronActivity("M-SuperiorColliculus-015", 24.0f),
                new NeuronActivity("M-SuperiorColliculus-031", 22.0f)
            ]);

        var runtime = Decode([superiorColliculus]);

        Assert.True(runtime.Active);
        Assert.True(runtime.HeadYawDrive > 0.50);
        Assert.True(runtime.HeadPitchDrive > 0.45);
        Assert.Equal(0.0, runtime.ForwardDrive, 6);
        Assert.Equal(0.0, runtime.TurnDrive, 6);
        Assert.Contains("orienting=topographic", runtime.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void OrientingOnlyTickCannotRetainStaleDescendingMotorAuthority()
    {
        var settings = CreateSettings() with { SmoothingAlpha = 0.20 };
        var previous = NeuronalMotorPopulationDecoder.Decode(
            tick: 1,
            CreateMotorCircuit(20.0f, 20.0f),
            new NeuronalMotorControlSnapshot(settings),
            NeuronalMotorRuntime.Default);
        var superiorColliculus = Snapshot(
            StructureId.SuperiorColliculus,
            "M",
            20.0f,
            topActiveNeurons:
            [
                new NeuronActivity("M-SuperiorColliculus-015", 24.0f),
                new NeuronActivity("M-SuperiorColliculus-031", 22.0f)
            ]);

        var runtime = NeuronalMotorPopulationDecoder.Decode(
            tick: 2,
            [superiorColliculus],
            new NeuronalMotorControlSnapshot(settings),
            previous);

        Assert.True(previous.ForwardDrive > 0.01);
        Assert.True(runtime.Active);
        Assert.True(Math.Abs(runtime.HeadYawDrive) > 0.01);
        Assert.Equal(0.0, runtime.LeftDrive, 6);
        Assert.Equal(0.0, runtime.RightDrive, 6);
        Assert.Equal(0.0, runtime.ForwardDrive, 6);
        Assert.Equal(0.0, runtime.TurnDrive, 6);
        Assert.Equal(0.0, runtime.ManipulatorDrive, 6);
        Assert.Equal(0.0, runtime.StandDrive, 6);
    }

    [Fact]
    public void EffectorPopulationMagnitudeIsIndependentOfConfiguredPopulationSize()
    {
        var state = CreateAvatarState(
            active: true,
            tick: 15,
            confidence: 0.8,
            left: 0.0,
            right: 0.0,
            leftShoulderSagittal: 0.25);

        var composed = AvatarNeuronalMotorBridge.Compose(state, [], -1, out _, out _);
        var armEvents = composed.Where(spike => spike.SourceNeuronId.StartsWith(
            "effector:arm:left:shoulder:sagittal:", StringComparison.Ordinal)).ToArray();
        var drive = AvatarEffectorCatalog.SummarizeArmDrive(composed);

        Assert.Equal(2, armEvents.Length);
        Assert.All(armEvents, spike => Assert.EndsWith(":n8", spike.SourceNeuronId));
        Assert.Equal(0.25, drive.LeftShoulderSagittalDelta, 6);
        Assert.Equal(0.0, drive.RightShoulderSagittalDelta, 6);
    }

    [Fact]
    public void NeuronalBridgeEmitsSignedOrientingEffectors()
    {
        var state = CreateAvatarState(
            active: true,
            tick: 14,
            confidence: 0.8,
            left: 0.0,
            right: 0.0,
            headYaw: 0.75,
            headPitch: -0.50);

        var composed = AvatarNeuronalMotorBridge.Compose(state, [], -1, out _, out var decoded);

        Assert.Equal(0.75, decoded.HeadYawDrive, 6);
        Assert.Equal(-0.50, decoded.HeadPitchDrive, 6);
        Assert.Contains(composed, spike => spike.SourceNeuronId.StartsWith(
            "effector:orient:yaw:excitatory:", StringComparison.Ordinal));
        Assert.Contains(composed, spike => spike.SourceNeuronId.StartsWith(
            "effector:orient:pitch:inhibitory:", StringComparison.Ordinal));
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

    private static NeuronalMotorRuntime Decode(IReadOnlyList<InstanceStructureSnapshot> snapshots)
        => NeuronalMotorPopulationDecoder.Decode(
            tick: 1,
            snapshots,
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
            StructureId.DentateNucleus,
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
        snapshots.AddRange(CreateActionCircuit(
            selectedChannel: 0,
            selectedOutputInhibition: outputInhibition,
            selectedThalamicRelay: thalamicDisinhibition));
        return snapshots;
    }

    private static InstanceStructureSnapshot Snapshot(
        StructureId structure,
        string hemisphere,
        float rate,
        BasalGangliaDiagnostics? basalGanglia = null,
        CerebellarDiagnostics? cerebellar = null,
        VestibuloReticularDiagnostics? postural = null,
        ActionSelectionDiagnostics? actionSelection = null,
        IReadOnlyList<NeuronActivity>? topActiveNeurons = null)
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
            TopActiveNeurons: topActiveNeurons ?? [],
            NeuromodLocal: new NeuromodState(),
            SpikeInCount: 0,
            SpikeOutCount: 0,
            FeedbackQueueDepth: 0,
            BasalGangliaDiagnostics: basalGanglia,
            CerebellarDiagnostics: cerebellar,
            VestibuloReticularDiagnostics: postural,
            ActionSelectionDiagnostics: actionSelection);
    }

    private static IReadOnlyList<InstanceStructureSnapshot> CreateActionCircuit(
        int selectedChannel,
        float selectedOutputInhibition = 0.04f,
        float selectedThalamicRelay = 0.85f)
    {
        var structures = new[]
        {
            StructureId.Pfc,
            StructureId.Acc,
            StructureId.PremotorCortex,
            StructureId.Sma,
            StructureId.Striatum,
            StructureId.Stn,
            StructureId.GPi,
            StructureId.Snr,
            StructureId.MotorThalamus
        };
        var channels = Enumerable.Range(0, NeuronalActionSelectionDecoder.ChannelCount)
            .Select(channel =>
            {
                var selected = channel == selectedChannel;
                return new ActionChannelActivity(
                    channel,
                    ProposalDrive: selected ? 0.90f : 0.05f,
                    DirectPathwayActivation: selected ? 0.90f : 0.10f,
                    IndirectPathwayActivation: selected ? 0.05f : 0.40f,
                    HyperdirectSuppression: selected ? 0.04f : 0.35f,
                    OutputNucleusInhibition: selected ? selectedOutputInhibition : 0.70f,
                    ThalamicRelayActivation: selected ? selectedThalamicRelay : 0.05f,
                    EligibilityTrace: selected ? 0.35f : 0f,
                    LearnedSynapticStrength: selected ? 2.8f : 0.8f,
                    SelectionScore: 0f);
            })
            .ToArray();

        return structures
            .Select(structure => Snapshot(
                structure,
                "M",
                12.0f,
                actionSelection: new ActionSelectionDiagnostics(
                    structure,
                    channels,
                    selectedChannel,
                    SelectionMargin: 0.5f,
                    DopamineModulation: 0.6f)))
            .ToArray();
    }

    private static IReadOnlyList<InstanceStructureSnapshot> CreateSpinalRightingReflex(
        float? leftDrive,
        float? rightDrive)
    {
        var snapshots = new List<InstanceStructureSnapshot>();
        if (leftDrive is { } left)
        {
            snapshots.Add(SpinalRightingSnapshot("L", left));
        }
        if (rightDrive is { } right)
        {
            snapshots.Add(SpinalRightingSnapshot("R", right));
        }
        return snapshots;
    }

    private static IReadOnlyList<InstanceStructureSnapshot> CreatePrimaryRightingReflex(
        float? leftDrive,
        float? rightDrive)
    {
        var snapshots = new List<InstanceStructureSnapshot>();
        if (leftDrive is { } left)
        {
            snapshots.Add(RightingSnapshot(StructureId.VestibularAfferents, "L", left));
        }
        if (rightDrive is { } right)
        {
            snapshots.Add(RightingSnapshot(StructureId.VestibularAfferents, "R", right));
        }
        return snapshots;
    }

    private static InstanceStructureSnapshot SpinalRightingSnapshot(string hemisphere, float standDrive)
        => RightingSnapshot(StructureId.SpinalCordMotor, hemisphere, standDrive);

    private static InstanceStructureSnapshot RightingSnapshot(
        StructureId structure,
        string hemisphere,
        float standDrive)
    {
        var channels = Enumerable.Range(0, NeuronalActionSelectionDecoder.ChannelCount)
            .Select(channel => new ActionChannelActivity(
                channel,
                ProposalDrive: 0f,
                DirectPathwayActivation: 0f,
                IndirectPathwayActivation: 0f,
                HyperdirectSuppression: 0f,
                OutputNucleusInhibition: 0f,
                ThalamicRelayActivation: 0f,
                EligibilityTrace: 0f,
                LearnedSynapticStrength: 0f,
                SelectionScore: channel == NeuronalActionSelectionDecoder.StandChannel
                    ? standDrive
                    : 0f,
				ReflexDrive: channel == NeuronalActionSelectionDecoder.StandChannel
						? standDrive
						: 0f))
            .ToArray();
        return Snapshot(
            structure,
            hemisphere,
            rate: 20.0f,
            actionSelection: new ActionSelectionDiagnostics(
                structure,
                channels,
                NeuronalActionSelectionDecoder.StandChannel,
                SelectionMargin: standDrive,
                DopamineModulation: 0f));
    }

    private static JsonElement CreateAvatarState(
        bool active,
        long tick,
        double confidence,
        double left,
        double right,
        double manipulator = 0.0,
        double headYaw = 0.0,
        double headPitch = 0.0,
        double leftShoulderSagittal = 0.0,
        double rightShoulderSagittal = 0.0,
        double leftShoulderCoronal = 0.0,
        double rightShoulderCoronal = 0.0,
        double leftElbow = 0.0,
        double rightElbow = 0.0)
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
                manipulatorDrive = manipulator,
                leftShoulderSagittalDrive = leftShoulderSagittal,
                rightShoulderSagittalDrive = rightShoulderSagittal,
                leftShoulderCoronalDrive = leftShoulderCoronal,
                rightShoulderCoronalDrive = rightShoulderCoronal,
                leftElbowDrive = leftElbow,
                rightElbowDrive = rightElbow,
                headYawDrive = headYaw,
                headPitchDrive = headPitch,
                confidence,
                minimumOutputConfidence = 0.45,
                maxPopulationEventsPerSide = 8
            }
        });
}
