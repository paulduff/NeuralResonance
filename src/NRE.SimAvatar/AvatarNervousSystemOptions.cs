namespace NRE.SimAvatar;

public sealed record AvatarNervousSystemOptions(
    AvatarKinematicsOptions Kinematics,
    double DriveDecay = 0.92,
    int IdleMotorFallbackTicks = int.MaxValue,
    double IdleMotorFallbackBaseDrive = 36.0,
    double IdleFallbackPhaseStepDeg = 6.5,
    double ToolActionThreshold = 1.2);
