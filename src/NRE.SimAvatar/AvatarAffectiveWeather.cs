namespace NRE.SimAvatar;

public readonly record struct AvatarAffectiveWeather(
    string State,
    double Valence,
    double Arousal,
    double Confidence,
    string Reason,
    long UpdatedUnixMs,
    string OutputSource = "avatar_affective_weather")
{
    public static AvatarAffectiveWeather Neutral(long updatedUnixMs = 0) => new(
        State: "calm",
        Valence: 0.0,
        Arousal: 0.0,
        Confidence: 0.0,
        Reason: "none",
        UpdatedUnixMs: updatedUnixMs);
}
