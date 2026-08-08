namespace NRE.SimAvatar;

public readonly record struct AvatarActionOutput(
    AvatarMotorOutput Movement,
    AvatarInteractionOutput Interaction,
    long EmittedUnixMs,
    string OutputSource = "avatar_action");
