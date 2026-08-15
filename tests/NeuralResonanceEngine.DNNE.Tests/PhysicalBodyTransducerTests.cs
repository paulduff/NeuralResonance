using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class PhysicalBodyTransducerTests
{
    [Fact]
    public void PhysicalMeasurementsBecomeThreeFixedNeuronalAfferentPopulations()
    {
        var runtime = new PhysicalBodyTransducerRuntime();
        var first = CreateDescriptor(new PhysicalBodyFrameRequest(
            1, 1_000, 0f, 0f, 0f, 0f, 0f, 0f,
            8_000_000f, 1f, 37f, 0.98f, 0.80f, "test_body"));
        runtime.Transduce(first, tick: 1, timestampMs: 1);

        var second = CreateDescriptor(new PhysicalBodyFrameRequest(
            2, 1_100, 0.4f, 0f, 3f, 0f, 1.2f, 0f,
            2_000_000f, 0.72f, 39f, 0.82f, 0.54f, "test_body"));
        var result = runtime.Transduce(second, tick: 2, timestampMs: 2);

        Assert.True(result.ActiveProprioceptivePopulations > 0);
        Assert.True(result.ActiveVestibularPopulations > 0);
        Assert.True(result.ActiveVisceralPopulations > 0);
        Assert.True(result.LinearAccelerationMagnitude > 0f);
        Assert.True(result.HomeostaticDeviation > 0f);

        AssertFixedAfferentSpikes(result.ProprioceptiveLeft, StructureId.ProprioceptiveAfferents, "L");
        AssertFixedAfferentSpikes(result.VestibularRight, StructureId.VestibularAfferents, "R");
        AssertFixedAfferentSpikes(result.VisceralLeft, StructureId.VisceralAfferents, "L");
    }

    [Fact]
    public void InvalidPhysiologyIsRejectedBeforeNeuralTransduction()
    {
        var valid = PhysicalBodyFrameDescriptor.TryCreate(
            new PhysicalBodyFrameRequest(
                1, 1, 0f, 0f, 0f, 0f, 0f, 0f,
                8_000_000f, 1.2f, 37f, 0.98f, 0.8f, "test"),
            out var descriptor,
            out var error);

        Assert.False(valid);
        Assert.Null(descriptor);
        Assert.Contains("physiological", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ArticulatedLimbMeasurementsReachContralateralProprioceptiveAfferents()
    {
        var articulation = PhysicalArticulationFrame.Neutral with
        {
            RightHipAngleRadians = 0.72f,
            RightFootLoadNewtons = 640f,
            RightShoulderAngleRadians = 0.88f,
            RightHandLoadNewtons = 220f
        };
        var runtime = new PhysicalBodyTransducerRuntime();
        var descriptor = CreateDescriptor(new PhysicalBodyFrameRequest(
            3, 1_200, 0f, 0f, 0f, 0f, 0f, 0f,
            8_000_000f, 1f, 37f, 0.98f, 0.80f, "test_articulation", articulation));

        var result = runtime.Transduce(descriptor, tick: 3, timestampMs: 3);

        Assert.Contains(result.ProprioceptiveLeft, spike =>
            spike.SourceNeuronId.Contains("right_hip_flexor_spindle", StringComparison.Ordinal));
        Assert.Contains(result.ProprioceptiveLeft, spike =>
            spike.SourceNeuronId.Contains("right_foot_golgi_load", StringComparison.Ordinal));
        Assert.Contains(result.ProprioceptiveLeft, spike =>
            spike.SourceNeuronId.Contains("right_hand_golgi_load", StringComparison.Ordinal));
        Assert.DoesNotContain(result.ProprioceptiveRight, spike =>
            spike.SourceNeuronId.Contains("right_hand_golgi_load", StringComparison.Ordinal));
    }

    [Fact]
    public void MuscleSpindlesAndGolgiTendonsReachContralateralProprioceptiveAfferents()
    {
        var muscle = new PhysicalMuscleMeasurement(
            Name: "Quadriceps",
            Side: "R",
            Activation: 0.82f,
            ForceNewtons: 1_750f,
            LengthFraction: 1.18f,
            VelocityPerSecond: -1.2f,
            FatigueFraction: 0.08f);
        var articulation = PhysicalArticulationFrame.Neutral with
        {
            Musculoskeletal = new MusculoskeletalStateFrame(
                Posture: "standing",
                BodyHeightMeters: 1.72f,
                UprightFraction: 0.97f,
                SupportFraction: 1.0f,
                BalanceError: 0.06f,
                Muscles: [muscle])
        };
        var runtime = new PhysicalBodyTransducerRuntime();
        var descriptor = CreateDescriptor(new PhysicalBodyFrameRequest(
            5, 1_400, 0f, 0f, 0f, 0f, 0f, 0f,
            8_000_000f, 1f, 37f, 0.98f, 0.80f, "muscle_receptors", articulation));

        var result = runtime.Transduce(descriptor, tick: 5, timestampMs: 5);

        Assert.Contains(result.ProprioceptiveLeft, spike =>
            spike.SourceNeuronId.Contains("r_quadriceps_primary_spindle", StringComparison.Ordinal));
        Assert.Contains(result.ProprioceptiveLeft, spike =>
            spike.SourceNeuronId.Contains("r_quadriceps_dynamic_spindle", StringComparison.Ordinal));
        Assert.Contains(result.ProprioceptiveLeft, spike =>
            spike.SourceNeuronId.Contains("r_quadriceps_golgi_tendon", StringComparison.Ordinal));
        Assert.DoesNotContain(result.ProprioceptiveRight, spike =>
            spike.SourceNeuronId.Contains("r_quadriceps", StringComparison.Ordinal));
    }

    [Fact]
    public void DynamicBalanceMeasurementsReachProprioceptiveAndVestibularAfferents()
    {
        var balance = new PhysicalBalanceStateFrame(
            CenterOfMassXMeters: 0.24f,
            CenterOfMassYMeters: 0.92f,
            CenterOfMassZMeters: 0.18f,
            CenterOfMassVelocityXMetersPerSecond: 0.8f,
            CenterOfMassVelocityZMetersPerSecond: 0.5f,
            ExtrapolatedCenterOfMassXMeters: 0.47f,
            ExtrapolatedCenterOfMassZMeters: 0.34f,
            CenterOfPressureXMeters: -0.03f,
            CenterOfPressureZMeters: 0.01f,
            SupportAreaSquareMeters: 0.04f,
            SupportMarginMeters: -0.11f,
            FallPitchRadians: 0.36f,
            FallRollRadians: -0.28f,
            FallPitchVelocityRadiansPerSecond: 1.4f,
            FallRollVelocityRadiansPerSecond: -1.1f,
            Phase: "falling");
        var articulation = PhysicalArticulationFrame.Neutral with
        {
            Musculoskeletal = MusculoskeletalStateFrame.Neutral with { Balance = balance }
        };
        var runtime = new PhysicalBodyTransducerRuntime();
        var descriptor = CreateDescriptor(new PhysicalBodyFrameRequest(
            6, 1_500, 0f, 0f, 0f, 1.4f, 0f, -1.1f,
            8_000_000f, 1f, 37f, 0.98f, 0.8f, "balance_receptors", articulation));

        var result = runtime.Transduce(descriptor, tick: 6, timestampMs: 6);

        Assert.Contains(result.ProprioceptiveLeft, spike =>
            spike.SourceNeuronId.Contains("support_margin_loss", StringComparison.Ordinal));
        Assert.Contains(result.ProprioceptiveRight, spike =>
            spike.SourceNeuronId.Contains("center_of_mass_right_of_pressure", StringComparison.Ordinal));
        Assert.Contains(result.VestibularLeft, spike =>
            spike.SourceNeuronId.Contains("otolith_pitch_forward", StringComparison.Ordinal));
        Assert.Contains(result.VestibularRight, spike =>
            spike.SourceNeuronId.Contains("otolith_roll_left", StringComparison.Ordinal));
        Assert.Contains(result.VestibularRight, spike =>
            spike.SourceNeuronId.Contains("dynamic_balance_margin_loss", StringComparison.Ordinal));
    }

    [Fact]
    public void NonPhysicalArticulationIsRejectedBeforeNeuralTransduction()
    {
        var articulation = PhysicalArticulationFrame.Neutral with
        {
            LeftHandLoadNewtons = -1f
        };

        var valid = PhysicalBodyFrameDescriptor.TryCreate(
            new PhysicalBodyFrameRequest(
                4, 1_300, 0f, 0f, 0f, 0f, 0f, 0f,
                8_000_000f, 1f, 37f, 0.98f, 0.8f, "test", articulation),
            out var descriptor,
            out var error);

        Assert.False(valid);
        Assert.Null(descriptor);
        Assert.Contains("articulation", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TissueLossAndPhysiologicalRestorationUseOppositeTeachingPathways()
    {
        var runtime = new PhysicalBodyTransducerRuntime();
        runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            1, 1_000, 0f, 0f, 0f, 0f, 0f, 0f,
            4_000_000f, 0.90f, 37f, 0.98f, 0.60f, "teaching_body")), 1, 1);

        var injured = runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            2, 1_100, 0f, 0f, 0f, 0f, 0f, 0f,
            4_000_000f, 0.25f, 37f, 0.98f, 0.60f, "teaching_body")), 2, 2);

        Assert.True(injured.NegativeTeachingSignal > 0.5f);
        Assert.NotEmpty(injured.For(StructureId.Habenula, null));
        Assert.Empty(injured.For(StructureId.Vta, null));
        Assert.Empty(injured.For(StructureId.Snc, null));

        var restored = runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            3, 1_200, 0f, 0f, 0f, 0f, 0f, 0f,
            7_000_000f, 0.65f, 37f, 0.98f, 0.82f, "teaching_body")), 3, 3);

        Assert.True(restored.PositiveTeachingSignal > 0.2f);
        Assert.Empty(restored.For(StructureId.Habenula, null));
        Assert.NotEmpty(restored.For(StructureId.Vta, null));
        Assert.NotEmpty(restored.For(StructureId.Snc, null));
        Assert.All(restored.VtaTeaching, spike => Assert.Equal(NTEnum.GLUTAMATE, spike.Neurotransmitter));
    }

    [Fact]
    public void RespawnAfterTerminalIntegrityIsNotAnAppetitiveOutcome()
    {
        var runtime = new PhysicalBodyTransducerRuntime();
        runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            1, 1_000, 0f, 0f, 0f, 0f, 0f, 0f,
            200_000f, 0f, 37f, 0.98f, 0.2f, "respawn_body")), 1, 1);

        var respawn = runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            2, 1_050, 0f, 0f, 0f, 0f, 0f, 0f,
            8_000_000f, 1f, 37f, 0.98f, 0.8f, "respawn_body")), 2, 2);

        Assert.Equal(0f, respawn.PositiveTeachingSignal);
        Assert.Empty(respawn.VtaTeaching);
        Assert.Empty(respawn.SncTeaching);
    }

    private static PhysicalBodyFrameDescriptor CreateDescriptor(PhysicalBodyFrameRequest request)
    {
        Assert.True(PhysicalBodyFrameDescriptor.TryCreate(request, out var descriptor, out var error), error);
        return Assert.IsType<PhysicalBodyFrameDescriptor>(descriptor);
    }

    private static void AssertFixedAfferentSpikes(
        IReadOnlyList<SpikeMessage> spikes,
        StructureId expectedStructure,
        string expectedHemisphere)
    {
        Assert.NotEmpty(spikes);
        Assert.All(spikes, spike =>
        {
            Assert.Equal(expectedStructure, spike.SourceStructure);
            Assert.Equal(expectedStructure, spike.TargetStructure);
            Assert.StartsWith($"{expectedHemisphere}:", spike.SourceNeuronId, StringComparison.Ordinal);
            Assert.NotEqual(Guid.Empty, spike.SynapseId);
            Assert.False(spike.IsFeedback);
            Assert.Null(spike.ModulationContext);
        });
    }
}
