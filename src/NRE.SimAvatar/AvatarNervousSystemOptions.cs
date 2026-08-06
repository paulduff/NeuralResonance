namespace NRE.SimAvatar;

public sealed record AvatarNervousSystemOptions(
    AvatarKinematicsOptions Kinematics,
    double DriveDecay = 0.92);
