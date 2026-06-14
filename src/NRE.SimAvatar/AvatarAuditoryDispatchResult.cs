namespace NRE.SimAvatar;

public readonly record struct AvatarAuditoryDispatchResult(
    string Pattern,
    int GeneratedSpikes,
    int DeliveredSpikes,
    int TargetInstances,
    bool PausedDueToSleep,
    bool Accepted = false,
    bool DispatchDeferred = false);
