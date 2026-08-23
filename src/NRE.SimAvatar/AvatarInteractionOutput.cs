namespace NRE.SimAvatar;

/// <summary>
/// Physical effector output. It carries actuator drive only; object selection and
/// consequences are resolved by contact geometry in the environment.
/// </summary>
public readonly record struct AvatarInteractionOutput(
    double ManipulatorDrive,
    double LeftHandGraspDrive = 0.0,
    double RightHandGraspDrive = 0.0);
