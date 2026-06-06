namespace NRE.SimAvatar;

public readonly record struct AvatarGestureOutput(
    string Name,
    double Intensity,
    string? Direction,
    long EmittedUnixMs,
    string OutputSource = "avatar_gesture")
{
    public static AvatarGestureOutput None(long emittedUnixMs = 0) => new(
        Name: "none",
        Intensity: 0.0,
        Direction: null,
        EmittedUnixMs: emittedUnixMs);
}
