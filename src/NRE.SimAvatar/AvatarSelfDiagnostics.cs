namespace NRE.SimAvatar;

public sealed record AvatarSelfDiagnostics(
    string BodyMood,
    string AttentionTarget,
    string CurrentAction,
    string LastSensation,
    string CurrentNeed,
    string RecentBodyEvent,
    long UpdatedUnixMs);
