using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AvatarNervousSystemTests
{
    [Fact]
    public void InterpretBrainSignalsIntegratesMotorDrive()
    {
        var nervousSystem = CreateNervousSystem();
        var dispatches = new[]
        {
            new AvatarDispatchSpike("M1", "L", 100, "motor_forward"),
            new AvatarDispatchSpike("M1", "R", 101, "motor_forward")
        };

        var signal = nervousSystem.InterpretBrainSignals(dispatches, AwakeBody);

        Assert.Equal(2, signal.MotorEvents);
        Assert.True(signal.LeftMotorDrive > 0);
        Assert.True(signal.RightMotorDrive > 0);
        Assert.Equal(0, signal.TicksWithoutMotorDispatch);
    }

    [Fact]
    public void InterpretBrainSignalsGatesMotorDuringSleepButKeepsToolSignal()
    {
        var nervousSystem = CreateNervousSystem();
        var dispatches = new[]
        {
            new AvatarDispatchSpike("Sma", "L", 100, "tool_forward")
        };

        var signal = nervousSystem.InterpretBrainSignals(dispatches, AwakeBody with { IsSleeping = true });

        Assert.Equal(0, signal.LeftMotorDrive);
        Assert.Equal(0, signal.RightMotorDrive);
        Assert.Equal(0, signal.MotorEvents);
        Assert.Equal(AvatarToolAction.Build, signal.Tool.Action);
    }

    [Fact]
    public void InterpretBrainSignalsProducesToolIntentFromMotorStructures()
    {
        var nervousSystem = CreateNervousSystem();
        var dispatches = new[]
        {
            new AvatarDispatchSpike("PremotorCortex", "R", 100, "dig.forward"),
            new AvatarDispatchSpike("PremotorCortex", "R", 101, "dig.forward")
        };

        var signal = nervousSystem.InterpretBrainSignals(dispatches, AwakeBody);

        Assert.True(signal.Tool.HasAction);
        Assert.Equal(AvatarToolAction.Dig, signal.Tool.Action);
    }

    [Fact]
    public void InterpretBrainSignalsDoesNotSynthesizeIdleMovement()
    {
        var nervousSystem = CreateNervousSystem(idleMotorFallbackTicks: 1);
        AvatarNervousSystemSignal signal = default;

        for (var i = 0; i < 8; i++)
        {
            signal = nervousSystem.InterpretBrainSignals(Array.Empty<AvatarDispatchSpike>(), AwakeBody with
            {
                Hunger = 0.95,
                Threat = 0.85,
                SecondsSinceProgress = 30.0
            });
        }

        Assert.Equal(0, signal.MotorEvents);
        Assert.Equal(0.0, signal.LeftMotorDrive);
        Assert.Equal(0.0, signal.RightMotorDrive);
        Assert.Equal(8, signal.TicksWithoutMotorDispatch);
    }

    private static AvatarNervousSystemBodyState AwakeBody { get; } = new(
        IsSleeping: false,
        Hunger: 0.2,
        Threat: 0.1,
        Health: 1.0,
        SecondsSinceProgress: 0.0,
        NoProgressTimeoutSeconds: 4.0);

    private static AvatarNervousSystem CreateNervousSystem(int idleMotorFallbackTicks = int.MaxValue)
        => new(new AvatarNervousSystemOptions(
            new AvatarKinematicsOptions(
                MaxMotorDrive: 240.0,
                ForwardSpeedCoefficient: 0.0125,
                TurnSpeedCoefficient: 3.2,
                MinForwardSpeed: 0.0,
                MaxForwardSpeed: 3.2,
                MaxTurnRateDeg: 220.0),
            IdleMotorFallbackTicks: idleMotorFallbackTicks));
}
