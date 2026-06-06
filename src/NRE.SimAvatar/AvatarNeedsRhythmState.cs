namespace NRE.SimAvatar;

public readonly record struct AvatarNeedsRhythmState(
    double Hunger,
    double Fatigue,
    double SleepPressure,
    double Stress,
    double Curiosity,
    double Restlessness,
    double Recovery,
    double RestNeed,
    long UpdatedUnixMs,
    string DominantNeed = "none")
{
    public static AvatarNeedsRhythmState Resting(long updatedUnixMs = 0) => new(
        Hunger: 0.0,
        Fatigue: 0.0,
        SleepPressure: 0.0,
        Stress: 0.0,
        Curiosity: 0.0,
        Restlessness: 0.0,
        Recovery: 0.0,
        RestNeed: 0.0,
        UpdatedUnixMs: updatedUnixMs,
        DominantNeed: "none");
}
