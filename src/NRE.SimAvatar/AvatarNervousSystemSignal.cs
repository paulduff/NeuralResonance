namespace NRE.SimAvatar;

public readonly record struct AvatarNervousSystemSignal(
    double LeftMotorDrive,
    double RightMotorDrive,
    double ManipulatorDrive,
    double LeftShoulderSagittalDrive,
    double RightShoulderSagittalDrive,
    double LeftShoulderCoronalDrive,
    double RightShoulderCoronalDrive,
    double LeftElbowDrive,
    double RightElbowDrive,
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
    int TicksWithoutMotorDispatch,
    double LeftHipCoronalDrive = 0.0,
    double RightHipCoronalDrive = 0.0,
    double LeftAnkleSagittalDrive = 0.0,
    double RightAnkleSagittalDrive = 0.0,
    double LeftAnkleCoronalDrive = 0.0,
    double RightAnkleCoronalDrive = 0.0,
    double TrunkYawDrive = 0.0,
    double LeftHandGraspDrive = 0.0,
    double RightHandGraspDrive = 0.0)
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
