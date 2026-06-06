namespace NRE.SimAvatar;

public readonly record struct AvatarBodyStateInput(
    AvatarBodyTelemetry Telemetry,
    AvatarBodyStateProfile Profile);
