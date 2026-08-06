namespace NRE.SimAvatar;

/// <summary>
/// Avatar-side nervous system: interprets brain dispatch spikes into body signals.
/// The DNN remains the brain; this object is the spinal/peripheral layer that keeps
/// motor drive, idle arousal, and tool/body intent inside the avatar.
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

    public int LastMotorDispatchCount { get; private set; }

    public int TicksWithoutMotorDispatch { get; private set; }

    public void ResetMotor()
    {
        LeftMotorDrive = 0.0;
        RightMotorDrive = 0.0;
        LastMotorDispatchCount = 0;
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
    }

    public (double ForwardSpeed, double TurnRateDeg) ComputeMotorOutput(
        double forwardGain = 1.0,
        double turnGain = 1.0,
        double forwardScale = 1.0)
        => AvatarKinematics.ComputeBrainMotorOutput(
            LeftMotorDrive,
            RightMotorDrive,
            _options.Kinematics,
            forwardGain: forwardGain,
            turnGain: turnGain,
            forwardScale: forwardScale);

    public AvatarNervousSystemSignal InterpretBrainSignals(
        IReadOnlyList<AvatarDispatchSpike> dispatches,
        AvatarNervousSystemBodyState body)
    {
        ArgumentNullException.ThrowIfNull(dispatches);

        var tool = AvatarToolSignal.None;
        if (body.IsSleeping)
        {
            ResetMotor();
            return CurrentSignal(tool);
        }

        var left = LeftMotorDrive;
        var right = RightMotorDrive;
        var motorSummary = AvatarKinematics.IntegrateMotorSpikes(dispatches, ref left, ref right, _options.Kinematics);
        SetMotorDrive(left, right);

        if (motorSummary.MotorEvents > 0)
        {
            TicksWithoutMotorDispatch = 0;
        }
        else
        {
            TicksWithoutMotorDispatch++;
            ApplyIdleFallback(body);
        }

        LastMotorDispatchCount = motorSummary.MotorEvents;
        return CurrentSignal(tool);
    }

    private AvatarNervousSystemSignal CurrentSignal(AvatarToolSignal tool)
        => new(LeftMotorDrive, RightMotorDrive, LastMotorDispatchCount, TicksWithoutMotorDispatch, tool);

    private void ApplyIdleFallback(AvatarNervousSystemBodyState body)
    {
        // Brain-drive only: idle body state must not synthesize locomotion.
        // Movement is produced only by motor pathway dispatches.
    }

}
