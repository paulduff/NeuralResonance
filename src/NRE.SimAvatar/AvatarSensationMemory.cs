namespace NRE.SimAvatar;

public sealed record AvatarSensationMemory(
    long Revision,
    long UpdatedUnixMs,
    AvatarAuditoryCue? LastHeardSound,
    AvatarAudioOutput? LastAudioOutput,
    AvatarBodyStateInput? LastBodyState,
    AvatarOutcomeTelemetry? LastOutcome,
    AvatarObjectObservation? LastSeenObject,
    int? LastSightGeneration,
    long? LastSightTimestampMs,
    string AttentionTarget,
    string BodyMood)
{
    public static AvatarSensationMemory Empty { get; } = new(
        Revision: 0,
        UpdatedUnixMs: 0,
        LastHeardSound: null,
        LastAudioOutput: null,
        LastBodyState: null,
        LastOutcome: null,
        LastSeenObject: null,
        LastSightGeneration: null,
        LastSightTimestampMs: null,
        AttentionTarget: "none",
        BodyMood: "unknown");
}
