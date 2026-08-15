using NRE.SimAvatar;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AvatarArticulatedBodyTests
{
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
            Assert.InRange(frame.LeftKneeAngleRadians, 0f, 2.45f);
            Assert.InRange(frame.RightKneeAngleRadians, 0f, 2.45f);
            Assert.InRange(frame.LeftAnkleAngleRadians, -0.78f, 0.52f);
            Assert.InRange(frame.RightAnkleAngleRadians, -0.78f, 0.52f);
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
        Assert.Equal(32, muscles.Count);
        Assert.Contains(muscles, muscle => muscle.Name == "Quadriceps" && muscle.Activation > 0f);
        Assert.Contains(muscles, muscle => muscle.Name == "GastrocnemiusSoleus" && muscle.ForceNewtons > 0f);
        Assert.True(output.ForwardSpeedMetersPerSecond > 0.0);
        Assert.True(output.SupportFraction > 0.9);
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

    [Theory]
    [InlineData(0.0, 1.0, 0.0, 0.0, "crouching")]
    [InlineData(0.0, 0.0, 1.0, 0.0, "sitting")]
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
        Assert.InRange(contacts.Sum(contact => contact.LoadNewtons), 719.0, 721.0);
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
            var collider = colliders[contact.Region];
            Assert.Equal(collider.Position.X, contact.BodyX, 5);
            Assert.Equal(collider.Position.Z, contact.BodyZ, 5);
            Assert.Equal(AvatarColliderRig.LowestSurfaceY(collider), contact.BodyY, 5);
            Assert.Equal(collider.ContactAreaSquareMillimeters, contact.AreaSquareMillimeters, 3);
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
            standingOutput = standing.Advance(0.025, 1.0, 1.0, 1.8, 0.0, 0.0, true, false);
            crouchingOutput = crouching.Advance(
                0.025, 1.0, 1.0, 1.8, 0.0, 0.0, true, false, crouchDrive: 1.0);
        }

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
            var muscles = body.CaptureFrame().Musculoskeletal!.Muscles;
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
        Assert.True(peakGluteActivation > 0.10f, $"Peak glute activation was {peakGluteActivation:0.000}.");
        Assert.True(peakQuadricepsActivation > 0.10f,
            $"Peak quadriceps activation was {peakQuadricepsActivation:0.000}.");
    }
}
