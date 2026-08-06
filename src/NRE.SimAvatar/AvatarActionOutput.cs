namespace NRE.SimAvatar;

public readonly record struct AvatarActionOutput(
    AvatarMotorOutput Movement,
    long EmittedUnixMs,
    string OutputSource = "avatar_action");
