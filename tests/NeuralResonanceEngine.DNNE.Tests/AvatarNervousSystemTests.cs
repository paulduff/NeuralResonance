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
    public void LocomotorAndManipulatorPopulationEventsRemainDistinct()
    {
        var locomotor = new AvatarDispatchSpike(
            "SpinalCordMotor", "L", 100, "population:l:excitatory:4:0");
        var manipulator = new AvatarDispatchSpike(
            "SpinalCordMotor", "M", 101, "effector:manipulator:excitatory:4:0");

        Assert.True(AvatarMotorCatalog.IsLocomotorPopulationEvent(locomotor));
        Assert.False(AvatarEffectorCatalog.IsManipulatorEvent(locomotor));
        Assert.False(AvatarMotorCatalog.IsLocomotorPopulationEvent(manipulator));
        Assert.True(AvatarEffectorCatalog.IsManipulatorEvent(manipulator));
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
            ["LeftMotorDrive", "RightMotorDrive", "ManipulatorDrive", "MotorEvents", "ManipulatorEvents", "TicksWithoutMotorDispatch"],
            typeof(AvatarNervousSystemSignal).GetProperties().Select(static property => property.Name).ToArray());
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
