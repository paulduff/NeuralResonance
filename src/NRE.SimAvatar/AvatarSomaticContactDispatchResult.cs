namespace NRE.SimAvatar;

public sealed record AvatarSomaticContactDispatchResult(
    bool Accepted,
    bool DispatchDeferred,
    int GeneratedSpikes,
    int DeliveredSpikes,
    int TargetInstances,
    int ReceptorSector,
    int ActiveReceptorPopulations,
    float PressureActivation,
    float OnsetActivation,
    float VibrationActivation,
    float StretchActivation,
    float HighThresholdActivation);
