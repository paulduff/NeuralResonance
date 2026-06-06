namespace NRE.SimAvatar;

public readonly record struct AvatarBodyEvent(
    string Kind,
    double Intensity,
    string Description,
    long ObservedUnixMs,
    string Source = "avatar_body");
