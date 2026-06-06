namespace NRE.SimAvatar;

public readonly record struct AvatarReflexOutput(
    string Name,
    double Intensity,
    double ForwardScale,
    double TurnBiasDeg,
    string? Target,
    long EmittedUnixMs,
    string OutputSource = "avatar_reflex")
{
    public static AvatarReflexOutput None(long emittedUnixMs = 0) => new(
        Name: "none",
        Intensity: 0.0,
        ForwardScale: 1.0,
        TurnBiasDeg: 0.0,
        Target: null,
        EmittedUnixMs: emittedUnixMs);
}
