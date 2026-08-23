using System.Numerics;
using NRE.SimAvatar;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AvatarBalanceDynamicsTests
{
    [Fact]
    public void BilateralFootSupportKeepsNeutralBodyStable()
    {
        var state = AvatarBalanceState.Neutral;
        AvatarBalanceResult result = default;
        for (var step = 0; step < 40; step++)
        {
            result = AvatarBalanceDynamics.Advance(
                state,
                PhysicalArticulationFrame.Neutral,
                StandingContacts(),
                [],
                grounded: true,
                commandedPosture: "standing",
                commandedBodyHeightMeters: 1.74,
                deltaSeconds: 0.025);
            state = result.State;
        }

        Assert.True(result.State.Phase is AvatarBalancePhase.Stable or AvatarBalancePhase.Marginal);
        Assert.True(result.Frame.SupportMarginMeters > -0.012f);
        Assert.InRange(result.UprightFraction, 0.99, 1.0);
        Assert.Equal("standing", result.PhysicalPosture);
    }

    [Fact]
    public void SustainedCenterOfMassOutsideSupportCommitsARealFall()
    {
        var state = AvatarBalanceState.Neutral;
        AvatarBalanceResult result = default;
        var displacedSupport = new[]
        {
            new AvatarGroundContactProbe("right_foot", 0.70, -0.90, 0.0, 720.0, 6_200.0)
        };

        for (var step = 0; step < 40; step++)
        {
            result = AvatarBalanceDynamics.Advance(
                state,
                PhysicalArticulationFrame.Neutral,
                displacedSupport,
                [],
                grounded: true,
                commandedPosture: "standing",
                commandedBodyHeightMeters: 1.74,
                deltaSeconds: 0.025);
            state = result.State;
        }

        Assert.True(result.State.Phase is AvatarBalancePhase.Falling or AvatarBalancePhase.Fallen);
        Assert.True(result.Frame.SupportMarginMeters < 0f);
        Assert.True(Math.Abs(result.Frame.FallRollRadians) > 0.05f);
        Assert.True(result.BalanceError >= 0.68);
        Assert.True(result.PhysicalPosture is "falling" or "fallen");
    }

    [Fact]
    public void RaisedLateralLegAndSingleSoleSupportProduceSidewaysFall()
    {
        var articulation = AvatarColliderRig.WithComputedSupportPlaneOffset(
            PhysicalArticulationFrame.Neutral with
            {
                LeftHipAbductionRadians = 0.60f
            });
        var colliders = AvatarColliderRig.CaptureResolved(articulation);
        var rightFoot = Assert.Single(colliders, collider => collider.Region == "right_foot");
        var support = new[]
        {
            new AvatarGroundContactProbe(
                "right_foot",
                rightFoot.Position.X,
                AvatarColliderRig.LowestSurfaceY(rightFoot),
                rightFoot.Position.Z,
                720.0,
                rightFoot.ContactAreaSquareMillimeters)
        };
        var state = AvatarBalanceState.Neutral;
        AvatarBalanceResult result = default;

        for (var step = 0; step < 40; step++)
        {
            result = AvatarBalanceDynamics.Advance(
                state,
                articulation,
                support,
                [],
                grounded: true,
                commandedPosture: "standing",
                commandedBodyHeightMeters: 1.74,
                deltaSeconds: 0.025);
            state = result.State;
        }

        Assert.True(result.State.Phase is AvatarBalancePhase.Falling or AvatarBalancePhase.Fallen);
        Assert.True(result.Frame.SupportMarginMeters < 0f);
        Assert.True(Math.Abs(result.Frame.FallRollRadians) > 0.05f);
    }

    [Fact]
    public void RightingDriveCannotCancelFallAfterSupportPassesBeyondCenterOfMass()
    {
        var displacedSupport = new[]
        {
            new AvatarGroundContactProbe("right_foot", 0.70, -0.90, 0.0, 720.0, 6_200.0)
        };
        var state = AvatarBalanceState.Neutral with
        {
            Initialized = true,
            Phase = AvatarBalancePhase.Righting,
            FallRollVelocityRadiansPerSecond = 2.75,
            RightingSeconds = 0.50
        };
        AvatarBalanceResult result = default;

        for (var step = 0; step < 12; step++)
        {
            result = AvatarBalanceDynamics.Advance(
                state,
                PhysicalArticulationFrame.Neutral,
                displacedSupport,
                [],
                grounded: true,
                commandedPosture: "standing",
                commandedBodyHeightMeters: 1.74,
                deltaSeconds: 0.025,
                rightingDrive: 1.0,
                rightingForceFraction: 1.0);
            state = result.State;
        }

        Assert.True(result.State.Phase is AvatarBalancePhase.Falling or AvatarBalancePhase.Fallen);
        Assert.True(result.Frame.SupportMarginMeters < -0.10f);
        Assert.True(Math.Abs(result.Frame.FallRollRadians) > 0.05f);
        Assert.True(result.BalanceError >= 0.68);
    }

    [Fact]
    public void BroadBodySupportDoesNotCreateAnUprightFallCommand()
    {
        var lying = PhysicalArticulationFrame.Neutral with
        {
            Musculoskeletal = MusculoskeletalStateFrame.Neutral with
            {
                Posture = "lying",
                BodyHeightMeters = 0.32f,
                UprightFraction = 0.05f
            }
        };

        var result = AvatarBalanceDynamics.Advance(
            AvatarBalanceState.Neutral,
            lying,
            [new AvatarGroundContactProbe("chest", 0.0, -0.55, 0.28, 720.0, 24_000.0)],
            [],
            grounded: true,
            commandedPosture: "lying",
            commandedBodyHeightMeters: 0.32,
            deltaSeconds: 0.025);

        Assert.Equal(AvatarBalancePhase.BroadSupport, result.State.Phase);
        Assert.Equal("lying", result.PhysicalPosture);
    }

    [Fact]
    public void MultiRegionRecumbentSupportCannotMasqueradeAsUprightBroadSupport()
    {
        var contacts = new[]
        {
            new AvatarExternalBodyContact(
                "head", new Vector3(0f, -0.52f, 0.62f), Vector3.UnitY, 90.0, 2.0, 9_000.0),
            new AvatarExternalBodyContact(
                "chest", new Vector3(0f, -0.56f, 0.24f), Vector3.UnitY, 250.0, 4.0, 24_000.0),
            new AvatarExternalBodyContact(
                "pelvis", new Vector3(0f, -0.58f, -0.10f), Vector3.UnitY, 190.0, 3.0, 18_000.0),
            new AvatarExternalBodyContact(
                "left_thigh", new Vector3(-0.16f, -0.60f, -0.42f), Vector3.UnitY, 95.0, 2.0, 11_000.0),
            new AvatarExternalBodyContact(
                "right_thigh", new Vector3(0.16f, -0.60f, -0.42f), Vector3.UnitY, 95.0, 2.0, 11_000.0)
        };

        var result = AvatarBalanceDynamics.Advance(
            AvatarBalanceState.Neutral,
            PhysicalArticulationFrame.Neutral,
            [],
            contacts,
            grounded: true,
            commandedPosture: "standing",
            commandedBodyHeightMeters: 1.74,
            deltaSeconds: 0.025);

        Assert.Equal(AvatarBalancePhase.Fallen, result.State.Phase);
        Assert.Equal("fallen", result.PhysicalPosture);
        Assert.InRange(result.UprightFraction, 0.0, 0.22);
        Assert.InRange(result.BodyHeightMeters, 0.28, 0.61);
        Assert.Equal(1.0, result.BalanceError);
    }

    [Fact]
    public void OffCenterExternalContactProducesAngularMomentum()
    {
        var result = AvatarBalanceDynamics.Advance(
            AvatarBalanceState.Neutral,
            PhysicalArticulationFrame.Neutral,
            StandingContacts(),
            [new AvatarExternalBodyContact(
                "right_upper_arm",
                new Vector3(0.40f, 0.52f, 0.06f),
                new Vector3(-1f, 0f, 0f),
                ForceNewtons: 900.0,
                ImpulseNewtonSeconds: 22.5,
                ContactAreaSquareMillimeters: 1_800.0)],
            grounded: true,
            commandedPosture: "standing",
            commandedBodyHeightMeters: 1.74,
            deltaSeconds: 0.025);

        Assert.True(Math.Abs(result.Frame.FallRollVelocityRadiansPerSecond) > 0.01f);
    }

    [Fact]
    public void FallenBodyDoesNotRightWithoutDescendingNeuronalDrive()
    {
        var fallen = CreateFallenState();

        for (var step = 0; step < 80; step++)
        {
            fallen = AvatarBalanceDynamics.Advance(
                fallen.State,
                PhysicalArticulationFrame.Neutral,
                StandingContacts(),
                [],
                grounded: true,
                commandedPosture: "standing",
                commandedBodyHeightMeters: 1.74,
                deltaSeconds: 0.025,
                rightingDrive: 0.0,
                rightingForceFraction: 0.85);
        }

        Assert.Equal(AvatarBalancePhase.Fallen, fallen.State.Phase);
        Assert.True(fallen.UprightFraction < 0.15);
    }

    [Fact]
    public void PhysicallyRecoveredBodyLeavesAStaleFallingClassificationWithoutAuthoredMotion()
    {
        var measured = AvatarBalanceDynamics.Advance(
            AvatarBalanceState.Neutral,
            PhysicalArticulationFrame.Neutral,
            StandingContacts(),
            [],
            grounded: true,
            commandedPosture: "standing",
            commandedBodyHeightMeters: 1.74,
            deltaSeconds: 0.025);
        var state = measured.State with
        {
            FallPitchRadians = 0.025,
            FallRollRadians = -0.018,
            FallPitchVelocityRadiansPerSecond = 0.02,
            FallRollVelocityRadiansPerSecond = -0.015,
            InstabilitySeconds = 0.25,
            Phase = AvatarBalancePhase.Falling
        };
        AvatarBalanceResult result = default;

        for (var step = 0; step < 20; step++)
        {
            result = AvatarBalanceDynamics.Advance(
                state,
                PhysicalArticulationFrame.Neutral,
                StandingContacts(),
                [],
                grounded: true,
                commandedPosture: "standing",
                commandedBodyHeightMeters: 1.74,
                deltaSeconds: 0.025,
                rightingDrive: 0.0,
                rightingForceFraction: 0.0);
            state = result.State;
        }

        Assert.True(result.State.Phase is AvatarBalancePhase.Stable or AvatarBalancePhase.Marginal);
        Assert.Equal("standing", result.PhysicalPosture);
        Assert.InRange(result.Frame.FallPitchRadians, 0.020f, 0.026f);
        Assert.InRange(result.Frame.FallRollRadians, -0.019f, -0.013f);
    }

    [Fact]
    public void SustainedNeuronalStandDriveAndExtensorForceCanRightFallenBody()
    {
        var result = CreateFallenState();

        for (var step = 0; step < 120; step++)
        {
            result = AvatarBalanceDynamics.Advance(
                result.State,
                PhysicalArticulationFrame.Neutral,
                StandingContacts(),
                [],
                grounded: true,
                commandedPosture: "standing",
                commandedBodyHeightMeters: 1.74,
                deltaSeconds: 0.025,
                rightingDrive: 0.90,
                rightingForceFraction: 0.80);
        }

        Assert.True(result.State.Phase is AvatarBalancePhase.Stable or AvatarBalancePhase.Marginal);
        Assert.InRange(result.UprightFraction, 0.98, 1.0);
        Assert.Equal("standing", result.PhysicalPosture);
    }

    [Fact]
    public void SustainedNeuronalRightingDriveCanCatchAnActiveFall()
    {
        var state = new AvatarBalanceState(
            Initialized: true,
            PreviousCenterOfMass: Vector2.Zero,
            CenterOfMassVelocity: new Vector2(0.12f, -0.08f),
            FallPitchRadians: 0.72,
            FallRollRadians: -0.46,
            FallPitchVelocityRadiansPerSecond: 1.2,
            FallRollVelocityRadiansPerSecond: -0.8,
            InstabilitySeconds: 0.20,
            RightingSeconds: 0.0,
            Phase: AvatarBalancePhase.Falling);
        AvatarBalanceResult result = default;

        for (var step = 0; step < 160; step++)
        {
            result = AvatarBalanceDynamics.Advance(
                state,
                PhysicalArticulationFrame.Neutral,
                StandingContacts(),
                [],
                grounded: true,
                commandedPosture: "standing",
                commandedBodyHeightMeters: 1.74,
                deltaSeconds: 0.025,
                rightingDrive: 0.90,
                rightingForceFraction: 0.80);
            state = result.State;
        }

        Assert.True(result.State.Phase is AvatarBalancePhase.Stable or AvatarBalancePhase.Marginal);
        Assert.InRange(result.UprightFraction, 0.98, 1.0);
        Assert.Equal("standing", result.PhysicalPosture);
    }

    [Fact]
    public void BriefRightingEvidenceGapsDoNotRestartTheSameFall()
    {
        var state = new AvatarBalanceState(
            Initialized: true,
            PreviousCenterOfMass: Vector2.Zero,
            CenterOfMassVelocity: new Vector2(0.03f, -0.02f),
            FallPitchRadians: 0.42,
            FallRollRadians: -0.28,
            FallPitchVelocityRadiansPerSecond: 0.35,
            FallRollVelocityRadiansPerSecond: -0.25,
            InstabilitySeconds: 0.20,
            RightingSeconds: 0.40,
            Phase: AvatarBalancePhase.Righting);
        var fallingEntries = 0;
        var previousPhase = state.Phase;

        for (var step = 0; step < 20; step++)
        {
            var evidencePresent = step % 2 == 0;
            var result = AvatarBalanceDynamics.Advance(
                state,
                PhysicalArticulationFrame.Neutral,
                StandingContacts(),
                [],
                grounded: true,
                commandedPosture: "standing",
                commandedBodyHeightMeters: 1.74,
                deltaSeconds: 0.025,
                rightingDrive: evidencePresent ? 0.90 : 0.0,
                rightingForceFraction: evidencePresent ? 0.80 : 0.0);
            state = result.State;
            if (state.Phase == AvatarBalancePhase.Falling && previousPhase != AvatarBalancePhase.Falling)
            {
                fallingEntries++;
            }
            previousPhase = state.Phase;
        }

        Assert.Equal(0, fallingEntries);
        Assert.True(state.Phase is AvatarBalancePhase.Righting or AvatarBalancePhase.Stable or AvatarBalancePhase.Marginal);
    }

    [Fact]
    public void PostureLabelWithoutBodyContactDoesNotInventBroadSupport()
    {
        var result = AvatarBalanceDynamics.Advance(
            AvatarBalanceState.Neutral,
            PhysicalArticulationFrame.Neutral,
            StandingContacts(),
            [],
            grounded: true,
            commandedPosture: "sitting",
            commandedBodyHeightMeters: 0.78,
            deltaSeconds: 0.025);

        Assert.NotEqual(AvatarBalancePhase.BroadSupport, result.State.Phase);
    }

    [Fact]
    public void GroundedBodyWithoutMeasuredSupportFallsTruthfully()
    {
        var state = AvatarBalanceState.Neutral;
        AvatarBalanceResult result = default;

        for (var step = 0; step < 12; step++)
        {
            result = AvatarBalanceDynamics.Advance(
                state,
                PhysicalArticulationFrame.Neutral,
                [],
                [],
                grounded: true,
                commandedPosture: "standing",
                commandedBodyHeightMeters: 1.74,
                deltaSeconds: 0.025);
            state = result.State;
        }

        Assert.True(result.State.Phase is AvatarBalancePhase.Falling or AvatarBalancePhase.Fallen);
        Assert.Equal(-1f, result.Frame.SupportMarginMeters);
    }

    [Fact]
    public void NeuronalLocomotorEffortPermitsBoundedDynamicImbalance()
    {
        var articulation = PhysicalArticulationFrame.Neutral;
        var colliders = AvatarColliderRig.CaptureResolved(articulation);
        var mass = colliders.Sum(static collider => collider.EffectiveMassKilograms);
        var center = colliders.Aggregate(
            Vector3.Zero,
            (sum, collider) => sum + (collider.Position * collider.EffectiveMassKilograms)) / mass;
        var naturalFrequency = Math.Sqrt(9.80665 / Math.Max(0.20, center.Y));
        const double desiredDynamicMargin = -0.03;
        const double supportFrontMeters = 0.155;
        var desiredExtrapolatedZ = supportFrontMeters - desiredDynamicMargin;
        var desiredVelocityZ = (desiredExtrapolatedZ - center.Z) * naturalFrequency;
        const double velocityBlend = 0.30;
        const double deltaSeconds = 0.025;
        var priorCenter = new Vector2(
            center.X,
            center.Z - (float)((desiredVelocityZ / velocityBlend) * deltaSeconds));
        var initial = AvatarBalanceState.Neutral with
        {
            Initialized = true,
            PreviousCenterOfMass = priorCenter
        };

        var passive = AvatarBalanceDynamics.Advance(
            initial,
            articulation,
            StandingContacts(),
            [],
            grounded: true,
            commandedPosture: "standing",
            commandedBodyHeightMeters: 1.74,
            deltaSeconds: deltaSeconds);
        var locomoting = AvatarBalanceDynamics.Advance(
            initial,
            articulation,
            StandingContacts(),
            [],
            grounded: true,
            commandedPosture: "standing",
            commandedBodyHeightMeters: 1.74,
            deltaSeconds: deltaSeconds,
            locomotorEffort: 1.0,
            commandedForwardSpeedMetersPerSecond: 1.8);

        Assert.Equal(AvatarBalancePhase.Unstable, passive.State.Phase);
        Assert.Equal(AvatarBalancePhase.Dynamic, locomoting.State.Phase);
        Assert.InRange(locomoting.Frame.SupportMarginMeters, -0.05f, -0.012f);
        Assert.InRange(locomoting.Frame.DynamicStabilityAllowanceMeters, 0.074f, 0.076f);
        Assert.True(passive.BalanceError >= 0.45);
        Assert.Equal(0.0, locomoting.BalanceError);
    }

    [Fact]
    public void CrossedLegsCannotHideForwardMomentumBehindStepReserve()
    {
        var articulation = AvatarColliderRig.WithComputedSupportPlaneOffset(
            PhysicalArticulationFrame.Neutral with
            {
                LeftHipAbductionRadians = -0.45f,
                RightHipAbductionRadians = -0.45f
            });
        var colliders = AvatarColliderRig.CaptureResolved(articulation);
        var leftFoot = Assert.Single(colliders, collider => collider.Region == "left_foot");
        var rightFoot = Assert.Single(colliders, collider => collider.Region == "right_foot");
        Assert.True(leftFoot.Position.X > rightFoot.Position.X);

        var contacts = new[]
        {
            new AvatarGroundContactProbe(
                "left_foot",
                leftFoot.Position.X,
                AvatarColliderRig.LowestSurfaceY(leftFoot),
                leftFoot.Position.Z,
                360.0,
                leftFoot.ContactAreaSquareMillimeters),
            new AvatarGroundContactProbe(
                "right_foot",
                rightFoot.Position.X,
                AvatarColliderRig.LowestSurfaceY(rightFoot),
                rightFoot.Position.Z,
                360.0,
                rightFoot.ContactAreaSquareMillimeters)
        };
        var state = AvatarBalanceState.Neutral;
        AvatarBalanceResult result = default;
        AvatarBalanceResult initial = default;

        for (var step = 0; step < 24; step++)
        {
            result = AvatarBalanceDynamics.Advance(
                state,
                articulation,
                contacts,
                [],
                grounded: true,
                commandedPosture: "standing",
                commandedBodyHeightMeters: 1.74,
                deltaSeconds: 0.025,
                locomotorEffort: 1.0,
                commandedForwardSpeedMetersPerSecond: 1.2);
            if (step == 0)
            {
                initial = result;
            }
            state = result.State;
        }

        Assert.True(result.State.Phase is AvatarBalancePhase.Falling or AvatarBalancePhase.Fallen);
        Assert.True(initial.Frame.SupportMarginMeters < -0.012f);
        Assert.Equal(0f, initial.Frame.DynamicStabilityAllowanceMeters, 5);
        Assert.True(result.Frame.FallPitchRadians < -0.01f);
    }

    [Fact]
    public void CrossedLegsAtRestDoNotInventForwardMomentum()
    {
        var articulation = AvatarColliderRig.WithComputedSupportPlaneOffset(
            PhysicalArticulationFrame.Neutral with
            {
                LeftHipAbductionRadians = -0.45f,
                RightHipAbductionRadians = -0.45f
            });
        var colliders = AvatarColliderRig.CaptureResolved(articulation);
        var leftFoot = Assert.Single(colliders, collider => collider.Region == "left_foot");
        var rightFoot = Assert.Single(colliders, collider => collider.Region == "right_foot");
        var contacts = new[]
        {
            new AvatarGroundContactProbe(
                "left_foot",
                leftFoot.Position.X,
                AvatarColliderRig.LowestSurfaceY(leftFoot),
                leftFoot.Position.Z,
                360.0,
                leftFoot.ContactAreaSquareMillimeters),
            new AvatarGroundContactProbe(
                "right_foot",
                rightFoot.Position.X,
                AvatarColliderRig.LowestSurfaceY(rightFoot),
                rightFoot.Position.Z,
                360.0,
                rightFoot.ContactAreaSquareMillimeters)
        };

        var result = AvatarBalanceDynamics.Advance(
            AvatarBalanceState.Neutral,
            articulation,
            contacts,
            [],
            grounded: true,
            commandedPosture: "standing",
            commandedBodyHeightMeters: 1.74,
            deltaSeconds: 0.025,
            locomotorEffort: 0.0,
            commandedForwardSpeedMetersPerSecond: 0.0);

        Assert.True(result.State.Phase is AvatarBalancePhase.Stable or AvatarBalancePhase.Marginal);
        Assert.True(result.Frame.SupportMarginMeters > 0f);
    }

    private static AvatarBalanceResult CreateFallenState()
    {
        var state = AvatarBalanceState.Neutral;
        AvatarBalanceResult result = default;
        var displacedSupport = new[]
        {
            new AvatarGroundContactProbe("right_foot", 0.70, -0.90, 0.0, 720.0, 6_200.0)
        };

        for (var step = 0; step < 80; step++)
        {
            result = AvatarBalanceDynamics.Advance(
                state,
                PhysicalArticulationFrame.Neutral,
                displacedSupport,
                [],
                grounded: true,
                commandedPosture: "standing",
                commandedBodyHeightMeters: 1.74,
                deltaSeconds: 0.025);
            state = result.State;
        }

        Assert.Equal(AvatarBalancePhase.Fallen, result.State.Phase);
        return result;
    }

    private static IReadOnlyList<AvatarGroundContactProbe> StandingContacts() =>
    [
        new AvatarGroundContactProbe("left_foot", -0.14, -0.90, 0.0, 360.0, 6_200.0),
        new AvatarGroundContactProbe("right_foot", 0.14, -0.90, 0.0, 360.0, 6_200.0)
    ];
}
