namespace NRE.SimAvatar;

public readonly record struct AvatarAuditoryDispatchResult(
    string Pattern,
    int GeneratedSpikes,
    int DeliveredSpikes,
    int TargetInstances,
    bool Accepted = false,
    bool DispatchDeferred = false);
