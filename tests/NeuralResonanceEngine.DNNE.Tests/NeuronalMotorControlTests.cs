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
        var disinhibited = Decode(CreateMotorCircuit(20.0f, 20.0f, outputInhibition: 0.04f, thalamicDisinhibition: 1.0f));
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
        Assert.Equal(0.0, runtime.ManipulatorDrive, 6);
        Assert.True(runtime.LeftShoulderSagittalDrive > 0.10);
        Assert.Equal(0.0, runtime.RightShoulderSagittalDrive, 6);
        Assert.Equal(0.0, runtime.ForwardDrive, 6);
        Assert.Equal(0.0, runtime.TurnDrive, 6);
    }

    [Fact]
    public void FarSpacePpcCompetitionInhibitsInteractionWithoutSuppressingArmMotion()
    {
        var circuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateActionCircuit(NeuronalActionSelectionDecoder.LeftShoulderFlexionChannel))
            .Concat(CreatePpcReachEvidence(nearBody: 0.1f, leftPeripersonal: 0.1f,
                rightPeripersonal: 0.1f, farSpace: 8.0f))
            .ToArray();

        var runtime = Decode(circuit);

        Assert.True(runtime.LeftShoulderSagittalDrive > 0.75);
        Assert.True(runtime.ManipulatorDrive < 0.10);
        Assert.Contains("reach-gate=ppc:", runtime.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void NearAndPeripersonalPpcCompetitionDoesNotInventAHandClosure()
    {
        var circuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateActionCircuit(NeuronalActionSelectionDecoder.LeftShoulderFlexionChannel))
            .Concat(CreatePpcReachEvidence(nearBody: 8.0f, leftPeripersonal: 5.0f,
                rightPeripersonal: 5.0f, farSpace: 1.0f))
            .ToArray();

        var runtime = Decode(circuit);

        Assert.True(runtime.LeftShoulderSagittalDrive > 0.75);
        Assert.Equal(0.0, runtime.ManipulatorDrive, 6);
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
    [InlineData(NeuronalActionSelectionDecoder.LeftHipAbductionChannel, 0, 1)]
    [InlineData(NeuronalActionSelectionDecoder.LeftHipAdductionChannel, 0, -1)]
    [InlineData(NeuronalActionSelectionDecoder.RightHipAbductionChannel, 1, 1)]
    [InlineData(NeuronalActionSelectionDecoder.RightHipAdductionChannel, 1, -1)]
    public void OpposingHipLanesRecruitOnlyTheirAnatomicalEffector(
        int selectedChannel,
        int expectedDriveIndex,
        int expectedSign)
    {
        var circuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateActionCircuit(selectedChannel))
            .ToArray();

        var runtime = Decode(circuit);
        var drives = new[] { runtime.LeftHipCoronalDrive, runtime.RightHipCoronalDrive };

        Assert.True(runtime.Active);
        Assert.Equal(selectedChannel, runtime.SelectedActionChannel);
        Assert.Equal(expectedSign, Math.Sign(drives[expectedDriveIndex]));
        Assert.True(Math.Abs(drives[expectedDriveIndex]) > 0.10);
        Assert.Single(drives, drive => Math.Abs(drive) > 0.001);
        Assert.Equal(0.0, runtime.ForwardDrive, 6);
        Assert.Equal(0.0, runtime.TurnDrive, 6);
    }

    [Theory]
    [InlineData(NeuronalActionSelectionDecoder.LeftAnkleDorsiflexionChannel, 0, 1)]
    [InlineData(NeuronalActionSelectionDecoder.LeftAnklePlantarflexionChannel, 0, -1)]
    [InlineData(NeuronalActionSelectionDecoder.RightAnkleDorsiflexionChannel, 1, 1)]
    [InlineData(NeuronalActionSelectionDecoder.RightAnklePlantarflexionChannel, 1, -1)]
    [InlineData(NeuronalActionSelectionDecoder.LeftAnkleInversionChannel, 2, 1)]
    [InlineData(NeuronalActionSelectionDecoder.LeftAnkleEversionChannel, 2, -1)]
    [InlineData(NeuronalActionSelectionDecoder.RightAnkleInversionChannel, 3, 1)]
    [InlineData(NeuronalActionSelectionDecoder.RightAnkleEversionChannel, 3, -1)]
    public void OpposingAnkleLanesRecruitOnlyTheirAnatomicalEffector(
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
            runtime.LeftAnkleSagittalDrive,
            runtime.RightAnkleSagittalDrive,
            runtime.LeftAnkleCoronalDrive,
            runtime.RightAnkleCoronalDrive
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
    [InlineData(NeuronalActionSelectionDecoder.TrunkRotateLeftChannel, -1)]
    [InlineData(NeuronalActionSelectionDecoder.TrunkRotateRightChannel, 1)]
    public void OpposingAxialLanesRecruitOnlyTrunkRotation(
        int selectedChannel,
        int expectedSign)
    {
        var circuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateActionCircuit(selectedChannel))
            .ToArray();

        var runtime = Decode(circuit);

        Assert.True(runtime.Active);
        Assert.Equal(selectedChannel, runtime.SelectedActionChannel);
        Assert.Equal(expectedSign, Math.Sign(runtime.TrunkYawDrive));
        Assert.True(Math.Abs(runtime.TrunkYawDrive) > 0.10);
        Assert.Equal(0.0, runtime.ForwardDrive, 6);
        Assert.Equal(0.0, runtime.TurnDrive, 6);
        Assert.Equal(0.0, runtime.ManipulatorDrive, 6);
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
    public void NewPostureWinnerImmediatelyInhibitsStaleCompetingPostures()
    {
        var circuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateActionCircuit(NeuronalActionSelectionDecoder.CrouchChannel))
            .ToArray();
        var previous = NeuronalMotorRuntime.Default with
        {
            StandDrive = 0.8,
            CrouchDrive = 0.7,
            SitDrive = 0.6,
            LieDrive = 0.5
        };

        var runtime = DecodeAt(2, circuit, previous, CreateSettings());

        Assert.True(runtime.CrouchDrive > 0.10);
        Assert.Equal(0.0, runtime.StandDrive, 6);
        Assert.Equal(0.0, runtime.SitDrive, 6);
        Assert.Equal(0.0, runtime.LieDrive, 6);
    }

    [Fact]
    public void BilateralSpinalRightingPopulationCanRecruitStandWithoutActionSelection()
    {
        var circuit = WithPopulationBalanceError(
            CreateMotorCircuit(20.0f, 20.0f)
                .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
                .Concat(CreatePrimaryRightingReflex(leftDrive: 0.85f, rightDrive: 0.80f))
                .Concat(CreateSpinalRightingReflex(leftDrive: 0.80f, rightDrive: 0.75f)),
            0.80f);

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

    [Theory]
    [InlineData(NeuronalActionSelectionDecoder.CrouchChannel)]
    [InlineData(NeuronalActionSelectionDecoder.SitChannel)]
    [InlineData(NeuronalActionSelectionDecoder.LieChannel)]
    public void SelectedNonStandingPostureNeurallyInhibitsRightingReflex(int selectedChannel)
    {
        var circuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateActionCircuit(selectedChannel))
            .Concat(CreatePrimaryRightingReflex(leftDrive: 0.95f, rightDrive: 0.95f))
            .Concat(CreateSpinalRightingReflex(leftDrive: 0.90f, rightDrive: 0.90f))
            .ToArray();

        var runtime = Decode(circuit);
        var nonStandingDrives = new Dictionary<int, double>
        {
            [NeuronalActionSelectionDecoder.CrouchChannel] = runtime.CrouchDrive,
            [NeuronalActionSelectionDecoder.SitChannel] = runtime.SitDrive,
            [NeuronalActionSelectionDecoder.LieChannel] = runtime.LieDrive
        };

        Assert.True(runtime.Active);
        Assert.Equal(selectedChannel, runtime.SelectedActionChannel);
        Assert.Equal(0.0, runtime.StandDrive, 6);
        Assert.True(nonStandingDrives[selectedChannel] > 0.10);
        Assert.All(nonStandingDrives.Where(pair => pair.Key != selectedChannel), pair =>
            Assert.Equal(0.0, pair.Value, 6));
    }

    [Fact]
    public void BilateralRightingReleasesUnrelatedVoluntaryLimbDriveDuringLargeBalanceError()
    {
        var circuit = WithPopulationBalanceError(
            CreateMotorCircuit(20.0f, 20.0f)
                .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
                .Concat(CreateActionCircuit(NeuronalActionSelectionDecoder.LeftShoulderFlexionChannel))
                .Concat(CreatePrimaryRightingReflex(leftDrive: 0.90f, rightDrive: 0.90f))
                .Concat(CreateSpinalRightingReflex(leftDrive: 0.85f, rightDrive: 0.85f)),
            0.80f);

        var runtime = Decode(circuit);

        Assert.Equal(0.0, runtime.LeftShoulderSagittalDrive, 6);
        Assert.Equal(0.0, runtime.RightShoulderSagittalDrive, 6);
        Assert.Equal(0.0, runtime.ForwardDrive, 6);
        Assert.True(runtime.StandDrive > 0.10);
        Assert.Contains("protective-release=righting", runtime.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void RightingLatchDiscardsStaleVoluntaryActionAndRequiresFreshSelectionAfterRecovery()
    {
        var settings = CreateSettings() with { SmoothingAlpha = 1.0 };
        var voluntaryCircuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateActionCircuit(NeuronalActionSelectionDecoder.LeftShoulderFlexionChannel))
            .ToArray();
        var rightingCircuit = WithPopulationBalanceError(
            voluntaryCircuit
                .Concat(CreatePrimaryRightingReflex(leftDrive: 0.90f, rightDrive: 0.90f))
                .Concat(CreateSpinalRightingReflex(leftDrive: 0.85f, rightDrive: 0.85f)),
            0.80f);

        var voluntary = DecodeAt(1, voluntaryCircuit, NeuronalMotorRuntime.Default, settings);
        var righting = DecodeAt(2, rightingCircuit, voluntary, settings);

        Assert.Equal(NeuronalActionSelectionDecoder.LeftShoulderFlexionChannel, voluntary.SelectedActionChannel);
        Assert.True(righting.RightingLatchActive);
        Assert.Equal(-1, righting.SelectedActionChannel);
        Assert.Equal(0.0, righting.LeftShoulderSagittalDrive, 6);

        var settling = righting;
        for (var tick = 3L; tick <= 5L; tick++)
        {
            settling = DecodeAt(tick, voluntaryCircuit, settling, settings);
            Assert.True(settling.RightingLatchActive);
            Assert.Equal(-1, settling.SelectedActionChannel);
        }

        var recovered = DecodeAt(6, voluntaryCircuit, settling, settings);
        Assert.False(recovered.RightingLatchActive);
        Assert.True(recovered.FreshActionRequired);
        Assert.Equal(-1, recovered.SelectedActionChannel);
        Assert.Equal(0.0, recovered.LeftShoulderSagittalDrive, 6);

        var freshlySelected = DecodeAt(7, voluntaryCircuit, recovered, settings);
        Assert.False(freshlySelected.FreshActionRequired);
        Assert.Equal(NeuronalActionSelectionDecoder.LeftShoulderFlexionChannel, freshlySelected.SelectedActionChannel);
        Assert.True(freshlySelected.LeftShoulderSagittalDrive > 0.10);
        Assert.Equal(7, freshlySelected.ActionProgramStartedTick);
    }

    [Fact]
    public void StableBalanceReleasesRightingLatchDespiteTonicPosturalDrive()
    {
        var settings = CreateSettings() with { SmoothingAlpha = 1.0 };
        var voluntaryCircuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateActionCircuit(NeuronalActionSelectionDecoder.ForwardChannel))
            .ToArray();
        var disturbedCircuit = WithPopulationBalanceError(
            voluntaryCircuit
                .Concat(CreatePrimaryRightingReflex(leftDrive: 0.90f, rightDrive: 0.90f))
                .Concat(CreateSpinalRightingReflex(leftDrive: 0.85f, rightDrive: 0.85f)),
            0.80f);
        var stableTonicCircuit = WithPopulationBalanceError(
            voluntaryCircuit
                .Concat(CreatePrimaryRightingReflex(leftDrive: 0.23f, rightDrive: 0.23f))
                .Concat(CreateSpinalRightingReflex(leftDrive: 0.51f, rightDrive: 0.51f)),
            0.0f);

        var runtime = DecodeAt(1, disturbedCircuit, NeuronalMotorRuntime.Default, settings);
        Assert.True(runtime.RightingLatchActive);

        for (var tick = 2L; tick <= 4L; tick++)
        {
            runtime = DecodeAt(tick, stableTonicCircuit, runtime, settings);
            Assert.True(runtime.RightingLatchActive);
            Assert.Equal(tick - 1, runtime.RightingStableTicks);
            Assert.Contains("righting=bilateral:0.230", runtime.Evidence, StringComparison.Ordinal);
        }

        runtime = DecodeAt(5, stableTonicCircuit, runtime, settings);
        Assert.False(runtime.RightingLatchActive);
        Assert.True(runtime.FreshActionRequired);
        Assert.Equal(-1, runtime.SelectedActionChannel);

        runtime = DecodeAt(6, stableTonicCircuit, runtime, settings);
        Assert.False(runtime.RightingLatchActive);
        Assert.Equal(NeuronalActionSelectionDecoder.ForwardChannel, runtime.SelectedActionChannel);
        Assert.True(runtime.ForwardDrive > 0.10);
    }

    [Fact]
    public void CompensatedPopulationIgnoresUnintegratedLocalVestibularError()
    {
        var settings = CreateSettings() with { SmoothingAlpha = 1.0 };
        var stableCircuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateActionCircuit(NeuronalActionSelectionDecoder.ForwardChannel))
            .Concat(CreatePrimaryRightingReflex(leftDrive: 0.23f, rightDrive: 0.23f))
            .Concat(CreateSpinalRightingReflex(leftDrive: 0.51f, rightDrive: 0.51f))
            .Select(snapshot => snapshot.StructureId == StructureId.VestibularNuclei &&
                                snapshot.VestibuloReticularDiagnostics is not null
                ? snapshot with
                {
                    VestibuloReticularDiagnostics = snapshot.VestibuloReticularDiagnostics with
                    {
                        BalanceError = snapshot.MeanFiringRateHz
                    }
                }
                : snapshot)
            .ToArray();
        var runtime = NeuronalMotorRuntime.Default with
        {
            RightingLatchActive = true,
            RightingEnteredTick = 1
        };

        for (var tick = 2L; tick <= 5L; tick++)
        {
            runtime = DecodeAt(tick, stableCircuit, runtime, settings);
        }

        Assert.False(runtime.RightingLatchActive);
        Assert.Contains("balance-error=population:0.000", runtime.Evidence, StringComparison.Ordinal);

        runtime = DecodeAt(6, stableCircuit, runtime, settings);
        Assert.Equal(NeuronalActionSelectionDecoder.ForwardChannel, runtime.SelectedActionChannel);
        Assert.True(runtime.ForwardDrive > 0.10);
    }

    [Fact]
    public void RightingLatchRecoveryCounterResetsWhenInstabilityReturns()
    {
        var settings = CreateSettings();
        var quietCircuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .ToArray();
        var rightingCircuit = WithPopulationBalanceError(
            quietCircuit
                .Concat(CreatePrimaryRightingReflex(leftDrive: 0.90f, rightDrive: 0.90f))
                .Concat(CreateSpinalRightingReflex(leftDrive: 0.85f, rightDrive: 0.85f)),
            0.80f);

        var runtime = DecodeAt(1, rightingCircuit, NeuronalMotorRuntime.Default, settings);
        runtime = DecodeAt(2, quietCircuit, runtime, settings);
        runtime = DecodeAt(3, quietCircuit, runtime, settings);
        Assert.Equal(2, runtime.RightingStableTicks);

        runtime = DecodeAt(4, rightingCircuit, runtime, settings);

        Assert.True(runtime.RightingLatchActive);
        Assert.Equal(0, runtime.RightingStableTicks);
    }

    [Fact]
    public void AcuteLocalWithdrawalRetainsPriorityDuringBilateralRightingRelease()
    {
        var circuit = WithPopulationBalanceError(
            CreateMotorCircuit(20.0f, 20.0f)
                .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
                .Concat(CreateActionCircuit(NeuronalActionSelectionDecoder.LeftShoulderFlexionChannel))
                .Concat(CreatePrimaryRightingReflex(leftDrive: 0.90f, rightDrive: 0.90f))
                .Concat(CreateSpinalRightingReflex(leftDrive: 0.85f, rightDrive: 0.85f))
                .Append(SpinalWithdrawalSnapshot(
                    NeuronalActionSelectionDecoder.LeftShoulderExtensionChannel,
                    NeuronalActionSelectionDecoder.LeftElbowFlexionChannel,
                    drive: 0.82f)),
            0.80f);

        var runtime = Decode(circuit);

        Assert.True(runtime.LeftShoulderSagittalDrive < -0.70);
        Assert.True(runtime.LeftElbowDrive > 0.70);
        Assert.True(runtime.StandDrive > 0.10);
        Assert.Contains("protective-release=righting", runtime.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void NearTieRetainsRecentNeuronalWinnerButStrongLaterEvidenceCanSwitch()
    {
        var settings = CreateSettings() with
        {
            ActionPersistenceMilliseconds = 96,
            ActionPersistenceBias = 0.06
        };
        var firstCircuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateActionCircuit(NeuronalActionSelectionDecoder.LeftShoulderFlexionChannel))
            .ToArray();
        var first = DecodeAt(100, firstCircuit, NeuronalMotorRuntime.Default, settings);
        var competingCircuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateCompetitiveActionCircuit(
                preferredChannel: NeuronalActionSelectionDecoder.LeftShoulderFlexionChannel,
                challengerChannel: NeuronalActionSelectionDecoder.RightShoulderFlexionChannel))
            .ToArray();

        var retained = DecodeAt(120, competingCircuit, first, settings);
        var released = DecodeAt(220, competingCircuit, first, settings);

        Assert.Equal(NeuronalActionSelectionDecoder.LeftShoulderFlexionChannel, retained.SelectedActionChannel);
        Assert.True(retained.ActionPersistenceApplied);
        Assert.Equal(first.ActionProgramStartedTick, retained.ActionProgramStartedTick);
        Assert.Equal(first.ActionProgramStartedMonotonicMs, retained.ActionProgramStartedMonotonicMs);
        Assert.Equal(NeuronalActionSelectionDecoder.RightShoulderFlexionChannel, released.SelectedActionChannel);
        Assert.False(released.ActionPersistenceApplied);
        Assert.Equal(220, released.ActionProgramStartedTick);
    }

    [Fact]
    public void HabenularNegativePredictionReleasesNearTieWithoutSelectingAnEscapeDirection()
    {
        var settings = CreateSettings() with
        {
            ActionPersistenceMilliseconds = 350,
            ActionPersistenceBias = 0.06
        };
        var firstCircuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateActionCircuit(NeuronalActionSelectionDecoder.ForwardChannel))
            .ToArray();
        var first = DecodeAt(1, firstCircuit, NeuronalMotorRuntime.Default, settings);
        var competingCircuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateCompetitiveActionCircuit(
                NeuronalActionSelectionDecoder.ForwardChannel,
                NeuronalActionSelectionDecoder.ReverseChannel))
            .Append(Snapshot(StructureId.Habenula, "M", 25.0f))
            .ToArray();

        var released = DecodeAt(2, competingCircuit, first, settings);

        Assert.False(released.ActionPersistenceApplied);
        Assert.Equal(NeuronalActionSelectionDecoder.ReverseChannel, released.SelectedActionChannel);
        Assert.Contains("persistence=neuronally-released", released.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void SustainedHabenularAversiveEvidenceInhibitsTheResponsibleActionPopulation()
    {
        var settings = CreateSettings() with
        {
            ActionPersistenceMilliseconds = 350,
            ActionPersistenceBias = 0.06
        };
        var actionCircuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateActionCircuit(NeuronalActionSelectionDecoder.ForwardChannel))
            .ToArray();
        var first = NeuronalMotorPopulationDecoder.Decode(
            1,
            actionCircuit,
            new NeuronalMotorControlSnapshot(settings),
            NeuronalMotorRuntime.Default,
            monotonicMilliseconds: 1_000);
        var aversiveCircuit = actionCircuit
            .Append(Snapshot(StructureId.Habenula, "M", 25.0f))
            .ToArray();

        var released = NeuronalMotorPopulationDecoder.Decode(
            2,
            aversiveCircuit,
            new NeuronalMotorControlSnapshot(settings),
            first,
            monotonicMilliseconds: 1_400);

        Assert.Equal(NeuronalActionSelectionDecoder.ForwardChannel, first.SelectedActionChannel);
        Assert.NotEqual(NeuronalActionSelectionDecoder.ForwardChannel, released.SelectedActionChannel);
        Assert.Contains("protective-release=aversive", released.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void ActionPersistenceUsesPhysicalMillisecondsRatherThanSchedulerTicks()
    {
        var settings = CreateSettings() with
        {
            ActionPersistenceMilliseconds = 350,
            ActionPersistenceBias = 0.06
        };
        var firstCircuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateActionCircuit(NeuronalActionSelectionDecoder.LeftShoulderFlexionChannel))
            .ToArray();
        var competingCircuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateCompetitiveActionCircuit(
                NeuronalActionSelectionDecoder.LeftShoulderFlexionChannel,
                NeuronalActionSelectionDecoder.RightShoulderFlexionChannel))
            .ToArray();
        var first = NeuronalMotorPopulationDecoder.Decode(
            1,
            firstCircuit,
            new NeuronalMotorControlSnapshot(settings),
            NeuronalMotorRuntime.Default,
            monotonicMilliseconds: 1_000);

        var retainedAcrossTickGap = NeuronalMotorPopulationDecoder.Decode(
            5_000,
            competingCircuit,
            new NeuronalMotorControlSnapshot(settings),
            first,
            monotonicMilliseconds: 1_100);
        var releasedAfterPhysicalDeadline = NeuronalMotorPopulationDecoder.Decode(
            5_001,
            competingCircuit,
            new NeuronalMotorControlSnapshot(settings),
            first,
            monotonicMilliseconds: 1_400);

        Assert.True(retainedAcrossTickGap.ActionPersistenceApplied);
        Assert.Equal(first.SelectedActionChannel, retainedAcrossTickGap.SelectedActionChannel);
        Assert.False(releasedAfterPhysicalDeadline.ActionPersistenceApplied);
        Assert.Equal(
            NeuronalActionSelectionDecoder.RightShoulderFlexionChannel,
            releasedAfterPhysicalDeadline.SelectedActionChannel);
    }

    [Fact]
    public void ReciprocalNeuronalWinnerReleasesPersistedActionImmediately()
    {
        var settings = CreateSettings() with
        {
            ActionPersistenceMilliseconds = 350,
            ActionPersistenceBias = 0.06
        };
        var firstCircuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateActionCircuit(NeuronalActionSelectionDecoder.LeftShoulderFlexionChannel))
            .ToArray();
        var reciprocalCircuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateCompetitiveActionCircuit(
                NeuronalActionSelectionDecoder.LeftShoulderFlexionChannel,
                NeuronalActionSelectionDecoder.LeftShoulderExtensionChannel))
            .ToArray();
        var first = NeuronalMotorPopulationDecoder.Decode(
            1,
            firstCircuit,
            new NeuronalMotorControlSnapshot(settings),
            NeuronalMotorRuntime.Default,
            monotonicMilliseconds: 1_000);

        var reciprocal = NeuronalMotorPopulationDecoder.Decode(
            2,
            reciprocalCircuit,
            new NeuronalMotorControlSnapshot(settings),
            first,
            monotonicMilliseconds: 1_020);

        Assert.False(reciprocal.ActionPersistenceApplied);
        Assert.Equal(NeuronalActionSelectionDecoder.LeftShoulderExtensionChannel, reciprocal.SelectedActionChannel);
        Assert.Contains("persistence=neuronally-released", reciprocal.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void ReciprocalReleaseRapidlyClearsPreviousUnrelatedMotorProgram()
    {
        var settings = CreateSettings() with
        {
            SmoothingAlpha = 0.20,
            ReciprocalReleaseAlpha = 0.80,
            ActionPersistenceMilliseconds = 0
        };
        var shoulderCircuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateActionCircuit(NeuronalActionSelectionDecoder.LeftShoulderFlexionChannel))
            .ToArray();
        var ankleCircuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateActionCircuit(NeuronalActionSelectionDecoder.LeftAnkleDorsiflexionChannel))
            .ToArray();

        var shoulder = DecodeAt(1, shoulderCircuit, NeuronalMotorRuntime.Default, settings);
        var ankle = DecodeAt(2, ankleCircuit, shoulder, settings);

        Assert.True(shoulder.LeftShoulderSagittalDrive > 0.10);
        Assert.InRange(ankle.LeftShoulderSagittalDrive, 0.0, shoulder.LeftShoulderSagittalDrive * 0.21);
        Assert.True(ankle.LeftAnkleSagittalDrive > 0.10);
    }

    [Fact]
    public void SpinalWithdrawalOverridesReciprocalVoluntaryArmDrive()
    {
        var circuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateActionCircuit(NeuronalActionSelectionDecoder.LeftShoulderFlexionChannel))
            .Append(SpinalWithdrawalSnapshot(
                NeuronalActionSelectionDecoder.LeftShoulderExtensionChannel,
                NeuronalActionSelectionDecoder.LeftElbowFlexionChannel,
                drive: 0.82f))
            .ToArray();

        var runtime = Decode(circuit);

        Assert.True(runtime.LeftShoulderSagittalDrive < -0.70);
        Assert.True(runtime.LeftElbowDrive > 0.70);
        Assert.Equal(0.82, runtime.SpinalWithdrawalDrive, 6);
        Assert.Contains("withdrawal=spinal", runtime.Evidence, StringComparison.Ordinal);
        Assert.Contains("persistence=neuronally-released", runtime.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void SpinalWithdrawalSourceAttributionSurvivesMotorDecoding()
    {
        var attributedSource = new SpinalWithdrawalSourceActivity(
            SourceKey: "left:hand:normal_z_neg:channel_5",
            BodySide: "left",
            Region: "hand",
            ContactNormalSector: "normal_z_neg",
            ChannelIndex: NeuronalActionSelectionDecoder.LeftShoulderExtensionChannel,
            MotorProjection: "left_shoulder_extension",
            AfferentDrive: 0.88f,
            ReflexDrive: 0.82f,
            RecurrentInhibition: 0.61f);
        var circuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Append(SpinalWithdrawalSnapshot(
                NeuronalActionSelectionDecoder.LeftShoulderExtensionChannel,
                NeuronalActionSelectionDecoder.LeftElbowFlexionChannel,
                drive: 0.82f,
                sources: [attributedSource]))
            .ToArray();

        var runtime = Decode(circuit);

        var source = Assert.Single(runtime.SpinalWithdrawalSources!);
        Assert.Equal(attributedSource, source);
    }

    [Fact]
    public void AxialSpinalWithdrawalReleasesForwardBracingAndRecruitsReverseDrive()
    {
        var settings = CreateSettings();
        var forwardCircuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateActionCircuit(NeuronalActionSelectionDecoder.ForwardChannel))
            .ToArray();
        var forward = DecodeAt(1, forwardCircuit, NeuronalMotorRuntime.Default, settings);
        var circuit = CreateMotorCircuit(20.0f, 20.0f)
            .Where(snapshot => snapshot.ActionSelectionDiagnostics is null)
            .Concat(CreateActionCircuit(NeuronalActionSelectionDecoder.ForwardChannel))
            .Append(SpinalWithdrawalSnapshot(
                NeuronalActionSelectionDecoder.ReverseChannel,
                NeuronalActionSelectionDecoder.ReverseChannel,
                drive: 0.82f))
            .ToArray();

        var runtime = forward;
        for (var tick = 2L; tick <= 8L; tick++)
        {
            runtime = DecodeAt(tick, circuit, runtime, settings);
        }

        Assert.True(runtime.LeftDrive < -0.70);
        Assert.True(runtime.RightDrive < -0.70);
        Assert.Equal(0.82, runtime.SpinalWithdrawalDrive, 6);
        Assert.Contains("protective-release=spinal", runtime.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void NeuronalBridgeAddsSidedHandEffectorPopulation()
    {
        var state = CreateAvatarState(
            active: true,
            tick: 13,
            confidence: 0.8,
            left: 0.0,
            right: 0.0,
            leftHandGrasp: 0.75,
            spinalWithdrawal: 0.72);

        var composed = AvatarNeuronalMotorBridge.Compose(state, [], -1, out _, out var decoded);

        Assert.Equal(0.0, decoded.ManipulatorDrive, 6);
        Assert.Equal(0.75, decoded.LeftHandGraspDrive, 6);
        Assert.Equal(0.72, decoded.SpinalWithdrawalDrive, 6);
        Assert.Contains(
            composed,
            spike => spike.SourceNeuronId.StartsWith("effector:hand:left:grasp:excitatory:", StringComparison.Ordinal));
        Assert.DoesNotContain(
            composed,
            spike => spike.SourceNeuronId.StartsWith("population:", StringComparison.Ordinal));
    }

    [Fact]
    public void NeuronalBridgeParsesWithdrawalSourceAttributionWithoutMotorAuthority()
    {
        var expected = new SpinalWithdrawalSourceActivity(
            SourceKey: "right:shin:unspecified:channel_26",
            BodySide: "right",
            Region: "shin",
            ContactNormalSector: "unspecified",
            ChannelIndex: NeuronalActionSelectionDecoder.RightAnkleDorsiflexionChannel,
            MotorProjection: "right_ankle_dorsiflexion",
            AfferentDrive: 0.76f,
            ReflexDrive: 0.64f,
            RecurrentInhibition: 0.52f);
        var state = CreateAvatarState(
            active: true,
            tick: 14,
            confidence: 0.8,
            left: 0.0,
            right: 0.0,
            spinalWithdrawal: 0.64,
            spinalWithdrawalSources: [expected]);

        var composed = AvatarNeuronalMotorBridge.Compose(state, [], -1, out _, out var decoded);

        Assert.Equal(expected, Assert.Single(decoded.SpinalWithdrawalSources!));
        Assert.Empty(composed);
    }

    [Fact]
    public void NeuronalBridgeEmitsIndependentTwoAxisAnkleEffectors()
    {
        var state = CreateAvatarState(
            active: true,
            tick: 16,
            confidence: 0.8,
            left: 0.0,
            right: 0.0,
            leftAnkleSagittal: 0.75,
            rightAnkleSagittal: -0.50,
            leftAnkleCoronal: 0.25,
            rightAnkleCoronal: -0.25);

        var composed = AvatarNeuronalMotorBridge.Compose(state, [], -1, out _, out var decoded);
        var leg = AvatarEffectorCatalog.SummarizeLegDrive(composed);

        Assert.Equal(0.75, decoded.LeftAnkleSagittalDrive, 6);
        Assert.Equal(-0.50, decoded.RightAnkleSagittalDrive, 6);
        Assert.Equal(0.75, leg.LeftAnkleSagittalDelta, 6);
        Assert.Equal(-0.50, leg.RightAnkleSagittalDelta, 6);
        Assert.Equal(0.25, leg.LeftAnkleCoronalDelta, 6);
        Assert.Equal(-0.25, leg.RightAnkleCoronalDelta, 6);
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
    public void PopulationCodeDrivesContralateralAnatomicalSide()
    {
        var summary = AvatarMotorCatalog.SummarizeMotorDrive(new[]
        {
            new AvatarDispatchSpike("SpinalCordMotor", "L", 100, "population:l:excitatory:1:0"),
            new AvatarDispatchSpike("SpinalCordMotor", "R", 101, "population:r:inhibitory:1:0")
        });

        Assert.True(summary.LeftInput < 0.0);
        Assert.True(summary.RightInput > 0.0);
        Assert.Equal(2, summary.MotorEvents);
    }

    [Fact]
    public void NeuronalBridgePreservesHemisphereUntilContralateralBodyHandoff()
    {
        var state = CreateAvatarState(active: true, tick: 19, confidence: 0.9, left: 0.8, right: 0.0);

        var dispatches = AvatarNeuronalMotorBridge.Compose(state, [], -1, out _, out _);
        var summary = AvatarMotorCatalog.SummarizeMotorDrive(dispatches);

        Assert.Contains(dispatches, spike =>
            spike.SourceHemisphere == "L" &&
            spike.SourceNeuronId.StartsWith("population:l:", StringComparison.Ordinal));
        Assert.Equal(0.0, summary.LeftInput, 6);
        Assert.True(summary.RightInput > 0.0);
    }

    private static NeuronalMotorRuntime Decode(IReadOnlyList<InstanceStructureSnapshot> snapshots)
        => DecodeAt(1, snapshots, NeuronalMotorRuntime.Default, CreateSettings());

    private static NeuronalMotorRuntime DecodeAt(
        long tick,
        IReadOnlyList<InstanceStructureSnapshot> snapshots,
        NeuronalMotorRuntime previous,
        NeuronalMotorControlSettings settings)
        => NeuronalMotorPopulationDecoder.Decode(
            tick,
            snapshots,
            new NeuronalMotorControlSnapshot(settings),
            previous);

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
        float outputInhibition = 0.04f,
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
            StructureId.CerebellarVermis,
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
        BodySchemaDiagnostics? bodySchema = null,
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
            BodySchemaDiagnostics: bodySchema,
            BasalGangliaDiagnostics: basalGanglia,
            CerebellarDiagnostics: cerebellar,
            VestibuloReticularDiagnostics: postural,
            ActionSelectionDiagnostics: actionSelection);
    }

    private static IReadOnlyList<InstanceStructureSnapshot> CreatePpcReachEvidence(
        float nearBody,
        float leftPeripersonal,
        float rightPeripersonal,
        float farSpace)
    {
        var diagnostics = new BodySchemaDiagnostics(
            DominantBodyZone: "HandArm",
            DominantSpatialZone: farSpace > nearBody + leftPeripersonal + rightPeripersonal
                ? "FarSpace"
                : "NearBody",
            FaceHeadActivation: 0.2f,
            HandArmActivation: 8.0f,
            TrunkActivation: 0.2f,
            LegFootActivation: 0.2f,
            NearBodyActivation: nearBody,
            LeftPeripersonalActivation: leftPeripersonal,
            RightPeripersonalActivation: rightPeripersonal,
            FarSpaceActivation: farSpace);
        return
        [
            Snapshot(StructureId.Ppc, "L", 8.0f, bodySchema: diagnostics),
            Snapshot(StructureId.Ppc, "R", 8.0f, bodySchema: diagnostics)
        ];
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

    private static IReadOnlyList<InstanceStructureSnapshot> CreateCompetitiveActionCircuit(
        int preferredChannel,
        int challengerChannel)
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
                var preferred = channel == preferredChannel;
                var challenger = channel == challengerChannel;
                return new ActionChannelActivity(
                    channel,
                    ProposalDrive: challenger ? 0.77f : preferred ? 0.74f : 0.05f,
                    DirectPathwayActivation: challenger ? 0.74f : preferred ? 0.72f : 0.10f,
                    IndirectPathwayActivation: challenger || preferred ? 0.08f : 0.40f,
                    HyperdirectSuppression: challenger || preferred ? 0.08f : 0.35f,
                    OutputNucleusInhibition: challenger || preferred ? 0.08f : 0.70f,
                    ThalamicRelayActivation: challenger ? 0.74f : preferred ? 0.72f : 0.05f,
                    EligibilityTrace: challenger || preferred ? 0.20f : 0f,
                    LearnedSynapticStrength: challenger || preferred ? 2.2f : 0.8f,
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
                    challengerChannel,
                    SelectionMargin: 0.02f,
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

    private static IReadOnlyList<InstanceStructureSnapshot> WithPopulationBalanceError(
        IEnumerable<InstanceStructureSnapshot> snapshots,
        float balanceError)
    {
        var materialized = snapshots.ToArray();
        var vermisRate = materialized
            .Where(static snapshot => snapshot.StructureId == StructureId.CerebellarVermis)
            .Select(static snapshot => snapshot.MeanFiringRateHz)
            .DefaultIfEmpty(0f)
            .Average();
        var spinalRate = materialized
            .Where(static snapshot => snapshot.StructureId == StructureId.SpinalCordMotor)
            .Select(static snapshot => snapshot.MeanFiringRateHz)
            .DefaultIfEmpty(0f)
            .Average();
        var targetVestibularRate = Math.Max(0f, balanceError) +
            (vermisRate * 0.55f) +
            (spinalRate * 0.25f);

        return materialized
            .Select(snapshot => snapshot.StructureId == StructureId.VestibularNuclei
                ? snapshot with { MeanFiringRateHz = targetVestibularRate }
                : snapshot)
            .ToArray();
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

    private static InstanceStructureSnapshot SpinalWithdrawalSnapshot(
        int firstChannel,
        int secondChannel,
        float drive,
        IReadOnlyList<SpinalWithdrawalSourceActivity>? sources = null)
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
                SelectionScore: channel == firstChannel || channel == secondChannel ? drive : 0f,
                ReflexDrive: channel == firstChannel || channel == secondChannel ? drive : 0f))
            .ToArray();
        return Snapshot(
            StructureId.SpinalCordMotor,
            "L",
            rate: 20.0f,
            actionSelection: new ActionSelectionDiagnostics(
                StructureId.SpinalCordMotor,
                channels,
                firstChannel,
                SelectionMargin: drive,
                DopamineModulation: 0f,
                WithdrawalSources: sources));
    }

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
        double rightElbow = 0.0,
        double leftAnkleSagittal = 0.0,
        double rightAnkleSagittal = 0.0,
        double leftAnkleCoronal = 0.0,
        double rightAnkleCoronal = 0.0,
        double leftHandGrasp = 0.0,
        double rightHandGrasp = 0.0,
        double spinalWithdrawal = 0.0,
        IReadOnlyList<SpinalWithdrawalSourceActivity>? spinalWithdrawalSources = null)
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
                leftAnkleSagittalDrive = leftAnkleSagittal,
                rightAnkleSagittalDrive = rightAnkleSagittal,
                leftAnkleCoronalDrive = leftAnkleCoronal,
                rightAnkleCoronalDrive = rightAnkleCoronal,
                leftHandGraspDrive = leftHandGrasp,
                rightHandGraspDrive = rightHandGrasp,
                spinalWithdrawalDrive = spinalWithdrawal,
                spinalWithdrawalSources,
                headYawDrive = headYaw,
                headPitchDrive = headPitch,
                confidence,
                minimumOutputConfidence = 0.45,
                maxPopulationEventsPerSide = 8
            }
        });
}
