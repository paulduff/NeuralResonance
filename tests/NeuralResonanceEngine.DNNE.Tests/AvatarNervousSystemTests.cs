using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AvatarNervousSystemTests
{
    [Fact]
    public void NumericProtocolStructureIdProducesMotorDrive()
    {
        var frame = JsonSerializer.SerializeToElement(new
        {
            dispatchSpikes = new[]
            {
                new
                {
                    wallClockUnixMs = 101L,
                    sourceStructure = StructureId.M1,
                    sourceHemisphere = "L",
                    sourceNeuronId = "population:l:excitatory:1:0"
                },
                new
                {
                    wallClockUnixMs = 102L,
                    sourceStructure = StructureId.M1,
                    sourceHemisphere = "R",
                    sourceNeuronId = "population:r:excitatory:1:0"
                }
            }
        });

        var dispatches = AvatarDispatchSpikeParser.ParseDispatchSpikes(frame, 0, out var cursor);
        var signal = CreateNervousSystem().InterpretBrainSignals(dispatches);

        Assert.Equal(102L, cursor);
        Assert.All(dispatches, dispatch => Assert.Equal("M1", dispatch.SourceStructure));
        Assert.Equal(2, signal.MotorEvents);
        Assert.True(signal.LeftMotorDrive > 0);
        Assert.True(signal.RightMotorDrive > 0);
    }

    [Fact]
    public void InterpretBrainSignalsIntegratesOnlyMotorPopulationSpikes()
    {
        var nervousSystem = CreateNervousSystem();
        var dispatches = new[]
        {
            new AvatarDispatchSpike("M1", "L", 100, "population:l:excitatory:1:0"),
            new AvatarDispatchSpike("M1", "R", 101, "population:r:excitatory:1:0")
        };

        var signal = nervousSystem.InterpretBrainSignals(dispatches);

        Assert.Equal(2, signal.MotorEvents);
        Assert.True(signal.LeftMotorDrive > 0);
        Assert.True(signal.RightMotorDrive > 0);
        Assert.Equal(0, signal.TicksWithoutMotorDispatch);
    }

    [Fact]
    public void SemanticMotorNamesProduceNoBodyDrive()
    {
        var nervousSystem = CreateNervousSystem();
        var dispatches = new[]
        {
            new AvatarDispatchSpike("PremotorCortex", "L", 100, "motor_seek_shelter"),
            new AvatarDispatchSpike("Sma", "R", 101, "dig.forward"),
            new AvatarDispatchSpike("M1", "L", 102, "move_to_goal")
        };

        var signal = nervousSystem.InterpretBrainSignals(dispatches);

        Assert.Equal(0, signal.MotorEvents);
        Assert.Equal(0.0, signal.LeftMotorDrive);
        Assert.Equal(0.0, signal.RightMotorDrive);
    }

    [Fact]
    public void IdleTicksNeverSynthesizeMovement()
    {
        var nervousSystem = CreateNervousSystem();
        AvatarNervousSystemSignal signal = default;

        for (var i = 0; i < 8; i++)
        {
            signal = nervousSystem.InterpretBrainSignals([]);
        }

        Assert.Equal(0, signal.MotorEvents);
        Assert.Equal(0.0, signal.LeftMotorDrive);
        Assert.Equal(0.0, signal.RightMotorDrive);
        Assert.Equal(8, signal.TicksWithoutMotorDispatch);
    }

    [Fact]
    public void LocomotorAndExplicitHandPopulationEventsRemainDistinct()
    {
        var locomotor = new AvatarDispatchSpike(
            "SpinalCordMotor", "L", 100, "population:l:excitatory:4:0");
        var hand = new AvatarDispatchSpike(
            "SpinalCordMotor", "L", 101, "effector:hand:left:grasp:excitatory:4:0");

        Assert.True(AvatarMotorCatalog.IsLocomotorPopulationEvent(locomotor));
        Assert.Equal(0, AvatarEffectorCatalog.SummarizeHandDrive([locomotor]).Events);
        Assert.False(AvatarMotorCatalog.IsLocomotorPopulationEvent(hand));
        var handDrive = AvatarEffectorCatalog.SummarizeHandDrive([hand]);
        Assert.Equal(1, handDrive.Events);
        Assert.True(handDrive.LeftDelta > 0.0);
        Assert.Equal(0.0, handDrive.RightDelta);
    }

    [Fact]
    public void PeripheralMotorLayerHasNoBodyStateOrSemanticActionContract()
    {
        var assembly = typeof(AvatarNervousSystem).Assembly;
        Assert.Null(assembly.GetType("NRE.SimAvatar.AvatarNervousSystemBodyState"));
        Assert.Null(assembly.GetType("NRE.SimAvatar.AvatarToolSignal"));
        Assert.Equal(
            ["Kinematics", "DriveDecay"],
            typeof(AvatarNervousSystemOptions).GetProperties().Select(static property => property.Name).ToArray());
        Assert.Equal(
            [
                "LeftMotorDrive", "RightMotorDrive", "ManipulatorDrive",
                "LeftShoulderSagittalDrive", "RightShoulderSagittalDrive",
                "LeftShoulderCoronalDrive", "RightShoulderCoronalDrive",
                "LeftElbowDrive", "RightElbowDrive",
                "HeadYawDrive", "HeadPitchDrive",
                "StandDrive", "CrouchDrive", "SitDrive", "LieDrive",
                "MotorEvents", "ManipulatorEvents", "OrientingEvents", "PostureEvents", "TicksWithoutMotorDispatch",
                "LeftHipCoronalDrive", "RightHipCoronalDrive",
                "LeftAnkleSagittalDrive", "RightAnkleSagittalDrive",
                "LeftAnkleCoronalDrive", "RightAnkleCoronalDrive",
                "TrunkYawDrive", "LeftHandGraspDrive", "RightHandGraspDrive"
            ],
            typeof(AvatarNervousSystemSignal).GetProperties().Select(static property => property.Name).ToArray());
    }

    [Fact]
    public void LateralHipPopulationProducesSignedLegDriveWithoutLocomotion()
    {
        var nervousSystem = CreateNervousSystem();
        var signal = nervousSystem.InterpretBrainSignals(
        [
            new AvatarDispatchSpike(
                "SpinalCordMotor", "L", 100, "effector:leg:left:hip:coronal:excitatory:9:0"),
            new AvatarDispatchSpike(
                "SpinalCordMotor", "R", 101, "effector:leg:right:hip:coronal:inhibitory:9:0")
        ]);

        Assert.True(signal.LeftHipCoronalDrive > 0.0);
        Assert.True(signal.RightHipCoronalDrive < 0.0);
        Assert.Equal(0.0, signal.LeftMotorDrive);
        Assert.Equal(0.0, signal.RightMotorDrive);
    }

    [Fact]
    public void TwoAxisAnklePopulationsProduceIndependentSignedLegDrive()
    {
        var nervousSystem = CreateNervousSystem();
        var signal = nervousSystem.InterpretBrainSignals(
        [
            new AvatarDispatchSpike(
                "SpinalCordMotor", "L", 100, "effector:leg:left:ankle:sagittal:excitatory:9:0"),
            new AvatarDispatchSpike(
                "SpinalCordMotor", "R", 101, "effector:leg:right:ankle:sagittal:inhibitory:9:0"),
            new AvatarDispatchSpike(
                "SpinalCordMotor", "L", 102, "effector:leg:left:ankle:coronal:excitatory:9:0"),
            new AvatarDispatchSpike(
                "SpinalCordMotor", "R", 103, "effector:leg:right:ankle:coronal:inhibitory:9:0")
        ]);

        Assert.True(signal.LeftAnkleSagittalDrive > 0.0);
        Assert.True(signal.RightAnkleSagittalDrive < 0.0);
        Assert.True(signal.LeftAnkleCoronalDrive > 0.0);
        Assert.True(signal.RightAnkleCoronalDrive < 0.0);
        Assert.Equal(0.0, signal.LeftMotorDrive);
        Assert.Equal(0.0, signal.RightMotorDrive);
    }

    [Fact]
    public void AxialPopulationProducesSignedTrunkDriveWithoutLocomotion()
    {
        var nervousSystem = CreateNervousSystem();
        var signal = nervousSystem.InterpretBrainSignals(
        [
            new AvatarDispatchSpike(
                "SpinalCordMotor", "M", 100, "effector:axial:trunk:yaw:excitatory:9:0")
        ]);

        Assert.True(signal.TrunkYawDrive > 0.0);
        Assert.Equal(0.0, signal.LeftMotorDrive);
        Assert.Equal(0.0, signal.RightMotorDrive);
    }

    [Fact]
    public void OrientingPopulationProducesSignedHeadDriveWithoutLocomotion()
    {
        var nervousSystem = CreateNervousSystem();
        var signal = nervousSystem.InterpretBrainSignals(
        [
            new AvatarDispatchSpike(
                "SpinalCordMotor", "M", 100, "effector:orient:yaw:excitatory:9:0"),
            new AvatarDispatchSpike(
                "SpinalCordMotor", "M", 101, "effector:orient:pitch:inhibitory:9:0")
        ]);

        Assert.Equal(2, signal.OrientingEvents);
        Assert.True(signal.HeadYawDrive > 0.0);
        Assert.True(signal.HeadPitchDrive < 0.0);
        Assert.Equal(0.0, signal.LeftMotorDrive);
        Assert.Equal(0.0, signal.RightMotorDrive);
    }

    [Fact]
    public void PosturePopulationEventsRemainPhysicalAndDistinctFromLocomotion()
    {
        var nervousSystem = CreateNervousSystem();
        var signal = nervousSystem.InterpretBrainSignals(
        [
            new AvatarDispatchSpike(
                "SpinalCordMotor", "M", 100, "effector:posture:crouch:excitatory:9:0")
        ]);

        Assert.Equal(1, signal.PostureEvents);
        Assert.True(signal.CrouchDrive > 0.0);
        Assert.Equal(0.0, signal.LeftMotorDrive);
        Assert.Equal(0.0, signal.RightMotorDrive);
    }

    [Fact]
    public void NewPosturePopulationInhibitsThePreviouslyAccumulatedPosture()
    {
        var nervousSystem = CreateNervousSystem();
        nervousSystem.InterpretBrainSignals(
        [
            new AvatarDispatchSpike(
                "SpinalCordMotor", "M", 100, "effector:posture:sit:excitatory:9:0")
        ]);

        var signal = nervousSystem.InterpretBrainSignals(
        [
            new AvatarDispatchSpike(
                "SpinalCordMotor", "M", 101, "effector:posture:stand:excitatory:10:0")
        ]);

        Assert.True(signal.StandDrive > 0.0);
        Assert.Equal(0.0, signal.CrouchDrive);
        Assert.Equal(0.0, signal.SitDrive);
        Assert.Equal(0.0, signal.LieDrive);
    }

    [Fact]
    public void OpposingEffectorPopulationReleasesItsAntagonistTraceImmediately()
    {
        var nervousSystem = CreateNervousSystem();
        nervousSystem.InterpretBrainSignals(
        [
            new AvatarDispatchSpike(
                "SpinalCordMotor", "L", 100, "effector:arm:left:shoulder:sagittal:excitatory:9:0")
        ]);

        var signal = nervousSystem.InterpretBrainSignals(
        [
            new AvatarDispatchSpike(
                "SpinalCordMotor", "L", 101, "effector:arm:left:shoulder:sagittal:inhibitory:10:0")
        ]);

        Assert.True(signal.LeftShoulderSagittalDrive < 0.0);
    }

    private static AvatarNervousSystem CreateNervousSystem()
        => new(new AvatarNervousSystemOptions(
            new AvatarKinematicsOptions(
                MaxMotorDrive: 240.0,
                ForwardSpeedCoefficient: 0.0125,
                TurnSpeedCoefficient: 3.2,
                MinForwardSpeed: 0.0,
                MaxForwardSpeed: 3.2,
                MaxTurnRateDeg: 220.0)));
}
