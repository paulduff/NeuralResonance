namespace NRE.SimAvatar;

/// <summary>
/// Physical effector output. It carries actuator drive only; object selection and
/// consequences are resolved by contact geometry in the environment.
/// </summary>
public readonly record struct AvatarInteractionOutput(double ManipulatorDrive);
