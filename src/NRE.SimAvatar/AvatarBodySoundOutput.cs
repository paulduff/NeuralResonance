namespace NRE.SimAvatar;

public readonly record struct AvatarBodySoundOutput(
    string Pattern,
    double Intensity,
    long EmittedUnixMs,
    string OutputSource = "avatar_body_sound")
{
    public static AvatarBodySoundOutput None(long emittedUnixMs = 0) => new(
        Pattern: "silent",
        Intensity: 0.0,
        EmittedUnixMs: emittedUnixMs);
}
