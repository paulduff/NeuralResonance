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
