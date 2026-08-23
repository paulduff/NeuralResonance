using System.Numerics;
using NRE.SimAvatar;
using NRE.WorldSim;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AvatarArticulatedBodyTests
{
    [Theory]
    [InlineData("broad_support", AvatarBalancePhase.BroadSupport)]
    [InlineData("broad-support", AvatarBalancePhase.BroadSupport)]
    [InlineData("Broad Support", AvatarBalancePhase.BroadSupport)]
    [InlineData("righting", AvatarBalancePhase.Righting)]
    public void BalancePhaseWireNamesRoundTrip(string value, AvatarBalancePhase expected)
    {
        Assert.True(AvatarArticulatedBody.TryParseBalancePhase(value, out var actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CollisionResolvedPoseIsReconciledIntoTheMusclePlant()
    {
        var body = new AvatarArticulatedBody();
        var previous = body.CaptureFrame();
        for (var step = 0; step < 80; step++)
        {
            body.Advance(
                0.025,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                grounded: true,
                movementBlocked: true,
                leftShoulderSagittalDrive: 1.0);
        }

        var rejected = body.CaptureFrame();
        Assert.True(rejected.LeftShoulderAngleRadians > 0.5f);
        var resolved = rejected with { LeftShoulderAngleRadians = 0.16f };

        body.ReconcileResolvedFrame(previous, rejected, resolved, 0.025);

        var reconciled = body.CaptureFrame();
        Assert.Equal(0.16f, reconciled.LeftShoulderAngleRadians, 4);
        Assert.Contains(reconciled.Musculoskeletal!.Muscles, muscle =>
            muscle.Name == "AnteriorDeltoid" && Math.Abs(muscle.VelocityPerSecond) < 0.0001f);

        body.Advance(0.025, 0.0, 0.0, 0.0, 0.0, 0.0, true, true);
        var next = body.CaptureFrame();
        Assert.InRange(next.LeftShoulderAngleRadians, 0.0f, 0.30f);
    }

    [Fact]
    public void GroundedBlockedLocomotorDriveProducesJointMotionAndMechanicalLoad()
    {
        var body = new AvatarArticulatedBody();

        for (var step = 0; step < 12; step++)
        {
            body.Advance(
                deltaSeconds: 0.05,
                leftMotorDrive: 0.9,
                rightMotorDrive: 0.7,
                achievedForwardSpeed: 0.0,
                turnRateDegrees: 0.0,
                manipulatorDrive: 0.0,
                grounded: true,
                movementBlocked: true);
        }

        var frame = body.CaptureFrame();
        Assert.NotEqual(0f, frame.LeftHipAngleRadians);
        Assert.NotEqual(frame.LeftHipAngleRadians, frame.RightHipAngleRadians);
        Assert.NotEqual(0f, frame.LeftShoulderAngleRadians);
        Assert.NotEqual(frame.LeftShoulderAngleRadians, frame.RightShoulderAngleRadians);
        Assert.True(frame.LeftFootLoadNewtons + frame.RightFootLoadNewtons > 720f);
        Assert.Equal(0f, frame.LeftHandLoadNewtons);
        Assert.Equal(0f, frame.RightHandLoadNewtons);
    }

    [Fact]
    public void ManipulatorExtensionDoesNotInventContactAndPhysicalLoadIsLateralized()
    {
        var body = new AvatarArticulatedBody();
        for (var step = 0; step < 4; step++)
        {
            body.Advance(
                0.20,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                true,
                false,
                leftShoulderSagittalDrive: 1.0);
        }

        var extended = body.CaptureFrame();
        Assert.True(extended.ManipulatorExtensionFraction > 0.9f);
        Assert.Equal(0f, extended.LeftHandLoadNewtons);
        Assert.Equal(0f, extended.RightHandLoadNewtons);

        body.ApplyManipulatorContact(loadNewtons: 120.0, bodyLocalLateralDirection: 1.0);
        var contacted = body.CaptureFrame();
        Assert.True(contacted.RightHandLoadNewtons > contacted.LeftHandLoadNewtons);
        Assert.InRange(
            contacted.LeftHandLoadNewtons + contacted.RightHandLoadNewtons,
            119.9f,
            120.1f);
    }

    [Fact]
    public void ResetReturnsTheMechanicalBodyToNeutralMeasurements()
    {
        var body = new AvatarArticulatedBody();
        body.Advance(0.20, 1.0, -0.8, 1.2, 60.0, 1.0, true, true);
        body.ApplyManipulatorContact(250.0, -0.8);

        body.Reset();

        var frame = body.CaptureFrame();
        Assert.Equal(0f, frame.LeftHipAngleRadians);
        Assert.Equal(0f, frame.RightHipAngleRadians);
        Assert.Equal("standing", frame.Musculoskeletal?.Posture);
        Assert.All(frame.Musculoskeletal?.Muscles ?? [], muscle => Assert.Equal(0f, muscle.Activation));
    }

    [Fact]
    public void LimbJointsRemainInsideAnatomicalHardStops()
    {
        var body = new AvatarArticulatedBody();

        for (var step = 0; step < 500; step++)
        {
            var phaseDrive = step % 2 == 0 ? 1.0 : -1.0;
            body.Advance(0.25, phaseDrive, -phaseDrive, 1.8, 180.0, 1.0, true, false);
            var frame = body.CaptureFrame();

            Assert.InRange(frame.LeftShoulderAngleRadians, -0.70f, 2.62f);
            Assert.InRange(frame.RightShoulderAngleRadians, -0.70f, 2.62f);
            Assert.InRange(frame.LeftElbowAngleRadians, 0f, 2.62f);
            Assert.InRange(frame.RightElbowAngleRadians, 0f, 2.62f);
            Assert.InRange(frame.LeftHipAngleRadians, -0.35f, 2.09f);
            Assert.InRange(frame.RightHipAngleRadians, -0.35f, 2.09f);
            Assert.InRange(frame.LeftHipAbductionRadians, -0.45f, 0.78f);
            Assert.InRange(frame.RightHipAbductionRadians, -0.45f, 0.78f);
            Assert.InRange(frame.LeftKneeAngleRadians, 0f, 2.45f);
            Assert.InRange(frame.RightKneeAngleRadians, 0f, 2.45f);
            Assert.InRange(frame.LeftAnkleAngleRadians, -0.78f, 0.52f);
            Assert.InRange(frame.RightAnkleAngleRadians, -0.78f, 0.52f);
            Assert.InRange(frame.LeftAnkleRollRadians, -0.26f, 0.52f);
            Assert.InRange(frame.RightAnkleRollRadians, -0.26f, 0.52f);
        }
    }

    [Fact]
    public void LocomotorDriveRecruitsAntagonisticMusclesAndProducesProprioception()
    {
        var body = new AvatarArticulatedBody();
        AvatarMechanicalOutput output = default;
        for (var step = 0; step < 30; step++)
        {
            output = body.Advance(0.033, 0.9, 0.8, 1.6, 12.0, 0.0, true, false);
        }

        var frame = body.CaptureFrame();
        var muscles = Assert.IsAssignableFrom<IReadOnlyList<PhysicalMuscleMeasurement>>(
            frame.Musculoskeletal?.Muscles);
        Assert.Equal(42, muscles.Count);
        Assert.Contains(muscles, muscle => muscle.Name == "Quadriceps" && muscle.Activation > 0f);
        Assert.Contains(muscles, muscle => muscle.Name == "GastrocnemiusSoleus" && muscle.ForceNewtons > 0f);
        Assert.True(output.ForwardSpeedMetersPerSecond > 0.0);
        Assert.True(output.SupportFraction > 0.9);
    }

    [Fact]
    public void LocomotorDriveProducesAlternatingStanceAndSwingInsteadOfAShuffle()
    {
        var body = new AvatarArticulatedBody();
        var observedLeftSwing = false;
        var observedRightSwing = false;
        var movingSamples = 0;

        for (var step = 0; step < 300; step++)
        {
            var output = body.Advance(0.025, 0.9, 0.9, 1.4, 0.0, 0.0, true, false);
            var frame = body.CaptureFrame();
            var colliders = AvatarColliderRig.CaptureResolved(frame);
            var leftFoot = Assert.Single(colliders, collider => collider.Region == "left_foot");
            var rightFoot = Assert.Single(colliders, collider => collider.Region == "right_foot");
            var leftClearance = AvatarColliderRig.LowestSurfaceY(leftFoot) -
                AvatarColliderRig.LowestSurfaceY(rightFoot);
            var rightClearance = -leftClearance;

            observedLeftSwing |= frame.LeftKneeAngleRadians > frame.RightKneeAngleRadians + 0.15f &&
                leftClearance > 0.015f &&
                frame.LeftFootLoadNewtons < frame.RightFootLoadNewtons;
            observedRightSwing |= frame.RightKneeAngleRadians > frame.LeftKneeAngleRadians + 0.15f &&
                rightClearance > 0.015f &&
                frame.RightFootLoadNewtons < frame.LeftFootLoadNewtons;
            movingSamples += output.ForwardSpeedMetersPerSecond > 0.02 ? 1 : 0;

            Assert.DoesNotContain(
                frame.Musculoskeletal?.Balance?.Phase,
                new[] { "falling", "fallen" });
        }

        Assert.True(observedLeftSwing);
        Assert.True(observedRightSwing);
        Assert.True(movingSamples > 250);
    }

    [Fact]
    public void FatiguingLoadBearingMusclesCannotRetainHiddenRootLocomotion()
    {
        var body = new AvatarArticulatedBody();
        var earlyPeakSpeed = 0.0;
        var finalPeakSpeed = 0.0;

        for (var step = 0; step < 4_000; step++)
        {
            var output = body.Advance(
                0.025, 1.0, 1.0, 1.8, 0.0, 0.0, true, false,
                standDrive: 1.0);
            if (step is >= 40 and < 400)
            {
                earlyPeakSpeed = Math.Max(earlyPeakSpeed, output.ForwardSpeedMetersPerSecond);
            }
            if (step >= 3_900)
            {
                finalPeakSpeed = Math.Max(finalPeakSpeed, output.ForwardSpeedMetersPerSecond);
            }
        }

        var muscles = body.CaptureFrame().Musculoskeletal!.Muscles;
        Assert.True(earlyPeakSpeed > 0.05);
        Assert.Contains(muscles, muscle =>
            muscle.Name == "Iliopsoas" && muscle.FatigueFraction > 0.40f);
        Assert.True(finalPeakSpeed < earlyPeakSpeed * 0.55,
            $"Early peak {earlyPeakSpeed:0.000}, exhausted peak {finalPeakSpeed:0.000}.");
    }

    [Fact]
    public void LivePopulationRecruitmentEnvelopeProducesAUsableSwingPhase()
    {
        var body = new AvatarArticulatedBody();
        var recruitment = HeadlessWorldRuntime.NormalizeMotorRecruitment(48.0);
        var maximumClearance = 0f;
        var maximumAllowance = 0f;

        for (var step = 0; step < 300; step++)
        {
            body.Advance(
                0.025,
                recruitment,
                recruitment,
                achievedForwardSpeed: 0.288,
                turnRateDegrees: 0.0,
                manipulatorDrive: 0.0,
                grounded: true,
                movementBlocked: false);
            var frame = body.CaptureFrame();
            var colliders = AvatarColliderRig.CaptureResolved(frame);
            var leftFoot = Assert.Single(colliders, collider => collider.Region == "left_foot");
            var rightFoot = Assert.Single(colliders, collider => collider.Region == "right_foot");
            maximumClearance = Math.Max(
                maximumClearance,
                Math.Abs(
                    AvatarColliderRig.LowestSurfaceY(leftFoot) -
                    AvatarColliderRig.LowestSurfaceY(rightFoot)));
            maximumAllowance = Math.Max(
                maximumAllowance,
                frame.Musculoskeletal?.Balance?.DynamicStabilityAllowanceMeters ?? 0f);
        }

        Assert.True(maximumClearance > 0.015f);
        Assert.True(maximumAllowance > 0.03f);
    }

    [Fact]
    public void IndependentCoronalArmPopulationsRecruitOnlyTheRequestedShoulder()
    {
        var body = new AvatarArticulatedBody();
        for (var step = 0; step < 60; step++)
        {
            body.Advance(
                0.025, 0.0, 0.0, 0.0, 0.0, 1.0, true, false,
                leftShoulderCoronalDrive: 1.0);
        }

        var lifted = body.CaptureFrame();
        Assert.True(lifted.LeftShoulderAbductionRadians > 0.35f);
        Assert.InRange(lifted.RightShoulderAbductionRadians, -0.05f, 0.05f);
        Assert.Contains(lifted.Musculoskeletal!.Muscles,
            muscle => muscle.Name == "MiddleDeltoid" && muscle.Activation > 0.05f);

        for (var step = 0; step < 20; step++)
        {
            body.Advance(0.025, 0.0, 0.0, 0.0, 0.0, 0.0, true, false);
        }

        var lowering = body.CaptureFrame();
        Assert.True(lowering.LeftShoulderAbductionRadians < lifted.LeftShoulderAbductionRadians);
        Assert.Contains(lowering.Musculoskeletal!.Muscles,
            muscle => muscle.Name == "PectoralisMajor" && muscle.Activation > 0.01f);
    }

    [Fact]
    public void IndependentCoronalHipPopulationsRecruitAntagonisticLateralMuscles()
    {
        var body = new AvatarArticulatedBody();
        for (var step = 0; step < 80; step++)
        {
            body.Advance(
                0.025, 0.0, 0.0, 0.0, 0.0, 0.0, true, false,
                leftHipCoronalDrive: 1.0,
                rightHipCoronalDrive: -1.0);
        }

        var frame = body.CaptureFrame();
        Assert.True(frame.LeftHipAbductionRadians > 0.30f);
        Assert.True(frame.RightHipAbductionRadians < -0.15f);
        Assert.Contains(frame.Musculoskeletal!.Muscles,
            muscle => muscle.Name == "GluteusMedius" && muscle.Side == "L" && muscle.Activation > 0.05f);
        Assert.Contains(frame.Musculoskeletal.Muscles,
            muscle => muscle.Name == "AdductorGroup" && muscle.Side == "R" && muscle.Activation > 0.05f);
    }

    [Fact]
    public void SustainedWideStanceFatiguesLateralHipStabilizers()
    {
        var body = new AvatarArticulatedBody();
        var peakGluteForce = 0f;
        for (var step = 0; step < 4_800; step++)
        {
            body.Advance(
                0.025, 0.0, 0.0, 0.0, 0.0, 0.0, true, false,
                standDrive: 1.0,
                leftHipCoronalDrive: 1.0,
                rightHipCoronalDrive: 1.0);
            var current = body.CaptureFrame().Musculoskeletal!.Muscles.Single(
                muscle => muscle.Name == "GluteusMedius" && muscle.Side == "L");
            peakGluteForce = Math.Max(peakGluteForce, current.ForceNewtons);
        }

        var frame = body.CaptureFrame();
        var leftGlute = frame.Musculoskeletal!.Muscles.Single(
            muscle => muscle.Name == "GluteusMedius" && muscle.Side == "L");
        var leftAdductors = frame.Musculoskeletal.Muscles.Single(
            muscle => muscle.Name == "AdductorGroup" && muscle.Side == "L");

        Assert.True(leftGlute.FatigueFraction > 0.45f);
        Assert.True(leftAdductors.FatigueFraction > 0.05f);
        Assert.True(leftGlute.ForceNewtons < peakGluteForce * 0.50f);
    }

    [Fact]
    public void SustainedAdductionFatiguesTheAdductorGroupAndReducesItsForce()
    {
        var body = new AvatarArticulatedBody();
        var peakAdductorForce = 0f;
        for (var step = 0; step < 4_800; step++)
        {
            body.Advance(
                0.025, 0.0, 0.0, 0.0, 0.0, 0.0, true, false,
                standDrive: 1.0,
                leftHipCoronalDrive: -1.0,
                rightHipCoronalDrive: -1.0);
            var current = body.CaptureFrame().Musculoskeletal!.Muscles.Single(
                muscle => muscle.Name == "AdductorGroup" && muscle.Side == "L");
            peakAdductorForce = Math.Max(peakAdductorForce, current.ForceNewtons);
        }

        var adductors = body.CaptureFrame().Musculoskeletal!.Muscles.Single(
            muscle => muscle.Name == "AdductorGroup" && muscle.Side == "L");
        Assert.True(adductors.FatigueFraction > 0.45f);
        Assert.True(adductors.ForceNewtons < peakAdductorForce * 0.50f);
    }

    [Fact]
    public void RaisedAbductedLegCannotCarryGroundReactionLoad()
    {
        var body = new AvatarArticulatedBody();
        PhysicalArticulationFrame frame = PhysicalArticulationFrame.Neutral;
        var observedUnilateralSupport = false;

        for (var step = 0; step < 80; step++)
        {
            body.Advance(
                0.025, 0.0, 0.0, 0.0, 0.0, 0.0, true, false,
                leftHipCoronalDrive: 1.0);
            frame = body.CaptureFrame();
            if (frame.LeftFootLoadNewtons < 0.5f && frame.RightFootLoadNewtons > 700f)
            {
                observedUnilateralSupport = true;
                break;
            }
        }

        Assert.True(
            observedUnilateralSupport,
            $"Expected right-only support, observed L={frame.LeftFootLoadNewtons:0.0} N, " +
            $"R={frame.RightFootLoadNewtons:0.0} N at left abduction {frame.LeftHipAbductionRadians:0.000} rad.");
        var contacts = body.CaptureGroundContacts();
        Assert.DoesNotContain(contacts, contact =>
            contact.Region.StartsWith("left_foot", StringComparison.Ordinal));
        Assert.Contains(contacts, contact =>
            contact.Region.StartsWith("right_foot", StringComparison.Ordinal));
    }

    [Fact]
    public void IndependentTwoAxisAnklePopulationsRecruitAntagonistsAndRedistributeSoleLoad()
    {
        var body = new AvatarArticulatedBody();
        for (var step = 0; step < 90; step++)
        {
            body.Advance(
                0.025, 0.0, 0.0, 0.0, 0.0, 0.0, true, false,
                leftAnkleSagittalDrive: 1.0,
                rightAnkleSagittalDrive: -1.0,
                leftAnkleCoronalDrive: 1.0,
                rightAnkleCoronalDrive: -1.0);
        }

        var frame = body.CaptureFrame();
        Assert.True(frame.LeftAnkleAngleRadians > 0.20f);
        Assert.True(frame.RightAnkleAngleRadians < -0.20f);
        Assert.True(frame.LeftAnkleRollRadians > 0.15f);
        Assert.True(frame.RightAnkleRollRadians < -0.08f);
        Assert.Contains(frame.Musculoskeletal!.Muscles,
            muscle => muscle.Name == "TibialisPosterior" && muscle.Side == "L" && muscle.Activation > 0.05f);
        Assert.Contains(frame.Musculoskeletal.Muscles,
            muscle => muscle.Name == "FibularisLongusBrevis" && muscle.Side == "R" && muscle.Activation > 0.05f);

        var leftPressure = Assert.IsType<PhysicalFootPressureFrame>(frame.LeftFootPressure);
        var rightPressure = Assert.IsType<PhysicalFootPressureFrame>(frame.RightFootPressure);
        Assert.InRange(PressureTotal(leftPressure), frame.LeftFootLoadNewtons - 0.1f, frame.LeftFootLoadNewtons + 0.1f);
        Assert.InRange(PressureTotal(rightPressure), frame.RightFootLoadNewtons - 0.1f, frame.RightFootLoadNewtons + 0.1f);
        if (frame.LeftFootLoadNewtons > 0.5f)
        {
            Assert.True(leftPressure.HeelLateralLoadNewtons > leftPressure.ForefootMedialLoadNewtons);
        }
        if (frame.RightFootLoadNewtons > 0.5f)
        {
            Assert.True(rightPressure.ForefootMedialLoadNewtons > rightPressure.HeelLateralLoadNewtons);
        }
        Assert.True(frame.LeftFootLoadNewtons > 0.5f || frame.RightFootLoadNewtons > 0.5f);
        var contacts = body.CaptureGroundContacts();
        AssertFootContactMatchesLoad(contacts, "left_foot", frame.LeftFootLoadNewtons);
        AssertFootContactMatchesLoad(contacts, "right_foot", frame.RightFootLoadNewtons);
        var leftFoot = Assert.Single(
            AvatarColliderRig.CaptureResolved(frame),
            collider => collider.Region == "left_foot");
        Assert.NotEqual(0f, leftFoot.Orientation.Z);
    }

    [Fact]
    public void SustainedLoadedHandRecruitmentWithdrawsTheArmDespiteContinuedDescendingDrive()
    {
        var body = new AvatarArticulatedBody();
        for (var step = 0; step < 45; step++)
        {
            body.Advance(
                0.025, 0.0, 0.0, 0.0, 0.0, 1.0, true, false,
                rightShoulderCoronalDrive: 1.0);
        }

        var extended = body.CaptureFrame();
        Assert.True(extended.RightShoulderAbductionRadians > 0.30f);

        for (var step = 0; step < 70; step++)
        {
            body.ApplyManipulatorContact(loadNewtons: 300.0, bodyLocalLateralDirection: 1.0);
            body.Advance(
                0.025, 0.0, 0.0, 0.0, 0.0, 1.0, true, true,
                rightShoulderCoronalDrive: 1.0);
        }

        var withdrawn = body.CaptureFrame();
        Assert.True(withdrawn.RightShoulderAbductionRadians < extended.RightShoulderAbductionRadians * 0.70f);
    }

    [Theory]
    [InlineData("left_hand")]
    [InlineData("right_hand")]
    public void PhysicalHandContactPreservesExactAnatomicalSide(string region)
    {
        var body = new AvatarArticulatedBody();

        body.ApplyHandContact(region, 275.0);

        var frame = body.CaptureFrame();
        if (region == "left_hand")
        {
            Assert.Equal(275f, frame.LeftHandLoadNewtons);
            Assert.Equal(0f, frame.RightHandLoadNewtons);
        }
        else
        {
            Assert.Equal(0f, frame.LeftHandLoadNewtons);
            Assert.Equal(275f, frame.RightHandLoadNewtons);
        }
    }

    [Fact]
    public void SuperiorColliculusDrivesMoveNeckThroughAntagonisticMuscles()
    {
        var body = new AvatarArticulatedBody();
        for (var step = 0; step < 60; step++)
        {
            body.Advance(
                0.025, 0.0, 0.0, 0.0, 0.0, 0.0, true, false,
                headYawDrive: 0.75,
                headPitchDrive: -0.55);
        }

        var frame = body.CaptureFrame();
        Assert.True(frame.NeckYawRadians > 0.25f);
        Assert.True(frame.NeckPitchRadians < -0.15f);
        Assert.Contains(frame.Musculoskeletal!.Muscles,
            muscle => muscle.Name.Contains("SpleniusCapitis", StringComparison.Ordinal) && muscle.Activation > 0f);
    }

    [Fact]
    public void DescendingAxialDriveRotatesTrunkThroughReciprocalObliqueMuscles()
    {
        var body = new AvatarArticulatedBody();
        for (var step = 0; step < 80; step++)
        {
            body.Advance(
                0.025, 0.0, 0.0, 0.0, 0.0, 0.0, true, false,
                trunkYawDrive: 0.85);
        }

        var frame = body.CaptureFrame();
        Assert.True(frame.TrunkYawRadians > 0.20f);
        Assert.Contains(frame.Musculoskeletal!.Muscles,
            muscle => muscle.Name.Contains("RightExternalOblique", StringComparison.Ordinal) &&
                muscle.Activation > 0f);
    }

    [Theory]
    [InlineData(0.0, 1.0, 0.0, 0.0, "crouching")]
    [InlineData(0.0, 0.0, 1.0, 0.0, "crouching")]
    [InlineData(0.0, 0.0, 0.0, 1.0, "lying")]
    public void PostureDriveChangesWholeBodyConfiguration(
        double stand,
        double crouch,
        double sit,
        double lie,
        string expected)
    {
        var body = new AvatarArticulatedBody();
        for (var step = 0; step < 100; step++)
        {
            body.Advance(0.025, 0.0, 0.0, 0.0, 0.0, 0.0, true, false, stand, crouch, sit, lie);
        }

        var frame = body.CaptureFrame();
        Assert.Equal(expected, frame.Musculoskeletal?.Posture);
        Assert.True(frame.Musculoskeletal?.BodyHeightMeters < 1.5f);
    }

    [Fact]
    public void SitDriveCannotInventPelvicSupport()
    {
        var body = new AvatarArticulatedBody();
        for (var step = 0; step < 100; step++)
        {
            body.Advance(
                0.025, 0.0, 0.0, 0.0, 0.0, 0.0,
                grounded: true,
                movementBlocked: false,
                sitDrive: 1.0);
        }

        var frame = body.CaptureFrame();
        Assert.Equal("crouching", frame.Musculoskeletal?.Posture);
        Assert.DoesNotContain(body.CaptureGroundContacts(), contact =>
            contact.Region.StartsWith("pelvis", StringComparison.Ordinal));
    }

    [Fact]
    public void UpwardPelvicContactArrestsDescentAndPermitsSitting()
    {
        var body = new AvatarArticulatedBody();
        for (var step = 0; step < 100; step++)
        {
            body.ApplyExternalContact(new AvatarExternalBodyContact(
                "pelvis",
                new Vector3(0f, -0.30f, 0f),
                Vector3.UnitY,
                420.0,
                10.5,
                45_000.0));
            body.Advance(
                0.025, 0.0, 0.0, 0.0, 0.0, 0.0,
                grounded: true,
                movementBlocked: false,
                sitDrive: 1.0);
        }

        var frame = body.CaptureFrame();
        Assert.Equal("sitting", frame.Musculoskeletal?.Posture);
        Assert.Contains(body.CaptureGroundContacts(), contact =>
            contact.Region.StartsWith("pelvis", StringComparison.Ordinal));
    }

    [Fact]
    public void HorizontalPelvicContactCannotMasqueradeAsASeat()
    {
        var body = new AvatarArticulatedBody();
        for (var step = 0; step < 100; step++)
        {
            body.ApplyExternalContact(new AvatarExternalBodyContact(
                "pelvis",
                new Vector3(0f, -0.20f, 0.25f),
                Vector3.UnitZ,
                420.0,
                10.5,
                45_000.0));
            body.Advance(
                0.025, 0.0, 0.0, 0.0, 0.0, 0.0,
                grounded: true,
                movementBlocked: true,
                sitDrive: 1.0);
        }

        Assert.NotEqual("sitting", body.CaptureFrame().Musculoskeletal?.Posture);
    }

    [Fact]
    public void SeatedStateExpiresAfterPhysicalSupportIsRemoved()
    {
        var body = new AvatarArticulatedBody();
        body.ApplyExternalContact(new AvatarExternalBodyContact(
            "pelvis",
            new Vector3(0f, -0.30f, 0f),
            Vector3.UnitY,
            420.0,
            10.5,
            45_000.0));
        body.Advance(
            0.025, 0.0, 0.0, 0.0, 0.0, 0.0,
            grounded: true,
            movementBlocked: false,
            sitDrive: 1.0);
        Assert.Equal("sitting", body.CaptureFrame().Musculoskeletal?.Posture);

        for (var step = 0; step < 10; step++)
        {
            body.Advance(
                0.025, 0.0, 0.0, 0.0, 0.0, 0.0,
                grounded: true,
                movementBlocked: false,
                sitDrive: 1.0);
        }

        Assert.Equal("crouching", body.CaptureFrame().Musculoskeletal?.Posture);
    }

    [Fact]
    public void HipGaitUsesAnAsymmetricBiologicalWalkingEnvelope()
    {
        var body = new AvatarArticulatedBody();
        var minimumLeftHip = double.PositiveInfinity;
        var maximumLeftHip = double.NegativeInfinity;

        for (var step = 0; step < 320; step++)
        {
            body.Advance(0.025, 1.0, 1.0, 1.6, 0.0, 0.0, true, false);
            var angle = body.CaptureFrame().LeftHipAngleRadians;
            minimumLeftHip = Math.Min(minimumLeftHip, angle);
            maximumLeftHip = Math.Max(maximumLeftHip, angle);
        }

        Assert.InRange(minimumLeftHip, -0.351, -0.12);
        Assert.InRange(maximumLeftHip, 0.20, 0.55);
        Assert.True(maximumLeftHip > Math.Abs(minimumLeftHip),
            $"Expected walking flexion to exceed extension, observed {minimumLeftHip:0.000}..{maximumLeftHip:0.000} rad.");
    }

    [Fact]
    public void LyingTransitionLowersTheBodyContinuouslyAndTransfersGroundLoad()
    {
        var body = new AvatarArticulatedBody();
        body.Advance(0.05, 0.0, 0.0, 0.0, 0.0, 0.0, true, false, lieDrive: 1.0);

        var early = body.CaptureFrame();
        Assert.InRange(early.Musculoskeletal!.BodyHeightMeters, 1.68f, 1.74f);
        Assert.All(body.CaptureGroundContacts(), contact => Assert.Contains("foot", contact.Region));

        for (var step = 0; step < 100; step++)
        {
            body.Advance(0.025, 0.0, 0.0, 0.0, 0.0, 0.0, true, false, lieDrive: 1.0);
        }

        var settled = body.CaptureFrame();
        var contacts = body.CaptureGroundContacts();
        Assert.Equal("lying", settled.Musculoskeletal?.Posture);
        Assert.InRange(settled.Musculoskeletal!.BodyHeightMeters, 0.28f, 0.46f);
        Assert.Contains(contacts, contact => contact.Region == "pelvis");
        Assert.Contains(contacts, contact => contact.Region == "chest");
        Assert.DoesNotContain(contacts, contact => contact.Region.StartsWith("left_foot", StringComparison.Ordinal));
        Assert.DoesNotContain(contacts, contact => contact.Region.StartsWith("right_foot", StringComparison.Ordinal));
        Assert.Equal(0f, settled.LeftFootLoadNewtons);
        Assert.Equal(0f, settled.RightFootLoadNewtons);
        Assert.InRange(contacts.Sum(contact => contact.LoadNewtons), 719.0, 721.0);
    }

    [Fact]
    public void UpwardHandSupportTransfersWeightAwayFromTheGroundContactBudget()
    {
        var body = new AvatarArticulatedBody();
        body.ApplyExternalContact(new AvatarExternalBodyContact(
            "left_hand",
            new Vector3(-0.38f, 0.20f, 0.12f),
            Vector3.UnitY,
            ForceNewtons: 240.0,
            ImpulseNewtonSeconds: 6.0,
            ContactAreaSquareMillimeters: 480.0));

        body.Advance(0.025, 0.0, 0.0, 0.0, 0.0, 0.0, true, false);

        var groundLoad = body.CaptureGroundContacts().Sum(static contact => contact.LoadNewtons);
        Assert.InRange(groundLoad, 479.0, 481.0);
        Assert.InRange(groundLoad + 240.0, 719.0, 721.0);
    }

    [Fact]
    public void ReleasedHandContactLeavesNoPersistentSupportForce()
    {
        var body = new AvatarArticulatedBody();
        body.ApplyExternalContact(new AvatarExternalBodyContact(
            "left_hand",
            new Vector3(-0.38f, 0.20f, 0.12f),
            Vector3.UnitY,
            ForceNewtons: 240.0,
            ImpulseNewtonSeconds: 6.0,
            ContactAreaSquareMillimeters: 480.0));

        body.Advance(0.025, 0.0, 0.0, 0.0, 0.0, 0.0, true, false);
        var supportedLoad = body.CaptureGroundContacts().Sum(static contact => contact.LoadNewtons);

        body.Advance(0.025, 0.0, 0.0, 0.0, 0.0, 0.0, true, false);
        var released = body.CaptureFrame();
        var releasedGroundLoad = body.CaptureGroundContacts().Sum(static contact => contact.LoadNewtons);

        Assert.InRange(supportedLoad, 479.0, 481.0);
        Assert.InRange(releasedGroundLoad, 719.0, 721.0);
        Assert.Equal(0f, released.LeftHandLoadNewtons);
        Assert.Equal(0f, released.RightHandLoadNewtons);
    }

    [Fact]
    public void GroundContactLocationsComeFromTheResolvedArticulatedColliders()
    {
        var body = new AvatarArticulatedBody();
        for (var step = 0; step < 80; step++)
        {
            body.Advance(0.025, 0.9, 0.7, 1.2, 0.0, 0.0, true, false);
        }

        var frame = body.CaptureFrame();
        var colliders = AvatarColliderRig.CaptureResolved(frame)
            .ToDictionary(static collider => collider.Region, StringComparer.Ordinal);
        var contacts = body.CaptureGroundContacts();

        Assert.NotEmpty(contacts);
        Assert.All(contacts, contact =>
        {
            var colliderRegion = contact.Region.StartsWith("left_foot_", StringComparison.Ordinal)
                ? "left_foot"
                : contact.Region.StartsWith("right_foot_", StringComparison.Ordinal)
                    ? "right_foot"
                    : contact.Region;
            var collider = colliders[colliderRegion];
            var point = new Vector3((float)contact.BodyX, (float)contact.BodyY, (float)contact.BodyZ);
            if (colliderRegion.EndsWith("_foot", StringComparison.Ordinal))
            {
                var local = Vector3.Transform(
                    point - collider.Position,
                    Quaternion.Inverse(collider.Orientation));
                Assert.InRange(local.X, -collider.Size.X * 0.5f, collider.Size.X * 0.5f);
                Assert.Equal(-collider.Size.Y * 0.5f, local.Y, 5);
                Assert.InRange(local.Z, -collider.Size.Z * 0.5f, collider.Size.Z * 0.5f);
                Assert.Equal(collider.ContactAreaSquareMillimeters * 0.25f, contact.AreaSquareMillimeters, 3);
            }
            else
            {
                var expected = AvatarColliderRig.LowestSurfacePoint(collider);
                Assert.Equal(expected.X, point.X, 5);
                Assert.Equal(expected.Y, point.Y, 5);
                Assert.Equal(expected.Z, point.Z, 5);
                Assert.Equal(collider.ContactAreaSquareMillimeters, contact.AreaSquareMillimeters, 3);
            }
        });
    }

    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(1.0, 0.0)]
    public void SittingAndLyingCannotGeneratePlanarPropulsion(double sitDrive, double lieDrive)
    {
        var body = new AvatarArticulatedBody();
        AvatarMechanicalOutput output = default;

        for (var step = 0; step < 80; step++)
        {
            output = body.Advance(
                0.025,
                1.0,
                1.0,
                1.8,
                120.0,
                0.0,
                true,
                false,
                sitDrive: sitDrive,
                lieDrive: lieDrive);
        }

        Assert.Equal(0.0, output.ForwardSpeedMetersPerSecond);
        Assert.Equal(0.0, output.TurnRateDegreesPerSecond);
    }

    [Fact]
    public void CrouchingRemainsFootSupportedAndHasReducedMobility()
    {
        var standing = new AvatarArticulatedBody();
        var crouching = new AvatarArticulatedBody();
        AvatarMechanicalOutput standingOutput = default;
        AvatarMechanicalOutput crouchingOutput = default;

        for (var step = 0; step < 100; step++)
        {
            standing.Advance(0.025, 0.0, 0.0, 0.0, 0.0, 0.0, true, false);
            crouchingOutput = crouching.Advance(
                0.025, 0.0, 0.0, 0.0, 0.0, 0.0, true, false, crouchDrive: 1.0);
        }

        standingOutput = standing.Advance(0.025, 1.0, 1.0, 1.8, 0.0, 0.0, true, false);
        crouchingOutput = crouching.Advance(
            0.025, 1.0, 1.0, 1.8, 0.0, 0.0, true, false, crouchDrive: 1.0);

        Assert.True(crouchingOutput.ForwardSpeedMetersPerSecond > 0.0);
        Assert.True(crouchingOutput.ForwardSpeedMetersPerSecond < standingOutput.ForwardSpeedMetersPerSecond);
        Assert.All(crouching.CaptureGroundContacts(), contact => Assert.Contains("foot", contact.Region));
    }

    [Fact]
    public void FallenArticulatedBodyUsesDescendingStandDriveAndItsOwnMusclesToRightItself()
    {
        var body = new AvatarArticulatedBody();

        for (var step = 0; step < 80; step++)
        {
            body.ApplyExternalContact(new AvatarExternalBodyContact(
                "right_upper_arm",
                new System.Numerics.Vector3(0.42f, 0.68f, 0.18f),
                new System.Numerics.Vector3(-1f, 0f, -0.35f),
                ForceNewtons: 1_800.0,
                ImpulseNewtonSeconds: 32.0,
                ContactAreaSquareMillimeters: 1_900.0));
            body.Advance(
                0.025,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                grounded: true,
                movementBlocked: true,
                standDrive: 0.0);
        }

        var fallen = body.CaptureFrame().Musculoskeletal!;
        Assert.True(fallen.Posture is "falling" or "fallen");
        Assert.True(fallen.UprightFraction < 0.30f);

        var peakGluteActivation = 0f;
        var peakQuadricepsActivation = 0f;
        var observedRighting = false;
        for (var step = 0; step < 320; step++)
        {
            body.Advance(
                0.025,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                grounded: true,
                movementBlocked: false,
                standDrive: 1.0);
            var physical = body.CaptureFrame().Musculoskeletal!;
            var muscles = physical.Muscles;
            if (physical.Posture == "righting")
            {
                observedRighting = true;
                var measuredGroundLoad = body.CaptureGroundContacts().Sum(contact => contact.LoadNewtons);
                Assert.InRange(measuredGroundLoad, 0.001, 721.0);
            }
            peakGluteActivation = Math.Max(
                peakGluteActivation,
                muscles.Where(muscle => muscle.Name == "GluteusMaximus").Max(muscle => muscle.Activation));
            peakQuadricepsActivation = Math.Max(
                peakQuadricepsActivation,
                muscles.Where(muscle => muscle.Name == "Quadriceps").Max(muscle => muscle.Activation));
        }

        var recovered = body.CaptureFrame().Musculoskeletal!;
        Assert.Equal("standing", recovered.Posture);
        Assert.InRange(recovered.UprightFraction, 0.98f, 1.0f);
        Assert.InRange(recovered.BodyHeightMeters, 1.68f, 1.74f);
        Assert.True(observedRighting);
        Assert.True(peakGluteActivation > 0.10f, $"Peak glute activation was {peakGluteActivation:0.000}.");
        Assert.True(peakQuadricepsActivation > 0.10f,
            $"Peak quadriceps activation was {peakQuadricepsActivation:0.000}.");
    }

    private static float PressureTotal(PhysicalFootPressureFrame pressure)
        => pressure.HeelMedialLoadNewtons +
           pressure.HeelLateralLoadNewtons +
           pressure.ForefootMedialLoadNewtons +
           pressure.ForefootLateralLoadNewtons;

    private static void AssertFootContactMatchesLoad(
        IReadOnlyList<AvatarGroundContactProbe> contacts,
        string footRegion,
        float loadNewtons)
    {
        if (loadNewtons > 0.5f)
        {
            Assert.Contains(contacts, contact =>
                contact.Region.StartsWith(footRegion, StringComparison.Ordinal));
            return;
        }

        Assert.DoesNotContain(contacts, contact =>
            contact.Region.StartsWith(footRegion, StringComparison.Ordinal));
    }
}
