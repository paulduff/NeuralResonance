namespace NRE.SimAvatar;

public readonly record struct AvatarNervousSystemSignal(
    double LeftMotorDrive,
    double RightMotorDrive,
    double ManipulatorDrive,
    double HeadYawDrive,
    double HeadPitchDrive,
    double StandDrive,
    double CrouchDrive,
    double SitDrive,
    double LieDrive,
    int MotorEvents,
    int ManipulatorEvents,
    int OrientingEvents,
    int PostureEvents,
    int TicksWithoutMotorDispatch)
{
    public AvatarNervousSystemSignal(
        double leftMotorDrive,
        double rightMotorDrive,
        double manipulatorDrive,
        int motorEvents,
        int manipulatorEvents,
        int ticksWithoutMotorDispatch)
        : this(
            leftMotorDrive,
            rightMotorDrive,
            manipulatorDrive,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            motorEvents,
            manipulatorEvents,
            0,
            0,
            ticksWithoutMotorDispatch)
    {
    }
}
