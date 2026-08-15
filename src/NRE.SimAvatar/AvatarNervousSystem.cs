namespace NRE.SimAvatar;

/// <summary>
/// Spinal/peripheral motor layer. It integrates neuronal motor population spikes
/// and exposes no independent goals or semantic action authority. Reciprocal
/// inhibition and postural reflexes below this boundary are physical spinal
/// machinery, not behavioural policy.
/// </summary>
public sealed class AvatarNervousSystem
{
    private readonly AvatarNervousSystemOptions _options;

    public AvatarNervousSystem(AvatarNervousSystemOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public double LeftMotorDrive { get; private set; }

    public double RightMotorDrive { get; private set; }

    public double ManipulatorDrive { get; private set; }
    public double LeftShoulderSagittalDrive { get; private set; }
    public double RightShoulderSagittalDrive { get; private set; }
    public double LeftShoulderCoronalDrive { get; private set; }
    public double RightShoulderCoronalDrive { get; private set; }
    public double LeftElbowDrive { get; private set; }
    public double RightElbowDrive { get; private set; }
    public double HeadYawDrive { get; private set; }
    public double HeadPitchDrive { get; private set; }
    public double StandDrive { get; private set; }
    public double CrouchDrive { get; private set; }
    public double SitDrive { get; private set; }
    public double LieDrive { get; private set; }

    public int LastMotorDispatchCount { get; private set; }

    public int LastManipulatorDispatchCount { get; private set; }
    public int LastOrientingDispatchCount { get; private set; }
    public int LastPostureDispatchCount { get; private set; }

    public int TicksWithoutMotorDispatch { get; private set; }

    public void ResetMotor()
    {
        LeftMotorDrive = 0.0;
        RightMotorDrive = 0.0;
        ManipulatorDrive = 0.0;
        LeftShoulderSagittalDrive = 0.0;
        RightShoulderSagittalDrive = 0.0;
        LeftShoulderCoronalDrive = 0.0;
        RightShoulderCoronalDrive = 0.0;
        LeftElbowDrive = 0.0;
        RightElbowDrive = 0.0;
        HeadYawDrive = 0.0;
        HeadPitchDrive = 0.0;
        StandDrive = 0.0;
        CrouchDrive = 0.0;
        SitDrive = 0.0;
        LieDrive = 0.0;
        LastMotorDispatchCount = 0;
        LastManipulatorDispatchCount = 0;
        LastOrientingDispatchCount = 0;
        LastPostureDispatchCount = 0;
        TicksWithoutMotorDispatch = 0;
    }

    public void SetMotorDrive(double left, double right)
    {
        var min = _options.Kinematics.AllowSignedMotorDrive ? -_options.Kinematics.MaxMotorDrive : 0.0;
        LeftMotorDrive = Math.Clamp(left, min, _options.Kinematics.MaxMotorDrive);
        RightMotorDrive = Math.Clamp(right, min, _options.Kinematics.MaxMotorDrive);
    }

    public void AddMotorDrive(double leftDelta, double rightDelta)
        => SetMotorDrive(LeftMotorDrive + leftDelta, RightMotorDrive + rightDelta);

    public void ApplyDriveDecay(double? smoothingOverride = null)
    {
        var left = LeftMotorDrive;
        var right = RightMotorDrive;
        AvatarKinematics.ApplyDriveDecay(ref left, ref right, smoothingOverride ?? _options.DriveDecay);
        SetMotorDrive(left, right);
        ManipulatorDrive *= Math.Clamp(smoothingOverride ?? _options.DriveDecay, 0.0, 1.0);
        LeftShoulderSagittalDrive *= Math.Clamp(smoothingOverride ?? _options.DriveDecay, 0.0, 1.0);
        RightShoulderSagittalDrive *= Math.Clamp(smoothingOverride ?? _options.DriveDecay, 0.0, 1.0);
        LeftShoulderCoronalDrive *= Math.Clamp(smoothingOverride ?? _options.DriveDecay, 0.0, 1.0);
        RightShoulderCoronalDrive *= Math.Clamp(smoothingOverride ?? _options.DriveDecay, 0.0, 1.0);
        LeftElbowDrive *= Math.Clamp(smoothingOverride ?? _options.DriveDecay, 0.0, 1.0);
        RightElbowDrive *= Math.Clamp(smoothingOverride ?? _options.DriveDecay, 0.0, 1.0);
        HeadYawDrive *= Math.Clamp(smoothingOverride ?? _options.DriveDecay, 0.0, 1.0);
        HeadPitchDrive *= Math.Clamp(smoothingOverride ?? _options.DriveDecay, 0.0, 1.0);
        var postureDecay = Math.Clamp(smoothingOverride ?? _options.DriveDecay, 0.0, 1.0);
        StandDrive *= postureDecay;
        CrouchDrive *= postureDecay;
        SitDrive *= postureDecay;
        LieDrive *= postureDecay;
    }

    public (double ForwardSpeed, double TurnRateDeg) ComputeMotorOutput(
        double forwardGain = 1.0,
        double turnGain = 1.0)
        => AvatarKinematics.ComputeBrainMotorOutput(
            LeftMotorDrive,
            RightMotorDrive,
            _options.Kinematics,
            forwardGain: forwardGain,
            turnGain: turnGain);

    public AvatarNervousSystemSignal InterpretBrainSignals(IReadOnlyList<AvatarDispatchSpike> dispatches)
    {
        ArgumentNullException.ThrowIfNull(dispatches);

        var left = LeftMotorDrive;
        var right = RightMotorDrive;
        var motorSummary = AvatarKinematics.IntegrateMotorSpikes(dispatches, ref left, ref right, _options.Kinematics);
        SetMotorDrive(left, right);
        var manipulator = AvatarEffectorCatalog.SummarizeManipulatorDrive(dispatches);
        ManipulatorDrive = Math.Clamp(
            ManipulatorDrive + manipulator.DriveDelta,
            0.0,
            1.0);
        var arm = AvatarEffectorCatalog.SummarizeArmDrive(dispatches);
        LeftShoulderSagittalDrive = Math.Clamp(
            LeftShoulderSagittalDrive + arm.LeftShoulderSagittalDelta, -1.0, 1.0);
        RightShoulderSagittalDrive = Math.Clamp(
            RightShoulderSagittalDrive + arm.RightShoulderSagittalDelta, -1.0, 1.0);
        LeftShoulderCoronalDrive = Math.Clamp(
            LeftShoulderCoronalDrive + arm.LeftShoulderCoronalDelta, -1.0, 1.0);
        RightShoulderCoronalDrive = Math.Clamp(
            RightShoulderCoronalDrive + arm.RightShoulderCoronalDelta, -1.0, 1.0);
        LeftElbowDrive = Math.Clamp(LeftElbowDrive + arm.LeftElbowDelta, -1.0, 1.0);
        RightElbowDrive = Math.Clamp(RightElbowDrive + arm.RightElbowDelta, -1.0, 1.0);
        var posture = AvatarEffectorCatalog.SummarizePostureDrive(dispatches);
        var orienting = AvatarEffectorCatalog.SummarizeOrientingDrive(dispatches);
        HeadYawDrive = Math.Clamp(
            HeadYawDrive + orienting.YawDelta,
            -1.0,
            1.0);
        HeadPitchDrive = Math.Clamp(
            HeadPitchDrive + orienting.PitchDelta,
            -1.0,
            1.0);
        StandDrive = Math.Clamp(StandDrive + posture.StandDelta, 0.0, 1.0);
        CrouchDrive = Math.Clamp(CrouchDrive + posture.CrouchDelta, 0.0, 1.0);
        SitDrive = Math.Clamp(SitDrive + posture.SitDelta, 0.0, 1.0);
        LieDrive = Math.Clamp(LieDrive + posture.LieDelta, 0.0, 1.0);

        if (motorSummary.MotorEvents > 0 || manipulator.Events > 0 || arm.Events > 0 ||
            orienting.Events > 0 || posture.Events > 0)
        {
            TicksWithoutMotorDispatch = 0;
        }
        else
        {
            TicksWithoutMotorDispatch++;
        }

        LastMotorDispatchCount = motorSummary.MotorEvents;
        LastManipulatorDispatchCount = manipulator.Events + arm.Events;
        LastOrientingDispatchCount = orienting.Events;
        LastPostureDispatchCount = posture.Events;
        return CurrentSignal();
    }

    private AvatarNervousSystemSignal CurrentSignal()
        => new(
            LeftMotorDrive,
            RightMotorDrive,
            ManipulatorDrive,
            LeftShoulderSagittalDrive,
            RightShoulderSagittalDrive,
            LeftShoulderCoronalDrive,
            RightShoulderCoronalDrive,
            LeftElbowDrive,
            RightElbowDrive,
            HeadYawDrive,
            HeadPitchDrive,
            StandDrive,
            CrouchDrive,
            SitDrive,
            LieDrive,
            LastMotorDispatchCount,
            LastManipulatorDispatchCount,
            LastOrientingDispatchCount,
            LastPostureDispatchCount,
            TicksWithoutMotorDispatch);
}
