namespace NRE.SimAvatar;

public readonly record struct AvatarAttentionOutput(
    string Mode,
    string Target,
    string? Hemisphere,
    double Confidence,
    double Salience,
    long EmittedUnixMs,
    string OutputSource = "avatar_attention")
{
    public static AvatarAttentionOutput None(long emittedUnixMs = 0) => new(
        Mode: "rest",
        Target: "none",
        Hemisphere: null,
        Confidence: 0.0,
        Salience: 0.0,
        EmittedUnixMs: emittedUnixMs);
}
