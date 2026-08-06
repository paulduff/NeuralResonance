using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AvatarKinematicsTests
{
    [Fact]
    public void Opposed_Population_Drive_Pivots_In_Place()
    {
        var dispatches = new[]
        {
            new AvatarDispatchSpike("M1", "L", 100, "population:l:inhibitory:100:0"),
            new AvatarDispatchSpike("M1", "R", 101, "population:r:excitatory:100:0")
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
        var summary = AvatarKinematics.IntegrateMotorSpikes(dispatches, ref leftDrive, ref rightDrive, options);
        var (forwardSpeed, turnRateDeg) = AvatarKinematics.ComputeBrainMotorOutput(leftDrive, rightDrive, options);

        Assert.Equal(2, summary.MotorEvents);
        Assert.True(leftDrive < 0.0, $"Expected left drive to reverse for pivot, got {leftDrive:0.000}.");
        Assert.True(rightDrive > 0.0, $"Expected right drive to push forward for pivot, got {rightDrive:0.000}.");
        Assert.InRange(Math.Abs(forwardSpeed), 0.0, 0.0001);
        Assert.True(turnRateDeg > 0.0, $"Expected positive left turn rate, got {turnRateDeg:0.000}.");
    }

    [Fact]
    public void Forward_Only_Profile_Clamps_Opposed_Population_Drive()
    {
        var dispatches = new[]
        {
            new AvatarDispatchSpike("M1", "L", 100, "population:l:inhibitory:100:0"),
            new AvatarDispatchSpike("M1", "R", 101, "population:r:excitatory:100:0")
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
    public void Opposed_Premotor_Populations_Pivot_Without_Forward_Drift()
    {
        var dispatches = new[]
        {
            new AvatarDispatchSpike("PremotorCortex", "L", 100, "population:l:inhibitory:100:0"),
            new AvatarDispatchSpike("PremotorCortex", "R", 101, "population:r:excitatory:100:0")
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
        var summary = AvatarKinematics.IntegrateMotorSpikes(dispatches, ref leftDrive, ref rightDrive, options);
        var (forwardSpeed, turnRateDeg) = AvatarKinematics.ComputeBrainMotorOutput(leftDrive, rightDrive, options);

        Assert.Equal(2, summary.MotorEvents);
        Assert.InRange(Math.Abs(forwardSpeed), 0.0, 0.0001);
        Assert.NotEqual(0.0, turnRateDeg);
    }

    [Fact]
    public void Bilateral_Inhibitory_Populations_Produce_Retreat_Drive()
    {
        var dispatches = new[]
        {
            new AvatarDispatchSpike("M1", "L", 100, "population:l:inhibitory:100:0"),
            new AvatarDispatchSpike("M1", "R", 101, "population:r:inhibitory:100:0")
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

    [Fact]
    public void Spinal_Population_Output_Produces_Forward_Drive()
    {
        var dispatches = new[]
        {
            new AvatarDispatchSpike("SpinalCordMotor", "M", 100, "population:m:excitatory:100:0")
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
        var summary = AvatarKinematics.IntegrateMotorSpikes(dispatches, ref leftDrive, ref rightDrive, options);
        var (forwardSpeed, turnRateDeg) = AvatarKinematics.ComputeBrainMotorOutput(leftDrive, rightDrive, options);

        Assert.Equal(1, summary.MotorEvents);
        Assert.True(leftDrive > 0.0);
        Assert.True(rightDrive > 0.0);
        Assert.True(forwardSpeed > 0.0);
        Assert.InRange(Math.Abs(turnRateDeg), 0.0, 0.0001);
    }

    [Fact]
    public void Reticular_Population_Output_Produces_Forward_Drive()
    {
        var dispatches = new[]
        {
            new AvatarDispatchSpike("ReticularFormation", "M", 100, "population:m:excitatory:100:0")
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
        var summary = AvatarKinematics.IntegrateMotorSpikes(dispatches, ref leftDrive, ref rightDrive, options);
        var (forwardSpeed, turnRateDeg) = AvatarKinematics.ComputeBrainMotorOutput(leftDrive, rightDrive, options);

        Assert.Equal(1, summary.MotorEvents);
        Assert.True(forwardSpeed > 0.0);
        Assert.InRange(Math.Abs(turnRateDeg), 0.0, 0.0001);
    }

    [Theory]
    [InlineData("motor_rest")]
    [InlineData("motor_guard_body")]
    [InlineData("motor_immobilize_protect")]
    public void Semantic_Motor_Directives_Are_Rejected(string directive)
    {
        var dispatches = new[]
        {
            new AvatarDispatchSpike("SpinalCordMotor", "M", 100, $"M:{directive}_100_0")
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
        var summary = AvatarKinematics.IntegrateMotorSpikes(dispatches, ref leftDrive, ref rightDrive, options);
        var (forwardSpeed, _) = AvatarKinematics.ComputeBrainMotorOutput(leftDrive, rightDrive, options);

        Assert.Equal(0, summary.MotorEvents);
        Assert.True(forwardSpeed <= 0.0);
    }
}
