namespace NRE.SimAvatar;

public readonly record struct AvatarArousalOutput(
    double Level,
    string Mode,
    string Reason,
    long EmittedUnixMs,
    string OutputSource = "avatar_arousal")
{
    public static AvatarArousalOutput None(long emittedUnixMs = 0) => new(
        Level: 0.0,
        Mode: "rest",
        Reason: "none",
        EmittedUnixMs: emittedUnixMs);
}
