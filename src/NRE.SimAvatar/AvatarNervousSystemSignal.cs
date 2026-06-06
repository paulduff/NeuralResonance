namespace NRE.SimAvatar;

public readonly record struct AvatarNervousSystemSignal(
    double LeftMotorDrive,
    double RightMotorDrive,
    int MotorEvents,
    int TicksWithoutMotorDispatch,
    AvatarToolSignal Tool);
