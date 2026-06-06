using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AvatarKinematicsTests
{
    [Fact]
    public void World_Turn_Left_Command_Pivots_In_Place()
    {
        var dispatches = new[]
        {
            new AvatarDispatchSpike("M1", "L", 100, "L:motor_turn_left_100_0")
        };
        var options = new AvatarKinematicsOptions(
            MaxMotorDrive: 240.0,
            ForwardSpeedCoefficient: 0.0128,
            TurnSpeedCoefficient: 3.2,
            MinForwardSpeed: -1.6,
            MaxForwardSpeed: 8.1,
            MaxTurnRateDeg: 240.0,
            AllowSignedMotorDrive: true,
            InPlaceTurnCancelsForwardDrive: true);

        double leftDrive = 80.0;
        double rightDrive = 80.0;
        var summary = AvatarKinematics.IntegrateMotorSpikes(dispatches, ref leftDrive, ref rightDrive, options);
        var (forwardSpeed, turnRateDeg) = AvatarKinematics.ComputeBrainMotorOutput(leftDrive, rightDrive, options);

        Assert.Equal(1, summary.InPlaceTurnEvents);
        Assert.True(leftDrive < 0.0, $"Expected left drive to reverse for pivot, got {leftDrive:0.000}.");
        Assert.True(rightDrive > 0.0, $"Expected right drive to push forward for pivot, got {rightDrive:0.000}.");
        Assert.InRange(Math.Abs(forwardSpeed), 0.0, 0.0001);
        Assert.True(turnRateDeg > 0.0, $"Expected positive left turn rate, got {turnRateDeg:0.000}.");
    }

    [Fact]
    public void Maze_Turn_Left_Command_Remains_Forward_Only()
    {
        var dispatches = new[]
        {
            new AvatarDispatchSpike("M1", "L", 100, "L:motor_turn_left_100_0")
        };
        var options = new AvatarKinematicsOptions(
            MaxMotorDrive: 240.0,
            ForwardSpeedCoefficient: 0.0125,
            TurnSpeedCoefficient: 3.2,
            MinForwardSpeed: 0.0,
            MaxForwardSpeed: 3.2,
            MaxTurnRateDeg: 220.0);

        double leftDrive = 0.0;
        double rightDrive = 0.0;
        AvatarKinematics.IntegrateMotorSpikes(dispatches, ref leftDrive, ref rightDrive, options);
        var (forwardSpeed, turnRateDeg) = AvatarKinematics.ComputeBrainMotorOutput(leftDrive, rightDrive, options);

        Assert.Equal(0.0, leftDrive);
        Assert.True(rightDrive > 0.0);
        Assert.True(forwardSpeed > 0.0);
        Assert.True(turnRateDeg > 0.0);
    }

    [Fact]
    public void About_Face_Command_Pivots_Without_Forward_Drift()
    {
        var dispatches = new[]
        {
            new AvatarDispatchSpike("PremotorCortex", "R", 100, "R:motor_about_face_escape_100_0")
        };
        var options = new AvatarKinematicsOptions(
            MaxMotorDrive: 240.0,
            ForwardSpeedCoefficient: 0.0128,
            TurnSpeedCoefficient: 3.2,
            MinForwardSpeed: -1.6,
            MaxForwardSpeed: 8.1,
            MaxTurnRateDeg: 240.0,
            AllowSignedMotorDrive: true,
            InPlaceTurnCancelsForwardDrive: true);

        double leftDrive = 90.0;
        double rightDrive = 90.0;
        var summary = AvatarKinematics.IntegrateMotorSpikes(dispatches, ref leftDrive, ref rightDrive, options);
        var (forwardSpeed, turnRateDeg) = AvatarKinematics.ComputeBrainMotorOutput(leftDrive, rightDrive, options);

        Assert.Equal(1, summary.InPlaceTurnEvents);
        Assert.InRange(Math.Abs(forwardSpeed), 0.0, 0.0001);
        Assert.NotEqual(0.0, turnRateDeg);
    }

    [Fact]
    public void Avoid_Threat_Command_Produces_Retreat_Drive()
    {
        var dispatches = new[]
        {
            new AvatarDispatchSpike("M1", "R", 100, "R:motor_avoid_threat_100_0")
        };
        var options = new AvatarKinematicsOptions(
            MaxMotorDrive: 240.0,
            ForwardSpeedCoefficient: 0.0128,
            TurnSpeedCoefficient: 3.2,
            MinForwardSpeed: -1.6,
            MaxForwardSpeed: 8.1,
            MaxTurnRateDeg: 240.0,
            AllowSignedMotorDrive: true,
            InPlaceTurnCancelsForwardDrive: true);

        double leftDrive = 0.0;
        double rightDrive = 0.0;
        AvatarKinematics.IntegrateMotorSpikes(dispatches, ref leftDrive, ref rightDrive, options);
        var (forwardSpeed, turnRateDeg) = AvatarKinematics.ComputeBrainMotorOutput(leftDrive, rightDrive, options);

        Assert.True(leftDrive < 0.0);
        Assert.True(rightDrive < 0.0);
        Assert.True(forwardSpeed < 0.0);
        Assert.InRange(Math.Abs(turnRateDeg), 0.0, 0.0001);
    }
}
