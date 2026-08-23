using System.Numerics;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.WorldSim;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AvatarTerrainAscentControllerTests
{
    private static readonly AvatarTerrainAscentReadiness StrongStepReadiness = new(
        ForwardEffort: 0.70,
        LegEffort: 0.82,
        ArmEffort: 0.0,
        ManipulatorEffort: 0.0,
        UprightFraction: 0.94,
        SupportFraction: 0.92,
        LeftHandSupported: false,
        RightHandSupported: false);

    private static readonly AvatarTerrainAscentReadiness StrongMantleReadiness = new(
        ForwardEffort: 0.70,
        LegEffort: 0.76,
        ArmEffort: 0.72,
        ManipulatorEffort: 0.84,
        UprightFraction: 0.86,
        SupportFraction: 0.74,
        LeftHandSupported: true,
        RightHandSupported: false);

    [Fact]
    public void QuarterMetreRiseUsesLegDrivenStepMotion()
    {
        var controller = new AvatarTerrainAscentController();
        var root = new Vector3(0f, 0.03f, 0f);
        var rise = new WorldTerrainRise(0.5, 0.0, 1.0, 0.0, 0.25, 0.25);

        Assert.True(controller.TryBegin(root, rise, StrongStepReadiness));
        Assert.Equal(AvatarTerrainAscentMode.Step, controller.Mode);

        var previous = root;
        for (var tick = 0; tick < 200 && controller.IsActive; tick++)
        {
            var proposal = controller.Propose(0.025, StrongStepReadiness);
            Assert.True(proposal.Active);
            Assert.InRange(Vector3.Distance(previous, proposal.RootPosition), 0f, 0.10f);
            controller.Commit(proposal, proposal.RootPosition, 0.025);
            previous = proposal.RootPosition;
        }

        Assert.False(controller.IsActive);
        Assert.Equal(0.28f, previous.Y, 3);
        Assert.Equal(1.0f, previous.Z, 3);
        Assert.Equal(1, controller.EncounterCount);
        Assert.Equal(1, controller.StartedCount);
        Assert.Equal(1, controller.StepStartedCount);
        Assert.Equal(1, controller.CompletedCount);
        Assert.Equal(1, controller.StepCompletedCount);
        Assert.Contains("completed", controller.LastOutcome, StringComparison.Ordinal);
    }

    [Fact]
    public void OneMetreMantleCannotBeginWithoutMeasuredHandSupport()
    {
        var controller = new AvatarTerrainAscentController();
        var unsupported = StrongMantleReadiness with
        {
            LeftHandSupported = false,
            RightHandSupported = false
        };
        var rise = new WorldTerrainRise(0.5, 0.0, 1.0, 0.0, 1.0, 1.0);

        Assert.False(controller.TryBegin(Vector3.Zero, rise, unsupported));
        Assert.False(controller.IsActive);
        Assert.Equal(1, controller.RejectedCount);
        Assert.True(controller.TryBegin(Vector3.Zero, rise, StrongMantleReadiness));
        Assert.Equal(AvatarTerrainAscentMode.Mantle, controller.Mode);
        Assert.Equal(2, controller.EncounterCount);
        Assert.Equal(1, controller.MantleStartedCount);
    }

    [Fact]
    public void MantleAbortsWhenNeuronalEffortIsWithdrawn()
    {
        var controller = new AvatarTerrainAscentController();
        var rise = new WorldTerrainRise(0.5, 0.0, 1.0, 0.0, 1.0, 1.0);
        Assert.True(controller.TryBegin(Vector3.Zero, rise, StrongMantleReadiness));

        var absent = new AvatarTerrainAscentReadiness();
        for (var tick = 0; tick < 30 && controller.IsActive; tick++)
        {
            controller.Propose(0.025, absent);
        }

        Assert.False(controller.IsActive);
        Assert.Equal(1, controller.AbortedCount);
        Assert.Contains("withdrawn", controller.LastOutcome, StringComparison.Ordinal);
    }

    [Fact]
    public void CollisionSceneCanRejectAnOtherwiseValidMantle()
    {
        using var scene = new WorldPhysicsScene(
        [
            new WorldPhysicsBox(
                "uncrossable_wall",
                new Vector3(0f, 2f, 0.72f),
                new Vector3(4f, 4f, 0.04f),
                Quaternion.Identity)
        ]);
        var controller = new AvatarTerrainAscentController();
        var rise = new WorldTerrainRise(0.4, 0.0, 1.2, 0.0, 1.0, 1.0);
        var root = Vector3.Zero;
        Assert.True(controller.TryBegin(root, rise, StrongMantleReadiness));

        for (var tick = 0; tick < 400 && controller.IsActive; tick++)
        {
            var proposal = controller.Propose(0.025, StrongMantleReadiness);
            if (!proposal.Active)
            {
                break;
            }

            var resolution = scene.ResolveAvatar(
                root,
                0f,
                PhysicalArticulationFrame.Neutral,
                proposal.RootPosition,
                0f,
                PhysicalArticulationFrame.Neutral,
                0.025f);
            controller.Commit(proposal, resolution.RootPosition, 0.025);
            root = resolution.RootPosition;
        }

        Assert.False(controller.IsActive);
        Assert.True(root.Z < 0.60f);
        Assert.Equal(1, controller.AbortedCount);
        Assert.Contains("collision scene", controller.LastOutcome, StringComparison.Ordinal);
    }

    [Fact]
    public void BodyRespawnCancelsActiveAscentWithoutErasingRunStatistics()
    {
        var controller = new AvatarTerrainAscentController();
        var rise = new WorldTerrainRise(0.5, 0.0, 1.0, 0.0, 0.25, 0.25);
        Assert.True(controller.TryBegin(Vector3.Zero, rise, StrongStepReadiness));

        controller.CancelActive("physical body respawned");

        Assert.False(controller.IsActive);
        Assert.Equal(1, controller.EncounterCount);
        Assert.Equal(1, controller.StartedCount);
        Assert.Equal(1, controller.AbortedCount);
        Assert.Contains("respawned", controller.LastOutcome, StringComparison.Ordinal);
    }
}
