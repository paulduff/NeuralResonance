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
        var homeostaticResult = runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            3, 1_250, 0.4f, 0f, 3f, 0f, 1.2f, 0f,
            2_000_000f, 0.72f, 39f, 0.82f, 0.54f, "test_body")), tick: 3, timestampMs: 3);

        Assert.True(result.ActiveProprioceptivePopulations > 0);
        Assert.True(result.ActiveVestibularPopulations > 0);
        Assert.True(result.ActiveVisceralPopulations > 0);
        Assert.True(result.LinearAccelerationMagnitude > 0f);
        Assert.True(result.HomeostaticDeviation > 0f);

        AssertFixedAfferentSpikes(result.ProprioceptiveLeft, StructureId.ProprioceptiveAfferents, "L");
        AssertFixedAfferentSpikes(result.VestibularRight, StructureId.VestibularAfferents, "R");
        Assert.Empty(result.VisceralLeft);
        AssertFixedAfferentSpikes(
            homeostaticResult.VisceralLeft,
            StructureId.VisceralAfferents,
            "L");
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
            TrunkYawRadians = 0.42f,
            RightHipAngleRadians = 0.72f,
            RightHipAbductionRadians = 0.52f,
            RightAnkleRollRadians = -0.18f,
            RightFootLoadNewtons = 640f,
            RightFootPressure = new PhysicalFootPressureFrame(80f, 40f, 420f, 100f),
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
            spike.SourceNeuronId.Contains("right_hip_abductor_spindle", StringComparison.Ordinal));
        Assert.Contains(result.ProprioceptiveLeft, spike =>
            spike.SourceNeuronId.Contains("right_foot_golgi_load", StringComparison.Ordinal));
        Assert.Contains(result.ProprioceptiveLeft, spike =>
            spike.SourceNeuronId.Contains("right_ankle_evertor_spindle", StringComparison.Ordinal));
        Assert.Contains(result.ProprioceptiveLeft, spike =>
            spike.SourceNeuronId.Contains("right_forefoot_medial_plantar_pressure", StringComparison.Ordinal));
        Assert.Contains(result.ProprioceptiveLeft, spike =>
            spike.SourceNeuronId.Contains("right_hand_golgi_load", StringComparison.Ordinal));
        Assert.Contains(result.ProprioceptiveLeft, spike =>
            spike.SourceNeuronId.Contains("axial_yaw_right_rotator_spindle", StringComparison.Ordinal));
        Assert.Contains(result.ProprioceptiveRight, spike =>
            spike.SourceNeuronId.Contains("axial_yaw_right_rotator_spindle", StringComparison.Ordinal));
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
        Assert.DoesNotContain(result.VestibularRight, spike =>
            spike.SourceNeuronId.Contains("margin", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ControlledDynamicMarginDoesNotRecruitEmergencyRightingAfferents()
    {
        var balance = PhysicalBalanceStateFrame.Neutral with
        {
            SupportAreaSquareMeters = 0.04f,
            SupportMarginMeters = -0.03f,
            DynamicStabilityAllowanceMeters = 0.075f,
            Phase = "dynamic"
        };
        var articulation = PhysicalArticulationFrame.Neutral with
        {
            Musculoskeletal = MusculoskeletalStateFrame.Neutral with { Balance = balance }
        };
        var runtime = new PhysicalBodyTransducerRuntime();
        var descriptor = CreateDescriptor(new PhysicalBodyFrameRequest(
            7, 1_600, 0f, 0f, 0f, 0f, 0f, 0f,
            8_000_000f, 1f, 37f, 0.98f, 0.8f, "dynamic_gait", articulation));

        var result = runtime.Transduce(descriptor, tick: 7, timestampMs: 7);

        Assert.Contains(result.ProprioceptiveLeft, spike =>
            spike.SourceNeuronId.Contains("dynamic_stability_reserve", StringComparison.Ordinal));
        Assert.DoesNotContain(result.ProprioceptiveLeft, spike =>
            spike.SourceNeuronId.Contains("support_margin_loss", StringComparison.Ordinal));
        Assert.DoesNotContain(result.VestibularLeft, spike =>
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
        Assert.Contains("leftHandLoadNewtons", error, StringComparison.Ordinal);
        Assert.Contains("-1", error, StringComparison.Ordinal);
    }

    [Fact]
    public void NonFiniteHipAbductionIsRejectedBeforeNeuralTransduction()
    {
        var articulation = PhysicalArticulationFrame.Neutral with
        {
            LeftHipAbductionRadians = float.NaN
        };

        var valid = PhysicalBodyFrameDescriptor.TryCreate(
            new PhysicalBodyFrameRequest(
                4, 1_300, 0f, 0f, 0f, 0f, 0f, 0f,
                8_000_000f, 1f, 37f, 0.98f, 0.8f, "test", articulation),
            out var descriptor,
            out var error);

        Assert.False(valid);
        Assert.Null(descriptor);
        Assert.Contains("leftHipAbductionRadians", error, StringComparison.Ordinal);
        Assert.Contains("finite", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SaturatedMuscleVelocityPreservesTheRestOfThePhysicalFrame()
    {
        var articulation = PhysicalArticulationFrame.Neutral with
        {
            Musculoskeletal = MusculoskeletalStateFrame.Neutral with
            {
                Muscles =
                [
                    new PhysicalMuscleMeasurement(
                        "Quadriceps", "L", 0.4f, 900f, 1f, 51f, 0f)
                ]
            }
        };

        var valid = PhysicalBodyFrameDescriptor.TryCreate(
            new PhysicalBodyFrameRequest(
                5, 1_400, 0f, 0f, 0f, 0f, 0f, 0f,
                8_000_000f, 1f, 37f, 0.98f, 0.8f, "test", articulation),
            out var descriptor,
            out var error);

        Assert.True(valid, error);
        var accepted = Assert.IsType<PhysicalBodyFrameDescriptor>(descriptor);
        Assert.Equal(1, accepted.SaturatedMuscleVelocityCount);
        Assert.Equal(50f, Assert.Single(accepted.Articulation.Musculoskeletal!.Muscles).VelocityPerSecond);
    }

    [Fact]
    public void SustainedForceWithoutMotionProducesNeuronalAversiveTeachingEvidence()
    {
        var runtime = new PhysicalBodyTransducerRuntime();
        var baseline = PhysicalArticulationFrame.Neutral with
        {
            Musculoskeletal = MusculoskeletalStateFrame.Neutral with
            {
                Balance = PhysicalBalanceStateFrame.Neutral with { SupportMarginMeters = -0.10f }
            }
        };
        runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            1, 1_000, 0f, 0f, 0f, 0f, 0f, 0f,
            8_000_000f, 1f, 37f, 0.98f, 0.8f, "motor_training", baseline,
            MotorTrainingMode: true)), 1, 1);
        var loaded = baseline with { LeftHandLoadNewtons = 280f, RightHandLoadNewtons = 260f };

        var result = runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            2, 1_100, 0f, 0f, 0f, 0f, 0f, 0f,
            8_000_000f, 1f, 37f, 0.98f, 0.8f, "motor_training", loaded,
            MotorTrainingMode: true)), 2, 2);

        Assert.True(result.IneffectiveForceEvidence > 0.80f);
        Assert.True(result.NegativeTeachingSignal > 0.20f);
        Assert.Empty(result.HabenularTeaching);
        Assert.Empty(result.SncTeaching);

        var dispatch = runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            3, 1_250, 0f, 0f, 0f, 0f, 0f, 0f,
            8_000_000f, 1f, 37f, 0.98f, 0.8f, "motor_training", loaded,
            MotorTrainingMode: true)), 3, 3);
        Assert.NotEmpty(dispatch.HabenularTeaching);
    }

    [Fact]
    public void StableStaticHandBraceDoesNotEarnSupportTeaching()
    {
        var runtime = new PhysicalBodyTransducerRuntime();
        var previousBalance = PhysicalBalanceStateFrame.Neutral with
        {
            SupportAreaSquareMeters = 0.18f,
            SupportMarginMeters = 0.08f,
            Phase = "stable"
        };
        var previous = PhysicalArticulationFrame.Neutral with
        {
            LeftHandLoadNewtons = 220f,
            Musculoskeletal = MusculoskeletalStateFrame.Neutral with
            {
                BalanceError = 0.08f,
                Balance = previousBalance
            }
        };
        runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            1, 1_000, 0f, 0f, 0f, 0f, 0f, 0f,
            8_000_000f, 1f, 37f, 0.98f, 0.8f, "stable_brace", previous)), 1, 1);
        var settled = previous with
        {
            Musculoskeletal = previous.Musculoskeletal! with
            {
                BalanceError = 0.04f,
                Balance = previousBalance with { SupportMarginMeters = 0.14f }
            }
        };

        var result = runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            2, 1_100, 0f, 0f, 0f, 0f, 0f, 0f,
            8_000_000f, 1f, 37f, 0.98f, 0.8f, "stable_brace", settled)), 2, 2);

        Assert.True(result.SupportMarginImprovement > 0f);
        Assert.True(result.BalanceImprovement > 0f);
        Assert.Equal(0f, result.PositiveTeachingSignal);
        Assert.Empty(result.VtaTeaching);
        Assert.Empty(result.SncTeaching);
    }

    [Fact]
    public void PassiveStandingLoadDoesNotTeachIneffectiveForce()
    {
        var runtime = new PhysicalBodyTransducerRuntime();
        var standing = PhysicalArticulationFrame.Neutral with
        {
            LeftFootLoadNewtons = 360f,
            RightFootLoadNewtons = 360f
        };

        runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            1, 1_000, 0f, 0f, 0f, 0f, 0f, 0f,
            8_000_000f, 1f, 37f, 0.98f, 0.8f, "passive_standing", standing,
            MotorTrainingMode: true)), 1, 1);

        var result = runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            2, 1_100, 0f, 0f, 0f, 0f, 0f, 0f,
            8_000_000f, 1f, 37f, 0.98f, 0.8f, "passive_standing", standing,
            MotorTrainingMode: true)), 2, 2);

        Assert.Equal(0f, result.IneffectiveForceEvidence);
    }

    [Fact]
    public void FallingBodyUsesLocalBalanceEvidenceWithoutGlobalAppetitiveTeaching()
    {
        var runtime = new PhysicalBodyTransducerRuntime();
        var falling = PhysicalArticulationFrame.Neutral with
        {
            Musculoskeletal = MusculoskeletalStateFrame.Neutral with
            {
                BalanceError = 0.82f,
                Balance = PhysicalBalanceStateFrame.Neutral with
                {
                    SupportMarginMeters = -0.12f,
                    Phase = "falling"
                }
            }
        };
        runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            1, 1_000, 0f, 0f, 0f, 0f, 0f, 0f,
            8_000_000f, 1f, 37f, 0.98f, 0.8f, "recovery_brace", falling)), 1, 1);
        var braced = falling with
        {
            LeftHandLoadNewtons = 240f,
            Musculoskeletal = falling.Musculoskeletal! with
            {
                BalanceError = 0.22f,
                Balance = falling.Musculoskeletal!.Balance! with
                {
                    SupportMarginMeters = 0.04f,
                    Phase = "righting"
                }
            }
        };

        var result = runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            2, 1_100, 0f, 0f, 0f, 0f, 0f, 0f,
            8_000_000f, 1f, 37f, 0.98f, 0.8f, "recovery_brace", braced)), 2, 2);

        Assert.True(result.SupportMarginImprovement > 0f);
        Assert.True(result.BalanceImprovement > 0f);
        Assert.Equal(0f, result.PositiveTeachingSignal);
        Assert.Empty(result.VtaTeaching);
        Assert.Empty(result.SncTeaching);
    }

    [Fact]
    public void MotionAloneDoesNotCreateGlobalAppetitiveTeaching()
    {
        var runtime = new PhysicalBodyTransducerRuntime();
        runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            1, 1_000, 0f, 0f, 0f, 0f, 0f, 0f,
            8_000_000f, 1f, 37f, 0.98f, 0.8f, "motor_training",
            MotorTrainingMode: true)), 1, 1);

        var result = runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            2, 1_100, 1.2f, 0f, 0f, 0f, 0f, 0f,
            8_000_000f, 1f, 37f, 0.98f, 0.8f, "motor_training",
            MotorTrainingMode: true)), 2, 2);

        Assert.True(result.MotionMagnitude > 0f);
        Assert.Equal(0f, result.PositiveTeachingSignal);
        Assert.Empty(result.VtaTeaching);
        Assert.Empty(result.SncTeaching);
    }

    [Fact]
    public void FatiguedMuscleProducesAnatomicallySidedPainAndNegativeTeaching()
    {
        var articulation = PhysicalArticulationFrame.Neutral with
        {
            Musculoskeletal = MusculoskeletalStateFrame.Neutral with
            {
                Muscles =
                [
                    new PhysicalMuscleMeasurement(
                        "AnteriorDeltoid", "R", 0.95f, 120f, 1f, 0f, 0.92f)
                ]
            }
        };
        var runtime = new PhysicalBodyTransducerRuntime();

        var result = runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            1, 1_000, 0f, 0f, 0f, 0f, 0f, 0f,
            8_000_000f, 1f, 37f, 0.98f, 0.8f, "fatigued_arm", articulation)), 1, 1);

        Assert.True(result.PeakMuscleFatigueDistress > 0.70f);
        Assert.True(result.HomeostaticDeviation > 0.80f);
        Assert.True(result.NegativeTeachingSignal > 0.10f);
        Assert.Contains(result.SomaticLeft, spike =>
            spike.SourceNeuronId.Contains(
                "arm:group_iii_iv_muscle_nociceptor:r_anteriordeltoid",
                StringComparison.Ordinal));
        Assert.Empty(result.SomaticRight);
        Assert.Contains(result.VisceralLeft, spike =>
            spike.SourceNeuronId.Contains(
                "muscle_metabolic_fatigue_interoceptor",
                StringComparison.Ordinal));
        Assert.Contains(result.VisceralRight, spike =>
            spike.SourceNeuronId.Contains(
                "muscle_metabolic_fatigue_interoceptor",
                StringComparison.Ordinal));
        Assert.NotEmpty(result.HabenularTeaching);
    }

    [Theory]
    [InlineData("GluteusMedius")]
    [InlineData("AdductorGroup")]
    public void FatiguedCoronalHipMuscleProducesLocalHipPain(string muscleName)
    {
        var articulation = PhysicalArticulationFrame.Neutral with
        {
            Musculoskeletal = MusculoskeletalStateFrame.Neutral with
            {
                Muscles =
                [
                    new PhysicalMuscleMeasurement(
                        muscleName, "L", 0.72f, 90f, 1f, 0f, 0.88f)
                ]
            }
        };
        var runtime = new PhysicalBodyTransducerRuntime();

        var result = runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            1, 1_000, 0f, 0f, 0f, 0f, 0f, 0f,
            8_000_000f, 1f, 37f, 0.98f, 0.8f, "fatigued_hip", articulation)), 1, 1);

        Assert.Contains(result.SomaticRight, spike =>
            spike.SourceNeuronId.Contains(
                $"hip:group_iii_iv_muscle_nociceptor:l_{muscleName.ToLowerInvariant()}",
                StringComparison.Ordinal));
        Assert.Empty(result.SomaticLeft);
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
        Assert.False(injured.HomeostaticCadenceDispatch);
        Assert.Empty(injured.For(StructureId.Habenula, null));
        Assert.Empty(injured.For(StructureId.Vta, null));
        Assert.Empty(injured.For(StructureId.Snc, null));

        var injuryDispatch = runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            3, 1_250, 0f, 0f, 0f, 0f, 0f, 0f,
            4_000_000f, 0.25f, 37f, 0.98f, 0.60f, "teaching_body")), 3, 3);
        Assert.True(injuryDispatch.HomeostaticCadenceDispatch);
        Assert.NotEmpty(injuryDispatch.For(StructureId.Habenula, null));

        var restored = runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            4, 1_350, 0f, 0f, 0f, 0f, 0f, 0f,
            7_000_000f, 0.65f, 37f, 0.98f, 0.82f, "teaching_body")), 4, 4);

        Assert.True(restored.PositiveTeachingSignal > 0.2f);
        Assert.Empty(restored.For(StructureId.Habenula, null));
        Assert.Empty(restored.For(StructureId.Vta, null));
        Assert.Empty(restored.For(StructureId.Snc, null));

        var restorationDispatch = runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            5, 1_500, 0f, 0f, 0f, 0f, 0f, 0f,
            7_000_000f, 0.65f, 37f, 0.98f, 0.82f, "teaching_body")), 5, 5);
        Assert.True(restorationDispatch.HomeostaticCadenceDispatch);
        Assert.NotEmpty(restorationDispatch.For(StructureId.Vta, null));
        Assert.NotEmpty(restorationDispatch.For(StructureId.Snc, null));
        Assert.All(restorationDispatch.VtaTeaching, spike =>
            Assert.Equal(NTEnum.GLUTAMATE, spike.Neurotransmitter));
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

    [Fact]
    public void HungerAndThirstRecruitDistinctVisceralNeedPopulations()
    {
        var runtime = new PhysicalBodyTransducerRuntime();

        var result = runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            1, 1_000, 0f, 0f, 0f, 0f, 0f, 0f,
            2_000_000f, 1f, 37f, 0.98f, 0.30f, "need_state_body")), 1, 1);

        Assert.True(result.HungerDrive > 0.70f);
        Assert.True(result.ThirstDrive > 0.90f);
        Assert.Contains(result.VisceralLeft, spike =>
            spike.SourceNeuronId.Contains("arcuate_agrp_npy_hunger_drive", StringComparison.Ordinal));
        Assert.Contains(result.VisceralRight, spike =>
            spike.SourceNeuronId.Contains("lamina_terminalis_osmotic_thirst_drive", StringComparison.Ordinal));
    }

    [Fact]
    public void EarnedFoodAndWaterRestorationProduceNeedWeightedTeachingPopulations()
    {
        var runtime = new PhysicalBodyTransducerRuntime();
        runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            1, 1_000, 0f, 0f, 0f, 0f, 0f, 0f,
            2_000_000f, 1f, 37f, 0.98f, 0.30f, "consummatory_body")), 1, 1);

        var restored = runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            2, 1_100, 0f, 0f, 0f, 0f, 0f, 0f,
            3_280_000f, 1f, 37f, 0.98f, 0.40f, "consummatory_body")), 2, 2);

        Assert.True(restored.EnergyRestorationTeachingSignal > 0.20f);
        Assert.True(restored.HydrationRestorationTeachingSignal > 0.15f);
        Assert.Empty(restored.VtaTeaching);

        var dispatch = runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            3, 1_250, 0f, 0f, 0f, 0f, 0f, 0f,
            3_280_000f, 1f, 37f, 0.98f, 0.40f, "consummatory_body")), 3, 3);
        Assert.Contains(dispatch.VtaTeaching, spike =>
            spike.SourceNeuronId.Contains("need_weighted_energy_restoration", StringComparison.Ordinal));
        Assert.Contains(dispatch.VtaTeaching, spike =>
            spike.SourceNeuronId.Contains("need_weighted_hydration_restoration", StringComparison.Ordinal));
    }

    [Fact]
    public void FastSensorimotorAfferenceContinuesWhileHomeostaticLaneIsBuffered()
    {
        var runtime = new PhysicalBodyTransducerRuntime();
        var first = runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            1, 1_000, 0f, 0f, 0.4f, 0f, 0f, 0f,
            2_000_000f, 1f, 37f, 0.98f, 0.30f, "cadence_body")), 1, 1);
        var fastFrame = runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
            2, 1_050, 0f, 0f, 0.8f, 0f, 0.2f, 0f,
            2_000_000f, 1f, 37f, 0.98f, 0.30f, "cadence_body")), 2, 2);

        Assert.True(first.HomeostaticCadenceDispatch);
        Assert.False(fastFrame.HomeostaticCadenceDispatch);
        Assert.Equal(250, fastFrame.HomeostaticCadenceMilliseconds);
        Assert.NotEmpty(fastFrame.ProprioceptiveLeft);
        Assert.NotEmpty(fastFrame.VestibularLeft);
        Assert.Empty(fastFrame.VisceralLeft);
        Assert.Empty(fastFrame.HabenularTeaching);
        Assert.Empty(fastFrame.VtaTeaching);
        Assert.Empty(fastFrame.SncTeaching);
    }

    [Fact]
    public void TeachingTelemetryReportsHomeostaticCadenceWithoutHidingFastFrames()
    {
        var runtime = new PhysicalBodyTransducerRuntime();
        var telemetry = new TeachingTelemetryAccumulator();
        var timestamps = new long[] { 1_000, 1_050, 1_250 };

        for (var index = 0; index < timestamps.Length; index++)
        {
            var tick = index + 1;
            var transduction = runtime.Transduce(CreateDescriptor(new PhysicalBodyFrameRequest(
                tick, timestamps[index], 0f, 0f, 0.4f, 0f, 0.1f, 0f,
                2_000_000f, 1f, 37f, 0.98f, 0.30f, "telemetry_cadence_body")), tick, tick);
            telemetry.Observe(tick, transduction);
        }

        var snapshot = telemetry.GetSnapshot();
        Assert.Equal(3, snapshot.PhysicalFramesObserved);
        Assert.Equal(2, snapshot.HomeostaticDispatches);
        Assert.Equal(1, snapshot.HomeostaticFramesBuffered);
        Assert.Equal(250, snapshot.HomeostaticCadenceMilliseconds);
        Assert.Equal(3, snapshot.LastHomeostaticDispatchTick);
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
