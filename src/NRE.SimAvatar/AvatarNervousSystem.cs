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
    public double LeftHandGraspDrive { get; private set; }
    public double RightHandGraspDrive { get; private set; }
    public double LeftShoulderSagittalDrive { get; private set; }
    public double RightShoulderSagittalDrive { get; private set; }
    public double LeftShoulderCoronalDrive { get; private set; }
    public double RightShoulderCoronalDrive { get; private set; }
    public double LeftElbowDrive { get; private set; }
    public double RightElbowDrive { get; private set; }
    public double LeftHipCoronalDrive { get; private set; }
    public double RightHipCoronalDrive { get; private set; }
    public double LeftAnkleSagittalDrive { get; private set; }
    public double RightAnkleSagittalDrive { get; private set; }
    public double LeftAnkleCoronalDrive { get; private set; }
    public double RightAnkleCoronalDrive { get; private set; }
    public double TrunkYawDrive { get; private set; }
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
        LeftHandGraspDrive = 0.0;
        RightHandGraspDrive = 0.0;
        LeftShoulderSagittalDrive = 0.0;
        RightShoulderSagittalDrive = 0.0;
        LeftShoulderCoronalDrive = 0.0;
        RightShoulderCoronalDrive = 0.0;
        LeftElbowDrive = 0.0;
        RightElbowDrive = 0.0;
        LeftHipCoronalDrive = 0.0;
        RightHipCoronalDrive = 0.0;
        LeftAnkleSagittalDrive = 0.0;
        RightAnkleSagittalDrive = 0.0;
        LeftAnkleCoronalDrive = 0.0;
        RightAnkleCoronalDrive = 0.0;
        TrunkYawDrive = 0.0;
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
        LeftHandGraspDrive *= Math.Clamp(smoothingOverride ?? _options.DriveDecay, 0.0, 1.0);
        RightHandGraspDrive *= Math.Clamp(smoothingOverride ?? _options.DriveDecay, 0.0, 1.0);
        LeftShoulderSagittalDrive *= Math.Clamp(smoothingOverride ?? _options.DriveDecay, 0.0, 1.0);
        RightShoulderSagittalDrive *= Math.Clamp(smoothingOverride ?? _options.DriveDecay, 0.0, 1.0);
        LeftShoulderCoronalDrive *= Math.Clamp(smoothingOverride ?? _options.DriveDecay, 0.0, 1.0);
        RightShoulderCoronalDrive *= Math.Clamp(smoothingOverride ?? _options.DriveDecay, 0.0, 1.0);
        LeftElbowDrive *= Math.Clamp(smoothingOverride ?? _options.DriveDecay, 0.0, 1.0);
        RightElbowDrive *= Math.Clamp(smoothingOverride ?? _options.DriveDecay, 0.0, 1.0);
        LeftHipCoronalDrive *= Math.Clamp(smoothingOverride ?? _options.DriveDecay, 0.0, 1.0);
        RightHipCoronalDrive *= Math.Clamp(smoothingOverride ?? _options.DriveDecay, 0.0, 1.0);
        LeftAnkleSagittalDrive *= Math.Clamp(smoothingOverride ?? _options.DriveDecay, 0.0, 1.0);
        RightAnkleSagittalDrive *= Math.Clamp(smoothingOverride ?? _options.DriveDecay, 0.0, 1.0);
        LeftAnkleCoronalDrive *= Math.Clamp(smoothingOverride ?? _options.DriveDecay, 0.0, 1.0);
        RightAnkleCoronalDrive *= Math.Clamp(smoothingOverride ?? _options.DriveDecay, 0.0, 1.0);
        TrunkYawDrive *= Math.Clamp(smoothingOverride ?? _options.DriveDecay, 0.0, 1.0);
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
        var hand = AvatarEffectorCatalog.SummarizeHandDrive(dispatches);
        LeftHandGraspDrive = ApplyReciprocalDelta(LeftHandGraspDrive, hand.LeftDelta);
        RightHandGraspDrive = ApplyReciprocalDelta(RightHandGraspDrive, hand.RightDelta);
        ManipulatorDrive = Math.Max(Math.Abs(LeftHandGraspDrive), Math.Abs(RightHandGraspDrive));
        var arm = AvatarEffectorCatalog.SummarizeArmDrive(dispatches);
        LeftShoulderSagittalDrive = ApplyReciprocalDelta(
            LeftShoulderSagittalDrive, arm.LeftShoulderSagittalDelta);
        RightShoulderSagittalDrive = ApplyReciprocalDelta(
            RightShoulderSagittalDrive, arm.RightShoulderSagittalDelta);
        LeftShoulderCoronalDrive = ApplyReciprocalDelta(
            LeftShoulderCoronalDrive, arm.LeftShoulderCoronalDelta);
        RightShoulderCoronalDrive = ApplyReciprocalDelta(
            RightShoulderCoronalDrive, arm.RightShoulderCoronalDelta);
        LeftElbowDrive = ApplyReciprocalDelta(LeftElbowDrive, arm.LeftElbowDelta);
        RightElbowDrive = ApplyReciprocalDelta(RightElbowDrive, arm.RightElbowDelta);
        var leg = AvatarEffectorCatalog.SummarizeLegDrive(dispatches);
        LeftHipCoronalDrive = ApplyReciprocalDelta(
            LeftHipCoronalDrive, leg.LeftHipCoronalDelta);
        RightHipCoronalDrive = ApplyReciprocalDelta(
            RightHipCoronalDrive, leg.RightHipCoronalDelta);
        LeftAnkleSagittalDrive = ApplyReciprocalDelta(
            LeftAnkleSagittalDrive, leg.LeftAnkleSagittalDelta);
        RightAnkleSagittalDrive = ApplyReciprocalDelta(
            RightAnkleSagittalDrive, leg.RightAnkleSagittalDelta);
        LeftAnkleCoronalDrive = ApplyReciprocalDelta(
            LeftAnkleCoronalDrive, leg.LeftAnkleCoronalDelta);
        RightAnkleCoronalDrive = ApplyReciprocalDelta(
            RightAnkleCoronalDrive, leg.RightAnkleCoronalDelta);
        var axial = AvatarEffectorCatalog.SummarizeAxialDrive(dispatches);
        TrunkYawDrive = ApplyReciprocalDelta(TrunkYawDrive, axial.TrunkYawDelta);
        var posture = AvatarEffectorCatalog.SummarizePostureDrive(dispatches);
        var orienting = AvatarEffectorCatalog.SummarizeOrientingDrive(dispatches);
        HeadYawDrive = ApplyReciprocalDelta(HeadYawDrive, orienting.YawDelta);
        HeadPitchDrive = ApplyReciprocalDelta(HeadPitchDrive, orienting.PitchDelta);
        ApplyPostureCompetition(posture);

        if (motorSummary.MotorEvents > 0 || hand.Events > 0 || arm.Events > 0 || leg.Events > 0 ||
            axial.Events > 0 ||
            orienting.Events > 0 || posture.Events > 0)
        {
            TicksWithoutMotorDispatch = 0;
        }
        else
        {
            TicksWithoutMotorDispatch++;
        }

        LastMotorDispatchCount = motorSummary.MotorEvents + leg.Events + axial.Events;
        LastManipulatorDispatchCount = hand.Events + arm.Events;
        LastOrientingDispatchCount = orienting.Events;
        LastPostureDispatchCount = posture.Events;
        return CurrentSignal();
    }

    private void ApplyPostureCompetition(AvatarPostureDrive posture)
    {
        if (posture.Events <= 0)
        {
            return;
        }

        var winnerIndex = 0;
        var winnerDrive = posture.StandDelta;
        if (posture.CrouchDelta > winnerDrive)
        {
            winnerIndex = 1;
            winnerDrive = posture.CrouchDelta;
        }
        if (posture.SitDelta > winnerDrive)
        {
            winnerIndex = 2;
            winnerDrive = posture.SitDelta;
        }
        if (posture.LieDelta > winnerDrive)
        {
            winnerIndex = 3;
        }

        StandDrive = winnerIndex == 0
            ? Math.Clamp(StandDrive + posture.StandDelta, 0.0, 1.0)
            : 0.0;
        CrouchDrive = winnerIndex == 1
            ? Math.Clamp(CrouchDrive + posture.CrouchDelta, 0.0, 1.0)
            : 0.0;
        SitDrive = winnerIndex == 2
            ? Math.Clamp(SitDrive + posture.SitDelta, 0.0, 1.0)
            : 0.0;
        LieDrive = winnerIndex == 3
            ? Math.Clamp(LieDrive + posture.LieDelta, 0.0, 1.0)
            : 0.0;
    }

    private static double ApplyReciprocalDelta(double current, double delta)
    {
        if (!double.IsFinite(delta) || Math.Abs(delta) < 0.000001)
        {
            return current;
        }

        var released = Math.Abs(current) >= 0.000001 && Math.Sign(current) != Math.Sign(delta)
            ? 0.0
            : current;
        return Math.Clamp(released + delta, -1.0, 1.0);
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
            TicksWithoutMotorDispatch,
            LeftHipCoronalDrive,
            RightHipCoronalDrive,
            LeftAnkleSagittalDrive,
            RightAnkleSagittalDrive,
            LeftAnkleCoronalDrive,
            RightAnkleCoronalDrive,
            TrunkYawDrive,
            LeftHandGraspDrive,
            RightHandGraspDrive);
}
