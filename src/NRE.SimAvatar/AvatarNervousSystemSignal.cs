namespace NRE.SimAvatar;

public readonly record struct AvatarNervousSystemSignal(
    double LeftMotorDrive,
    double RightMotorDrive,
    double ManipulatorDrive,
    int MotorEvents,
    int ManipulatorEvents,
    int TicksWithoutMotorDispatch);
